"""Unreal-side edit detection for an actual saved transform-animation prototype."""
import json,os,sys,unreal
with open(os.path.join(unreal.Paths.project_dir(),'EMAnimationGuardJob.json'),encoding='utf-8-sig') as stream:job=json.load(stream)
sys.path.insert(0,job['adapters']);import runtime_prototype_guard as guard
world=unreal.EditorLoadingAndSavingUtils.load_map(job['map']);assert world
subsystem=unreal.get_editor_subsystem(unreal.EditorActorSubsystem);guard.verify(world,subsystem,required=True)
actor=next(a for a in subsystem.get_all_level_actors() if 'EMRuntimeTemplate' in map(str,a.tags) and unreal.EditorAssetLibrary.get_metadata_tag(a,'EngineMigratorAnimation'))
sequence=actor.get_sequence();key=next(k for b in sequence.get_bindings() for track in b.get_tracks() for section in track.get_sections() for channel in section.get_all_channels() for k in channel.get_keys())
before=key.get_value();key.set_value(before+.125);assert abs(key.get_value()-before-.125)<1e-6
try:guard.verify(world,subsystem,required=True)
except RuntimeError as error:assert 'state changed independently' in str(error)
else:raise AssertionError('Unsynchronized animation key edit was accepted')
key.set_value(before);guard.verify(world,subsystem,required=True)
with open(job['output'],'w',encoding='utf-8') as stream:json.dump({'status':'PASS','actualAnimationKeyChanged':True,'unsynchronizedAnimationEditRejected':True,'restoredInMemory':True,'mapSaved':False,'assetSaved':False,'sequence':sequence.get_path_name()},stream,indent=2)
unreal.log('RUNTIME_ANIMATION_PROTOTYPE_GUARD_PASS')
