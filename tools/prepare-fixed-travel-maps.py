"""Create actual private Unreal maps; executed only through the common audited runner."""
import unreal,os,json,math,struct
sub=unreal.get_editor_subsystem(unreal.EditorActorSubsystem);cls=unreal.load_class(None,'/Script/EMActorPort.TravelFixed');assert cls
requested=struct.unpack("f",struct.pack("f",float(os.environ.get("EM_FIXED_TRAVEL_MAP_STEP","0.02"))))[0]
ticks=math.floor(requested*141120000);actual=struct.unpack("f",struct.pack("f",ticks/141120000))[0]
for stage,name in enumerate(('FixedStart','FixedMiddle','FixedFinal')):
 world=unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
 if not (stage==1 and os.environ.get("EM_FIXED_TRAVEL_MAP_EMPTY")=="1"):
  actor=sub.spawn_actor_from_class(cls,unreal.Vector(),unreal.Rotator())
  actor.set_editor_property('Stage',stage)
  actor.tags=[unreal.Name('EMScene:FixedMiddle=/Game/FixedTravel/FixedMiddle'),unreal.Name('EMScene:FixedFinal=/Game/FixedTravel/FixedFinal'),unreal.Name('EMFixedStep:'+format(actual,'.9g')),unreal.Name('EMFixedTicks:'+str(ticks))]
 path='/Game/FixedTravel/'+name
 assert unreal.EditorLoadingAndSavingUtils.save_map(world,path)
 unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True,True)
assert unreal.EditorLoadingAndSavingUtils.load_map('/Game/FixedTravel/FixedStart')
print('PRIVATE_FIXED_TRAVEL_MAPS_SAVED')
