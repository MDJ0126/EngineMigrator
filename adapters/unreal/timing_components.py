"""Persist Unity project timing as world settings and runtime bootstrap input."""
import math,json
import unreal
PREFIX="EMTimeScale:"
def validate(package):
    if not package.get("hasTiming"):return None
    raw=package["timing"]
    values={key:float(raw[key]) for key in ("timeScale","fixedDeltaTime","maximumDeltaTime")}
    if any(not math.isfinite(v) for v in values.values()) or values["timeScale"]<0 or values["fixedDeltaTime"]<=0 or values["maximumDeltaTime"]<=0 or round(values["fixedDeltaTime"]*141120000)<1:raise RuntimeError("Invalid Unity project timing")
    return values

def apply_world(world,package):
    values=validate(package)
    if values is None:return
    settings=world.get_world_settings()
    settings.set_editor_property("MinUndilatedFrameTime",0.0)
    settings.set_editor_property("MaxUndilatedFrameTime",values["maximumDeltaTime"])
    settings.set_editor_property("MinGlobalTimeDilation",0.0)
    settings.set_editor_property("MaxGlobalTimeDilation",max(float(settings.get_editor_property("MaxGlobalTimeDilation")),values["timeScale"]))
    settings.tags=[v for v in settings.tags if not str(v).startswith(PREFIX)]+[unreal.Name(PREFIX+format(values["timeScale"],".9g"))]
    unreal.EditorAssetLibrary.set_metadata_tag(world,"EngineMigratorTiming",json.dumps(package["timing"]))

def verify_world(world,package):
    values=validate(package)
    if values is None:return
    settings=world.get_world_settings();tags=[str(v) for v in settings.tags if str(v).startswith(PREFIX)]
    if len(tags)!=1 or abs(float(tags[0][len(PREFIX):])-values["timeScale"])>1e-6:raise RuntimeError("Initial time scale failed map reload")
    if float(settings.get_editor_property("MinGlobalTimeDilation"))!=0 or abs(float(settings.get_editor_property("MaxUndilatedFrameTime"))-values["maximumDeltaTime"])>1e-6:raise RuntimeError("Project timing bounds failed map reload")

def read_world(world):
    text=unreal.EditorAssetLibrary.get_metadata_tag(world,"EngineMigratorTiming")
    if not text:return None
    data=json.loads(text);settings=world.get_world_settings()
    tags=[str(v) for v in settings.tags if str(v).startswith(PREFIX)]
    if len(tags)>1:raise RuntimeError("Ambiguous initial time scale")
    if tags:data["timeScale"]=float(tags[0][len(PREFIX):])
    data["maximumDeltaTime"]=float(settings.get_editor_property("MaxUndilatedFrameTime"))
    validate({"hasTiming":True,"timing":data})
    return data
