"""Run with UnrealEditor-Cmd on a private imported fixture. Never saves edits."""
import json,os,sys
import unreal
sys.path.insert(0,os.path.abspath(os.path.join(os.path.dirname(__file__),"../adapters/unreal")))
import runtime_prototype_guard as guard
project=unreal.Paths.project_dir()
with open(os.path.join(project,".EngineMigratorProject.json"),encoding="utf-8-sig") as stream:manifest=json.load(stream)
subsystem=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
checked=[]
for kind in ("scene","prefab"):
    found=False
    for unit in manifest["units"]:
        if unit["kind"]!=kind:continue
        world=unreal.EditorLoadingAndSavingUtils.load_map(unit["map"])
        raw=unreal.EditorAssetLibrary.get_metadata_tag(world,guard.KEY)
        if not raw or not json.loads(raw)["actors"]:continue
        guard.verify(world,subsystem)
        candidates=[a for a in subsystem.get_all_level_actors() if unreal.EditorAssetLibrary.get_metadata_tag(a,"EngineMigratorSceneNode") and (kind=="prefab" or "EMRuntimeTemplate" in [str(t) for t in a.tags])]
        actor=candidates[0];original=actor.get_actor_location()
        actor.set_actor_location(unreal.Vector(original.x+37,original.y,original.z),False,True)
        try:
            try:guard.verify(world,subsystem)
            except RuntimeError as error:
                if "changed independently" not in str(error):raise
            else:raise RuntimeError("Edited runtime definition was silently accepted")
        finally:actor.set_actor_location(original,False,True)
        guard.verify(world,subsystem);checked.append(kind);found=True;break
    if not found:raise RuntimeError("No guarded test map: "+kind)
with open(os.path.join(project,"runtime-prototype-guard-verification.json"),"w",encoding="utf-8") as stream:json.dump({"status":"PASS","actualEditorEditsRejected":checked,"editsSaved":False},stream,indent=2)
unreal.log("RUNTIME_PROTOTYPE_GUARD_NEGATIVE_TEST_PASS")
