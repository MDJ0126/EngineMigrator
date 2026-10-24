"""A Unity scene must not acquire Unreal's implicit colliding DefaultPawn."""
import json
import unreal

def apply(world,base):
    if not hasattr(unreal.EMBlueprintLibrary,"runtime_policy"):raise RuntimeError("Current native bridge with runtime policy support is required")
    result=unreal.EMBlueprintLibrary.runtime_policy(world,json.dumps({"mode":"create","asset":base+"/Runtime/EM_NoImplicitPawn"}))
    if result.startswith("ERROR:"):raise RuntimeError(result)
    return json.loads(result)

def read(world):
    if not hasattr(unreal.EMBlueprintLibrary,"runtime_policy"):raise RuntimeError("Current native runtime policy verifier is required")
    result=unreal.EMBlueprintLibrary.runtime_policy(world,json.dumps({"mode":"read"}))
    if result.startswith("ERROR:"):raise RuntimeError(result)
    return json.loads(result)
