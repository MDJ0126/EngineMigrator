import json,os,sys
import unreal
config=json.load(open(os.path.join(unreal.Paths.project_dir(),"RuntimePolicyTest.json"),encoding="utf-8-sig"))
sys.path.insert(0,config["adapters"])
import runtime_policy
world=unreal.EditorLoadingAndSavingUtils.load_map(config["map"])
created=runtime_policy.apply(world,config["base"])
if not unreal.EditorLoadingAndSavingUtils.save_map(world,config["map"]):raise RuntimeError("Runtime policy map save failed")
world=unreal.EditorLoadingAndSavingUtils.load_map(config["map"])
actual=runtime_policy.read(world)
if actual!=created:raise RuntimeError("Runtime policy changed on reload")
json.dump({"status":"PASS","actualMapReloaded":True,"runtimePolicy":actual},open(config["output"],"w",encoding="utf-8"),indent=2)
