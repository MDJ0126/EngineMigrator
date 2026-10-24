import json, os, unreal
with open(os.path.join(unreal.Paths.project_dir(),"ActorRun.json"),encoding="utf-8-sig") as stream:
    config=json.load(stream)
result=unreal.EMBlueprintLibrary.run_actors(config["input"],config["output"])
if result.startswith("ERROR:"):
    raise RuntimeError(result)
unreal.log("ENGINE_MIGRATOR_ACTOR_EVENTS_EXECUTED")
