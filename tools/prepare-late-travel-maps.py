"""Create private scaled-runtime fixtures using the audited commandlet runner."""
import unreal,sys,os,json
from pathlib import Path
root=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(root/"adapters/unreal"));import timing_components as timing
initial=float(os.environ.get("EM_LATE_TRAVEL_INITIAL","1"));package={"hasTiming":True,"timing":{"timeScale":initial,"fixedDeltaTime":.02,"maximumDeltaTime":.33333334,"unitySettings":(root/"artifacts/runtime-ui-api-tests/UnitySource/ProjectSettings/TimeManager.asset").read_text()}}
sub=unreal.get_editor_subsystem(unreal.EditorActorSubsystem);cls=unreal.load_class(None,'/Script/EMActorPort.LateTravel');assert cls
for stage,name in enumerate(('LateStart','LateMiddle','LateFinal')):
 world=unreal.EditorLoadingAndSavingUtils.new_blank_map(False);timing.apply_world(world,package);actor=sub.spawn_actor_from_class(cls,unreal.Vector(),unreal.Rotator());actor.set_editor_property('Stage',stage)
 actor.tags=[unreal.Name('EMScene:LateMiddle=/Game/LateTravel/LateMiddle'),unreal.Name('EMScene:LateFinal=/Game/LateTravel/LateFinal'),unreal.Name('EMFixedStep:0.0199999921'),unreal.Name('EMFixedTicks:2822399'),unreal.Name('EMScriptOrder:-100'),unreal.Name('EMScriptClassOrder:LateTravelWorker=0')]
 assert unreal.EditorLoadingAndSavingUtils.save_map(world,'/Game/LateTravel/'+name)
 unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True,True)
world=unreal.EditorLoadingAndSavingUtils.new_blank_map(False);timing.apply_world(world,package);assert unreal.EditorLoadingAndSavingUtils.save_map(world,'/Game/LateTravel/LateEmpty')
assert unreal.EditorLoadingAndSavingUtils.load_map('/Game/LateTravel/LateStart')
for name in ('LateStart','LateMiddle','LateFinal','LateEmpty'):
 world=unreal.EditorLoadingAndSavingUtils.load_map('/Game/LateTravel/'+name);timing.verify_world(world,package)
exported=timing.read_world(world);assert exported["timeScale"]==initial
(root/'artifacts/late-travel-check'/('exported-timing-'+str(initial)+'.json')).write_text(json.dumps(exported))
class Untouched:
 def get_world_settings(self):raise AssertionError("Invalid timing mutated settings before validation")
invalids=[("timeScale",-1),("timeScale",float("nan")),("timeScale",float("inf")),("fixedDeltaTime",0),("fixedDeltaTime",1e-20),("maximumDeltaTime",0)]
for key,value in invalids:
 bad={"hasTiming":True,"timing":dict(package["timing"])};bad["timing"][key]=value
 try:timing.apply_world(Untouched(),bad)
 except RuntimeError:pass
 else:raise AssertionError("Invalid timing accepted")
(root/'artifacts/late-travel-check'/('timing-map-'+str(initial)+'.json')).write_text(json.dumps({'status':'PASS','initialTimeScale':initial,'mapsReloaded':4,'invalidInputsRejectedBeforeSettingsMutation':6,'actualExportTimeScale':exported['timeScale']}))
print('PRIVATE_LATE_TRAVEL_MAPS_SAVED')
