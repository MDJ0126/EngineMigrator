"""Native camera/light/post-process mappings. Unmatched renderer details are explicit warnings."""
import copy, json, math
import unreal

NODE_KEY="EngineMigratorSceneNode"
ENV_KEY="EngineMigratorEnvironment"

def is_environment(actor):
    return unreal.EditorAssetLibrary.get_metadata_tag(actor,ENV_KEY)=="helper"

def actor_class(node):
    if node.get("hasPhysics"): return unreal.Actor
    if node.get("hasCamera"): return unreal.CameraActor
    if node.get("hasLight"): return unreal.DirectionalLight
    if node.get("hasVolume"): return unreal.PostProcessVolume
    return unreal.StaticMeshActor

def mesh_component(actor):
    return actor.static_mesh_component if isinstance(actor,unreal.StaticMeshActor) else actor.get_component_by_class(unreal.StaticMeshComponent) if actor.get_class()==unreal.Actor.static_class() else None

def camera_component(actor):
    return actor.get_editor_property("camera_component")

def project_near_clip():
    # Engine's non-Python-wrapped property keeps its native reflected spelling.
    return unreal.get_default_object(unreal.load_class(None,"/Script/Engine.Engine")).get_editor_property("NearClipPlane")

def light_component(actor):
    return actor.get_editor_property("light_component")

def effect_parameter(effect,name):
    return next((p for p in effect.get("parameters",[]) if p["name"]==name),None)

def post_process_values(volume):
    values={}
    for effect in volume.get("effects",[]):
        short=effect["type"].split(".")[-1]
        if short not in ("Bloom","Vignette","Tonemapping","MotionBlur"): raise RuntimeError("Unsupported volume effect "+short)
        if short=="Tonemapping": continue
        for name,target in (("intensity",{"Bloom":"bloom_intensity","Vignette":"vignette_intensity","MotionBlur":"motion_blur_amount"}[short]),):
            parameter=effect_parameter(effect,name)
            if parameter and parameter["overrideState"]: values[target]=float(parameter["value"]) if effect["active"] else 0.0
        if short=="Bloom":
            parameter=effect_parameter(effect,"threshold")
            if parameter and parameter["overrideState"]: values["bloom_threshold"]=float(parameter["value"])
    return values

def apply(actor,node):
    unreal.EditorAssetLibrary.set_metadata_tag(actor,NODE_KEY,json.dumps(node,separators=(",",":")))
    if node.get("hasCamera"):
        source=node["camera"]; component=camera_component(actor)
        if source["aspect"]<=0 or not 0<source["fov"]<180: raise RuntimeError("Invalid camera projection")
        component.set_editor_property("aspect_ratio",source["aspect"])
        component.set_editor_property("constrain_aspect_ratio",True)
        component.set_editor_property("projection_mode",unreal.CameraProjectionMode.ORTHOGRAPHIC if source["orthographic"] else unreal.CameraProjectionMode.PERSPECTIVE)
        component.set_editor_property("field_of_view",math.degrees(2*math.atan(math.tan(math.radians(source["fov"])/2)*source["aspect"])))
        component.set_editor_property("ortho_width",source["size"]*2*source["aspect"]*100)
        component.set_editor_property("auto_calculate_ortho_planes",False)
        component.set_editor_property("ortho_near_clip_plane",source["near"]*100)
        component.set_editor_property("ortho_far_clip_plane",source["far"]*100)
        if node.get("tag")=="MainCamera" and source["enabled"] and node.get("effectiveActive",node["active"]): actor.set_editor_property("auto_activate_for_player",unreal.AutoReceiveInput.PLAYER0)
    if node.get("hasLight"):
        source=node["light"];component=light_component(actor)
        if source["type"]!="Directional": raise RuntimeError("Unsupported light type")
        component.set_intensity(source["intensity"])
        color=source["color"];component.set_light_color(unreal.LinearColor(color["r"],color["g"],color["b"],color["a"]),False)
        component.set_temperature(source["temperature"]);component.set_use_temperature(source["useTemperature"])
        component.set_cast_shadows(source["shadows"]!="None")
        component.set_indirect_lighting_intensity(source["bounceIntensity"])
        component.set_editor_property("light_source_angle",0.0 if source["shadows"]=="Hard" else 0.5357)
        component.set_editor_property("atmosphere_sun_light",True)
        component.set_visibility(source["enabled"] and node.get("effectiveActive",node["active"]),True)
    if node.get("hasVolume"):
        source=node["volume"]
        if not source["global"]: raise RuntimeError("Local volume bounds are unsupported")
        actor.set_editor_property("unbound",True);actor.set_editor_property("priority",source["priority"])
        actor.set_editor_property("blend_weight",source["weight"]);actor.set_editor_property("blend_radius",source["blendDistance"]*100)
        actor.set_editor_property("enabled",source["enabled"] and node.get("effectiveActive",node["active"]))
        settings=actor.get_editor_property("settings")
        for prop,value in post_process_values(source).items():settings.set_editor_property("override_"+prop,True);settings.set_editor_property(prop,value)
        actor.set_editor_property("settings",settings)

def verify(actor,node):
    stored=unreal.EditorAssetLibrary.get_metadata_tag(actor,NODE_KEY)
    if not stored or json.loads(stored)!=node: raise RuntimeError("Scene component metadata did not survive map reload")
    if node.get("hasCamera"):
        source=node["camera"];component=camera_component(actor)
        expected=math.degrees(2*math.atan(math.tan(math.radians(source["fov"])/2)*source["aspect"]))
        if abs(component.get_editor_property("field_of_view")-expected)>0.0001 or abs(component.get_editor_property("aspect_ratio")-source["aspect"])>0.0001: raise RuntimeError("Camera FOV/aspect failed native reload")
        if (component.get_editor_property("projection_mode")==unreal.CameraProjectionMode.ORTHOGRAPHIC)!=source["orthographic"]: raise RuntimeError("Camera projection reload mismatch")
        if not source["orthographic"] and abs(project_near_clip()-source["near"]*100)>0.001: raise RuntimeError("Native project near clip does not match source meters-to-centimeters")
    if node.get("hasLight"):
        source=node["light"];component=light_component(actor)
        if abs(component.get_editor_property("intensity")-source["intensity"])>0.0001 or abs(component.get_editor_property("temperature")-source["temperature"])>0.001 or component.get_editor_property("use_temperature")!=source["useTemperature"]: raise RuntimeError("Native light properties failed reload")
    if node.get("hasVolume"):
        source=node["volume"];settings=actor.get_editor_property("settings")
        if not actor.get_editor_property("unbound") or abs(actor.get_editor_property("blend_weight")-source["weight"])>0.0001: raise RuntimeError("Post-process volume properties failed reload")
        for prop,value in post_process_values(source).items():
            if not settings.get_editor_property("override_"+prop) or abs(settings.get_editor_property(prop)-value)>0.0001: raise RuntimeError("Native post-process mapping failed: "+prop)

def import_environment(package,world,subsystem):
    if not package.get("hasEnvironment"): return []
    environment=package["environment"]
    helpers=[]
    unreal.EditorAssetLibrary.set_metadata_tag(world,ENV_KEY,json.dumps(environment,separators=(",",":")))
    if environment.get("skyShader"):
        sky=subsystem.spawn_actor_from_class(unreal.SkyAtmosphere,unreal.Vector(0,0,0));sky.set_actor_label("EngineMigrator Sky Atmosphere")
        unreal.EditorAssetLibrary.set_metadata_tag(sky,ENV_KEY,"helper")
        helpers.append(sky)
    skylight=subsystem.spawn_actor_from_class(unreal.SkyLight,unreal.Vector(0,0,0));skylight.set_actor_label("EngineMigrator Environment Light")
    unreal.EditorAssetLibrary.set_metadata_tag(skylight,ENV_KEY,"helper")
    component=skylight.get_editor_property("light_component");component.set_intensity(environment["ambientIntensity"])
    component.set_editor_property("real_time_capture",True)
    helpers.append(skylight)
    return helpers

def export_environment(world,actors):
    data=unreal.EditorAssetLibrary.get_metadata_tag(world,ENV_KEY)
    if not data:return None
    environment=json.loads(data)
    for actor in actors:
        if isinstance(actor,unreal.SkyLight) and is_environment(actor):environment["ambientIntensity"]=actor.get_editor_property("light_component").get_editor_property("intensity")
    return environment

def export_node(actor):
    metadata=unreal.EditorAssetLibrary.get_metadata_tag(actor,NODE_KEY)
    source=json.loads(metadata) if metadata else {}
    flags={"hasCamera":isinstance(actor,unreal.CameraActor),"hasLight":isinstance(actor,unreal.DirectionalLight),"hasVolume":isinstance(actor,unreal.PostProcessVolume)}
    result={**flags,"tag":source.get("tag","MainCamera" if flags["hasCamera"] else "Untagged"),"layer":source.get("layer",0)}
    if flags["hasCamera"]:
        component=camera_component(actor);original=copy.deepcopy(source.get("camera",{}))
        if not original: raise RuntimeError("Unmapped native camera clip/clear/audio policy requires explicit configuration")
        aspect=component.get_editor_property("aspect_ratio");fov=component.get_editor_property("field_of_view")
        original.update(aspect=aspect,fov=math.degrees(2*math.atan(math.tan(math.radians(fov)/2)/aspect)),orthographic=component.get_editor_property("projection_mode")==unreal.CameraProjectionMode.ORTHOGRAPHIC,size=component.get_editor_property("ortho_width")/aspect/200)
        if original["orthographic"]:original.update(near=component.get_editor_property("ortho_near_clip_plane")/100,far=component.get_editor_property("ortho_far_clip_plane")/100)
        else:original["near"]=project_near_clip()/100
        result["camera"]=original
    if flags["hasLight"]:
        component=light_component(actor);original=copy.deepcopy(source.get("light",{}))
        if not original: raise RuntimeError("Unmapped native light unit policy requires explicit configuration")
        color=component.get_light_color()
        original.update(intensity=component.get_editor_property("intensity"),temperature=component.get_editor_property("temperature"),useTemperature=component.get_editor_property("use_temperature"),bounceIntensity=component.get_editor_property("indirect_lighting_intensity"),color={"r":color.r,"g":color.g,"b":color.b,"a":color.a})
        result["light"]=original
    if flags["hasVolume"]:
        original=copy.deepcopy(source.get("volume",{}))
        if not original: raise RuntimeError("Unmapped native post-process graph requires explicit configuration")
        original.update(global_=actor.get_editor_property("unbound"))
        original["global"]=original.pop("global_");original.update(priority=actor.get_editor_property("priority"),weight=actor.get_editor_property("blend_weight"),blendDistance=actor.get_editor_property("blend_radius")/100)
        settings=actor.get_editor_property("settings")
        for effect in original["effects"]:
            short=effect["type"].split(".")[-1]
            if short in ("Bloom","Vignette","MotionBlur"):
                prop={"Bloom":"bloom_intensity","Vignette":"vignette_intensity","MotionBlur":"motion_blur_amount"}[short]
                parameter=effect_parameter(effect,"intensity")
                if parameter and effect["active"] and parameter["overrideState"]:parameter["value"]=format(settings.get_editor_property(prop),".9g")
                if short=="Bloom":
                    parameter=effect_parameter(effect,"threshold")
                    if parameter and parameter["overrideState"]:parameter["value"]=format(settings.get_editor_property("bloom_threshold"),".9g")
        result["volume"]=original
    result["active"]=source.get("active",not actor.get_editor_property("hidden")) if source and source.get("effectiveActive",source.get("active",True))==not_hidden(actor) else not_hidden(actor)
    result["effectiveActive"]=not_hidden(actor)
    return result

def not_hidden(actor): return not actor.get_editor_property("hidden")
