"""Exercise the actual staging module without an editor, including cycle/error paths."""
from pathlib import Path
import importlib.util,json,shutil,sys,types,uuid
root=Path(__file__).resolve().parents[1];art=root/'artifacts'
transfer=Path(sys.argv[1]).resolve();output=Path(sys.argv[2]).resolve()
assert transfer.is_relative_to(art) and output.is_relative_to(art)
manifest=json.loads((transfer/'project-package.json').read_text(encoding='utf-8-sig'))
unit=next(u for u in manifest['units'] if u['kind']=='scene' and u['name']=='Dependency')
definitions={u['id']:u for u in manifest['units'] if u['kind']=='prefab' and u['name'] in ('DependencyA','DependencyB','DependencyC')}
assert len(definitions)==3
folder=output.parent/('catalog-stage-'+uuid.uuid4().hex[:8]);shutil.copytree(transfer/unit['package'],folder)
sys.modules['unreal']=types.ModuleType('unreal')
spec=importlib.util.spec_from_file_location('runtime_templates',root/'adapters/unreal/runtime_templates.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
original=(folder/'package.json').read_bytes();before=json.loads(original)
assert {f['value'] for n in before['nodes'] for b in n.get('behaviours',[]) for f in b['fields'] if f['kind']=='prefab' and f['value']}=={next(k for k,v in definitions.items() if v['name']=='DependencyA')}
assert module.stage(str(folder),manifest,str(transfer))==original
staged=json.loads((folder/'package.json').read_text(encoding='utf-8'))
roots=[n['runtimePrefab'] for n in staged['nodes'] if n.get('runtimePrefab')]
assert len(roots)==3 and set(roots)==set(definitions)
ids={n['id'] for n in staged['nodes']};components={b['id'] for n in staged['nodes'] for b in n.get('behaviours',[])}
assert len(ids)==len(staged['nodes'])
for n in staged['nodes']:
 for b in n.get('behaviours',[]):
  for f in b['fields']:
   if f['value'] and f['kind'] in ('node','transform'):assert f['value'] in ids
   if f['value'] and f['kind']=='component':assert f['value'] in components
(folder/'package.json').write_bytes(original)
missing=next(k for k,v in definitions.items() if v['name']=='DependencyC')
broken={**manifest,'units':[u for u in manifest['units'] if u['id']!=missing]}
try:module.stage(str(folder),broken,str(transfer))
except RuntimeError as error:assert missing in str(error)
else:raise AssertionError('Missing transitive dependency was accepted')
assert (folder/'package.json').read_bytes()==original
output.write_text(json.dumps({'status':'PASS','directReferences':1,'transitivePrototypes':len(roots),'cyclesTerminated':True,'internalReferencesResolved':True,'missingTransitiveRejected':True,'packageUnchangedOnFailure':True,'stagedFolder':str(folder)},indent=2),encoding='utf-8')
print('RUNTIME_DEPENDENCY_CATALOG_PASS')
