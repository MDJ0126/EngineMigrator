"""Run inside Unreal: reject an unsaved prototype mass edit, then restore it."""
import json,os,sys,unreal
project=unreal.Paths.project_dir()
with open(os.path.join(project,'EMPhysicsGuardJob.json'),encoding='utf-8-sig') as stream:job=json.load(stream)
sys.path.insert(0,job['adapters']);import runtime_prototype_guard as guard
world=unreal.EditorLoadingAndSavingUtils.load_map(job['map']);assert world
subsystem=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
guard.verify(world,subsystem,required=True)
actor=next(a for a in subsystem.get_all_level_actors() if 'EMRuntimeTemplate' in map(str,a.tags) and 'EMRuntimePhysicsDynamic' in map(str,a.tags) and any(c.get_name()=='EM_Physics' for c in a.get_components_by_class(unreal.ShapeComponent)))
shape=next(c for c in actor.get_components_by_class(unreal.ShapeComponent) if c.get_name()=='EM_Physics')
before=json.loads(unreal.EMBlueprintLibrary.read_physics(actor))['mass']
shape.set_mass_override_in_kg(unreal.Name('None'),before+1,True)
assert json.loads(unreal.EMBlueprintLibrary.read_physics(actor))['mass']==before+1
try:guard.verify(world,subsystem,required=True)
except RuntimeError as error:assert 'state changed independently' in str(error)
else:raise AssertionError('Unsynchronized prototype physics edit was accepted')
shape.set_mass_override_in_kg(unreal.Name('None'),before,True)
guard.verify(world,subsystem,required=True)
with open(job['output'],'w',encoding='utf-8') as stream:json.dump({'status':'PASS','actualMassChanged':True,'unsynchronizedPhysicsEditRejected':True,'restoredInMemory':True,'mapSaved':False},stream,indent=2)
unreal.log('RUNTIME_PHYSICS_PROTOTYPE_GUARD_PASS')
