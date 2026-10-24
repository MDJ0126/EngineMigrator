"""Primitive 3D physics backed by real, saved UShapeComponents, never metadata-only."""
import copy,json,math
import unreal
import project_assets as shared

def library():
    if not hasattr(unreal,"EMBlueprintLibrary") or not hasattr(unreal.EMBlueprintLibrary,"apply_physics"):
        raise RuntimeError("Physics requires the current engine-build-specific EMBlueprintBridge")
    return unreal.EMBlueprintLibrary

def apply(actor,node,base):
    if not node.get("hasPhysics"):return
    data=copy.deepcopy(node["physics"])
    if data.get("customMaterial"):data["materialAsset"]=(shared.root if shared.enabled else base)+"/PhysicsMaterials/"+data["materialId"]
    result=library().apply_physics(actor,json.dumps(data))
    if result!="PHYSICS_APPLIED":raise RuntimeError(result)

def export(actor,node):
    original=node.get("physics") if node.get("hasPhysics") else None
    shapes=[c for c in actor.get_components_by_class(unreal.ShapeComponent) if c.get_name()=="EM_Physics"]
    if not shapes:
        if original:raise RuntimeError("Ported collision component was removed")
        return {"hasPhysics":False}
    if not original:raise RuntimeError("Unmapped native physics policy requires explicit conversion")
    actual=json.loads(library().read_physics(actor));result=copy.deepcopy(original)
    fields=["shape","center","size","radius","height","direction","enabled","trigger"]
    if original["hasBody"]:fields += ["kinematic","gravity","mass","linearDamping","angularDamping","constraints","detection"]
    fields += ["dynamicFriction","staticFriction","bounciness","frictionCombine","bounceCombine"]
    result["hasMaterialMapping"]=True
    for field in fields:result[field]=actual[field]
    return {"hasPhysics":True,"physics":result}

def verify(actor,node):
    if not node.get("hasPhysics"):return
    actual=export(actor,node)["physics"]
    def same(a,b):
        if isinstance(a,dict):return all(k in b and same(v,b[k]) for k,v in a.items())
        if isinstance(a,(float,int)) and not isinstance(a,bool):return math.isclose(a,b,abs_tol=1e-5,rel_tol=1e-6)
        return a==b
    if not same(node["physics"],actual):raise RuntimeError("Physics property mismatch after reload: "+json.dumps(actual))

def apply_world(world,package):
    gravity=package.get("gravity",{"x":0,"y":-9.81,"z":0})
    if gravity["x"]!=0 or gravity["z"]!=0:raise RuntimeError("Non-vertical gravity requires a force-field runtime")
    settings=world.get_world_settings();settings.set_editor_property("GlobalGravityZ",gravity["y"]*100);settings.set_editor_property("bGlobalGravitySet",True)

def export_world(world):
    settings=world.get_world_settings()
    value=settings.get_editor_property("global_gravity_z") if settings.get_editor_property("bGlobalGravitySet") else unreal.get_default_object(unreal.PhysicsSettings).get_editor_property("default_gravity_z")
    return {"x":0,"y":value/100,"z":0}

def structural_component(actor,component):
    if component.get_editor_property("bIsEditorOnly"):return True
    if component.get_class()!=unreal.SceneComponent.static_class() or actor.get_class()!=unreal.Actor.static_class():return False
    if component==actor.root_component:return True # actor world transform is already exported as the node transform
    p=component.get_editor_property("relative_location");r=component.get_editor_property("relative_rotation");s=component.get_editor_property("relative_scale3d")
    return max(abs(p.x),abs(p.y),abs(p.z),abs(r.roll),abs(r.pitch),abs(r.yaw),abs(s.x-1),abs(s.y-1),abs(s.z-1))<1e-6
