import json, os, unreal
with open(os.path.join(unreal.Paths.project_dir(),"BlueprintRun.json"),encoding="utf-8-sig") as stream:
    config=json.load(stream)
result=unreal.EMBlueprintLibrary.run(config["input"],config["output"])
if result.startswith("ERROR:"):
    raise RuntimeError(result)
unreal.log("ENGINE_MIGRATOR_BLUEPRINT_EXECUTION_VERIFIED")
