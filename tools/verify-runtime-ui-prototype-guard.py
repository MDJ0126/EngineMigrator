"""Reject an actual native widget callback edit and restore without saving assets."""
import json,os,sys,unreal
with open(os.path.join(unreal.Paths.project_dir(),'EMUIPrototypeGuardJob.json'),encoding='utf-8-sig') as stream:job=json.load(stream)
sys.path.insert(0,job['adapters']);import runtime_prototype_guard as guard,ui_components
subsystem=unreal.get_editor_subsystem(unreal.EditorActorSubsystem);cases=[]
for path in job['maps']:
 world=unreal.EditorLoadingAndSavingUtils.load_map(path);assert world
 guard.verify(world,subsystem,required=True);actors=subsystem.get_all_level_actors()
 package={'nodes':[json.loads(raw) for a in actors if (raw:=unreal.EditorAssetLibrary.get_metadata_tag(a,'EngineMigratorSceneNode'))]}
 candidates=[(a,json.loads(raw)) for a in actors if (raw:=unreal.EditorAssetLibrary.get_metadata_tag(a,ui_components.KEY))]
 baseline=json.loads(unreal.EditorAssetLibrary.get_metadata_tag(world,guard.KEY))
 if not baseline['definition']:candidates=[(a,d) for a,d in candidates if 'EMRuntimeTemplate' in map(str,a.tags)]
 actor,data=next((a,d) for a,d in candidates if any(n['ui'].get('onClick') for n in ui_components.members(package,d['owner'])))
 widget_class=actor.get_editor_property('EM_WidgetClass') if 'EMRuntimeUI' in map(str,actor.tags) else unreal.load_class(None,data['widget']+'_C');defaults=unreal.get_default_object(widget_class)
 before=defaults.get_editor_property('EM_Bindings');changed=json.loads(before);changed['callbacks'][0]['method']='Second'
 assert changed['callbacks'][0]['method']!=json.loads(before)['callbacks'][0]['method']
 try:
  defaults.set_editor_property('EM_Bindings',json.dumps(changed))
  try:guard.verify(world,subsystem,required=True)
  except RuntimeError as error:assert 'state changed independently' in str(error)
  else:raise AssertionError('Unsynchronized native UI callback edit was accepted')
 finally:defaults.set_editor_property('EM_Bindings',before)
 guard.verify(world,subsystem,required=True);cases.append({'map':path,'actualWidgetCallbackChanged':True,'unsynchronizedUIEditRejected':True,'restoredInMemory':True})
with open(job['output'],'w',encoding='utf-8') as stream:json.dump({'status':'PASS','cases':cases,'mapSaved':False,'assetSaved':False},stream,indent=2)
unreal.log('RUNTIME_UI_PROTOTYPE_GUARD_PASS')
