import json, os, unreal
with open(os.path.join(unreal.Paths.project_dir(),"SceneVerify.json"),encoding="utf-8-sig") as stream:config=json.load(stream)
if not unreal.EditorLoadingAndSavingUtils.load_map(config["map"]):raise RuntimeError("Saved scene unavailable")
if config.get("actorNames"):
    for actor in unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors():
        if actor.get_actor_label() in config["actorNames"]:actor.tags=[unreal.Name(config["actorNames"][actor.get_actor_label()])]
result=unreal.EMBlueprintLibrary.run_scene(config["input"],config["output"])
if result.startswith("ERROR:"):raise RuntimeError(result)
unreal.log("ENGINE_MIGRATOR_SAVED_SCENE_EXECUTED")
