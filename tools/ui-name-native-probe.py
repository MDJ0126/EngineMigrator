"""Invoke the real generated methods in the existing private Unreal project."""
import json,os,unreal
sub=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
cls=unreal.load_class(None,'/Script/EMActorPort.AuditUiName')
assert cls
def get_name(actor):
 chunks={}
 for tag in actor.tags:
  value=str(tag)
  if value.startswith('EMRuntimeName:'):
   _,index,encoded=value.split(':',2);chunks[int(index)]=encoded
 units=''.join(chunks[k] for k in sorted(chunks))
 return ''.join(chr(int(units[i:i+8],16)) for i in range(0,len(units),8))
def set_initial_name(actor):
 encoded=''.join(f'{ord(c):08X}' for c in 'Initial UI')
 actor.tags=list(actor.tags)+[unreal.Name('EMRuntimeName:0:'+encoded)]
def owner_of(ref):return ref.get_attach_parent_actor() if 'EMUIProxy' in [str(t) for t in ref.tags] else ref
rows=[]
for existing in (False,True):
 owner=sub.spawn_actor_from_class(unreal.StaticMeshActor,unreal.Vector(),unreal.Rotator())
 if existing:owner.tags=[unreal.Name('EMUI:Canvas'),unreal.Name('EMUI:Button')]
 set_initial_name(owner)
 actor=sub.spawn_actor_from_class(cls,unreal.Vector(),unreal.Rotator())
 actor.attach_to_actor(owner,unreal.Name('None'),unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,False)
 actor.set_editor_property('EM_Field_Owner',owner);actor.set_editor_property('Existing',existing)
 for method,callback in [('InitUI',actor.init_ui),('RenameCanvas',actor.rename_canvas),('RenameButton',actor.rename_button),('CloneOwner',actor.clone_owner)]:
  callback();copy=actor.get_editor_property('Copy');panel=actor.get_editor_property('Panel');button=actor.get_editor_property('Action')
  rows.append({'existing':existing,'stage':method,'owner':get_name(owner),'copy':get_name(copy) if copy else '', 'panelOwner':owner_of(panel)==owner,'buttonOwner':owner_of(button)==owner})
with open(os.environ['EM_UI_NAME_OUTPUT'],'w',encoding='utf-8') as stream:json.dump({'status':'CAPTURED','rows':rows},stream,ensure_ascii=False,indent=2)
print('ACTUAL_UI_NAME_NATIVE_CAPTURED')
