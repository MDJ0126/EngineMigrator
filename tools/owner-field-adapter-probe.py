"""Run inside the audited Unreal editor against the existing private project."""
import sys,json,os
import unreal
root=r'C:\Projects\EngineMigrator'
sys.path.insert(0,os.path.join(root,'adapters','unreal'))
import script_components as components
import runtime_prototype_guard as guard
subsystem=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
owner=subsystem.spawn_actor_from_class(unreal.StaticMeshActor,unreal.Vector(0,0,0),unreal.Rotator(0,0,0))
owner.tags=[unreal.Name('OwnerNode')]
node={'id':'OwnerNode','active':True,'behaviours':[{'id':'OwnerBehaviour','className':'AuditOwnerCollision','enabled':True,'fields':[{'name':'Owner','kind':'node','value':'OwnerNode'}]}]}
package={'nodes':[node],'scripts':[]}
actors=components.import_all(package,{'OwnerNode':owner},subsystem,'/Game/OwnerFieldProbe')
actor=actors[0][0]
assert actor.get_editor_property('EM_Field_Owner')==owner
assert actor.get_owner() is None,'User field must not mutate AActor.Owner'
bindings=components.verify_all(package,{'OwnerNode':owner},[actor])
world=unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
components.persist(package,world,actors)
unreal.EditorAssetLibrary.set_metadata_tag(owner,'EngineMigratorSceneNode',json.dumps(node))
map_path='/Game/OwnerFieldProbe/Mapping'
assert unreal.EditorLoadingAndSavingUtils.save_map(world,map_path)
unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True,True)
world=unreal.EditorLoadingAndSavingUtils.load_map(map_path)
assert world is not None
reloaded=subsystem.get_all_level_actors()
owner=next(a for a in reloaded if a.tags and str(a.tags[0])=='OwnerNode')
actor=next(a for a in reloaded if components.is_behaviour(a))
assert actor.get_editor_property('EM_Field_Owner')==owner
assert actor.get_owner() is None
bindings=components.verify_all(package,{'OwnerNode':owner},reloaded)
exported={'nodes':[{'id':'OwnerNode'}]}
components.export_all(world,[owner,actor],{owner:'OwnerNode'},exported)
field=exported['nodes'][0]['behaviours'][0]['fields'][0]
assert field=={'name':'Owner','kind':'node','value':'OwnerNode'},field
snapshot=guard.snapshot(subsystem,definition=True)
row=next(r for r in snapshot if r['actor']==actor.get_path_name())
assert row['fields']['Owner']==owner.get_path_name()
result={'status':'PASS','sourceFieldPreserved':True,'nativeOwnerUnchanged':True,'sceneReferenceReloadChecked':True,'exportSourceReferencePreserved':True,'prototypeSnapshotMapped':True,'bindings':bindings}
with open(os.path.join(root,'artifacts','owner-field-check','adapter-verification.json'),'w',encoding='utf-8') as stream:json.dump(result,stream,indent=2)
print('OWNER_FIELD_ACTUAL_ADAPTER_PASS')
