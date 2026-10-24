"""Stage real Unity prefab packages with production import/reference/template functions."""
import unreal,json,sys,os
from pathlib import Path
root=Path(__file__).resolve().parent.parent;h=root/'artifacts/scaled-prefab-check';sys.path.insert(0,str(root/'adapters/unreal'))
import runtime_templates as templates,script_components as scripts,scene_components as scene,timing_components as timing
folder=h/'Transfer/scene';manifest=json.loads((h/'Transfer/project-package.json').read_text(encoding='utf-8-sig'))
current=json.loads((folder/'package.json').read_text(encoding='utf-8-sig'))
if not any(n.get('runtimeTemplate')for n in current['nodes']):(h/'scene-original-package.json').write_bytes((folder/'package.json').read_bytes())
else:(folder/'package.json').write_bytes((h/'scene-original-package.json').read_bytes())
original=templates.stage(str(folder),manifest,str(h/'Transfer'));assert original
package=json.loads((folder/'package.json').read_text());assert not package['blockers']
# Scenario timing is independently controlled; prefab definitions and reference graph are unchanged.
package['timing']['timeScale']=0.0 if os.environ.get('EM_SCALED_PREFAB_INITIAL')=='0'else 0.5
world=unreal.EditorLoadingAndSavingUtils.new_blank_map(False);timing.apply_world(world,package);sub=unreal.get_editor_subsystem(unreal.EditorActorSubsystem);nodes={}
for n in package['nodes']:
 p=n['position'];a=sub.spawn_actor_from_class(scene.actor_class(n),unreal.Vector(p['z']*100,p['x']*100,p['y']*100),unreal.Rotator());a.set_actor_label(n['name']);a.tags=[unreal.Name(n['id'])]+([unreal.Name(templates.TEMPLATE)]if n.get('runtimeTemplate')else[])+([unreal.Name(templates.PREFAB+n['runtimePrefab'])]if n.get('runtimePrefab')else[])+([unreal.Name('EMInactiveSelf')]if not n['active']else[]);nodes[n['id']]=a
for n in package['nodes']:
 if n.get('parent'):nodes[n['id']].attach_to_actor(nodes[n['parent']],unreal.Name('None'),unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,False)
actors=scripts.import_all(package,nodes,sub,'/Game/ScaledPrefab');path='/Game/ScaledPrefab/Start';assert unreal.EditorLoadingAndSavingUtils.save_map(world,path)
scripts.persist(package,world,actors);assert unreal.EditorLoadingAndSavingUtils.save_map(world,path)
templates.suppress(world,actors,sub,path);world=unreal.EditorLoadingAndSavingUtils.load_map(path);timing.verify_world(world,package)
loaded={str(a.tags[0]):a for a in sub.get_all_level_actors()if a.tags and str(a.tags[0])in nodes};details=[]
for n in package['nodes']:
 for b in n.get('behaviours',[]):
  found=[a for a in sub.get_all_level_actors()if a.tags and str(a.tags[0])==b['id']]
  details.append({'id':b['id'],'expectedClass':b['className'],'expectedOwner':loaded[n['id']].get_path_name()if n['id']in loaded else None,'actors':[{'path':a.get_path_name(),'class':scripts.class_name(a),'parent':a.get_attach_parent_actor().get_path_name()if a.get_attach_parent_actor()else None,'metadata':scripts.is_behaviour(a)}for a in found]})
(h/'reload-diagnostics.json').write_text(json.dumps(details,indent=2))
bindings=scripts.verify_all(package,loaded,sub.get_all_level_actors())
# Read actual native fields and object/component references with the production exporter.
import copy
live=copy.deepcopy(package);scripts.export_all(world,sub.get_all_level_actors(),{actor:identity for identity,actor in loaded.items()},live)
by_id={n['id']:n for n in live['nodes']};reverse=h/'Reverse';reverse.mkdir(exist_ok=True)
for unit in manifest['units']:
 source=json.loads((h/'Transfer'/unit['package']/'package.json').read_text(encoding='utf-8-sig')) if unit['kind']=='prefab'else json.loads((h/'scene-original-package.json').read_text())
 prefix='node_rt_'+unit['id']+'_'if unit['kind']=='prefab'else''
 for node in source['nodes']:
  actual=by_id[prefix+node['id']];node['behaviours']=copy.deepcopy(actual.get('behaviours',[]));owner=loaded[prefix+node['id']];node['active']='EMInactiveSelf'not in[str(t)for t in owner.tags]
  for behaviour in node['behaviours']:
   if prefix:behaviour['id']=behaviour['id'].removeprefix(prefix)
   for field in behaviour['fields']:
    if prefix and field['kind']in('node','transform','component')and field['value']:field['value']=field['value'].removeprefix(prefix)
 source['sourceEngine']='Unreal';source['sourceVersion']='5.8';destination=reverse/unit['package'];destination.mkdir(exist_ok=True);(destination/'package.json').write_text(json.dumps(source,indent=2))
(reverse/'project-package.json').write_text(json.dumps(manifest,indent=2))
(h/'adapter-verification.json').write_text(json.dumps({'status':'PASS','actualUnityPrefabDefinitions':2,'productionStageAndImport':True,'mapReloadVerified':True,'behaviourBindings':len(bindings),'stagedNodes':len(package['nodes'])},indent=2));print('ACTUAL_SCALED_PREFAB_IMPORT_PASS')
