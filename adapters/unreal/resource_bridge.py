"""Native Unreal editor asset/level importer. Requires PythonScriptPlugin and EditorScriptingUtilities."""
import json, os, math, re, sys
import unreal
sys.path.insert(0,os.path.dirname(__file__))
import scene_components as scene
import script_components as scripts
import physics_components as physics
import animation_components as animations
import ui_components as ui
import runtime_policy as policy
import project_assets as shared
import timing_components as timing

def load_json(path):
    with open(path, encoding="utf-8-sig") as stream:
        return json.load(stream)

config = load_json(os.path.join(unreal.Paths.project_dir(), "EngineMigratorRun.json"))
root = os.path.abspath(config["package"])
package = load_json(os.path.join(root, "package.json"))
if package["schema"] != 1 or package.get("blockers"):
    raise RuntimeError("Unsupported or blocked package")
timing.validate(package)
for entry in package["textures"] + package["materials"] + package["meshes"] + package["nodes"] + [{"id":package["scene"]}]:
    if not re.fullmatch(r"[A-Za-z0-9_]+", entry["id"]): raise RuntimeError("Unsafe package identifier")
base = config.get("assetRoot", "/Game/EngineMigrator")
if not base.startswith("/Game/") or ".." in base:
    raise RuntimeError("Invalid target asset root")
if unreal.EditorAssetLibrary.does_directory_exist(base):
    raise RuntimeError("Target import directory already exists; refusing overwrite")
tools = unreal.AssetToolsHelpers.get_asset_tools()
textures, materials, meshes, actors, bindings = {}, {}, {}, {}, []

def file_path(relative):
    full = os.path.realpath(os.path.join(root, relative))
    if os.path.commonpath([full, root]) != root:
        raise RuntimeError("Package file escapes its root")
    return full

def imported(task):
    tools.import_asset_tasks([task])
    results = task.get_objects()
    if len(results) != 1:
        raise RuntimeError("Expected one imported asset: " + str(task.filename))
    return results[0]

def record(source_id, obj, kind, links=None, source_name=None):
    unreal.EditorAssetLibrary.set_metadata_tag(obj, "EngineMigratorSourceId", source_id)
    if source_name is not None:unreal.EditorAssetLibrary.set_metadata_tag(obj,"EngineMigratorSourceName",source_name)
    bindings.append({"sourceId": source_id, "target": obj.get_path_name(), "kind": kind, "references": links or []})

for source in package["textures"]:
    if shared.enabled and source["id"] in shared.textures:
        texture=shared.textures[source["id"]];textures[source["id"]]=texture;record(source["id"],texture,"Texture2D",source_name=source["name"]);continue
    task = unreal.AssetImportTask()
    task.filename = file_path(source["file"])
    task.destination_path = (shared.root if shared.enabled else base) + "/Textures"
    task.destination_name = "T_" + source["id"]
    task.automated = True
    task.save = True
    texture = imported(task)
    if not isinstance(texture, unreal.Texture2D):
        raise RuntimeError("Imported texture has wrong type")
    texture.set_editor_property("srgb", source["srgb"])
    texture.set_editor_property("filter", {"Point":unreal.TextureFilter.TF_NEAREST,"Bilinear":unreal.TextureFilter.TF_BILINEAR,"Trilinear":unreal.TextureFilter.TF_TRILINEAR}[source.get("filter","Bilinear")])
    address = {"Repeat":unreal.TextureAddress.TA_WRAP,"Clamp":unreal.TextureAddress.TA_CLAMP,"Mirror":unreal.TextureAddress.TA_MIRROR}
    texture.set_editor_property("address_x", address[source.get("wrapU","Repeat")])
    texture.set_editor_property("address_y", address[source.get("wrapV","Repeat")])
    texture.set_editor_property("mip_gen_settings", unreal.TextureMipGenSettings.TMGS_FROM_TEXTURE_GROUP if source.get("mipmaps",False) else unreal.TextureMipGenSettings.TMGS_NO_MIPMAPS)
    unreal.EditorAssetLibrary.save_loaded_asset(texture)
    textures[source["id"]] = texture
    if shared.enabled:shared.textures[source["id"]]=texture
    record(source["id"], texture, "Texture2D",source_name=source["name"])
    unreal.EditorAssetLibrary.save_loaded_asset(texture)

for source in package["materials"]:
    if shared.enabled and source["id"] in shared.materials:
        mat=shared.materials[source["id"]];materials[source["id"]]=mat;record(source["id"],mat,"Material",[source["texture"]] if source.get("texture") else [],source_name=source["name"]);continue
    if source["shader"] not in ("unlit","standard"):
        raise RuntimeError("Unsupported shader: " + source["shader"])
    mat = tools.create_asset("M_" + source["id"], (shared.root if shared.enabled else base) + "/Materials", unreal.Material, unreal.MaterialFactoryNew())
    standard = source["shader"] == "standard"
    mat.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_DEFAULT_LIT if standard else unreal.MaterialShadingModel.MSM_UNLIT)
    if source.get("texture"):
        expression = unreal.MaterialEditingLibrary.create_material_expression(mat, unreal.MaterialExpressionTextureSample, -300, 0)
        expression.texture = textures[source["texture"]]
        output = "RGB"
    else:
        expression = unreal.MaterialEditingLibrary.create_material_expression(mat, unreal.MaterialExpressionConstant3Vector, -300, 0)
        c = source["color"]
        expression.constant = unreal.LinearColor(c["r"], c["g"], c["b"], c["a"])
        output = ""
    if source.get("texture"):
        tint = unreal.MaterialEditingLibrary.create_material_expression(mat, unreal.MaterialExpressionConstant3Vector, -500, 180)
        c = source["color"]; tint.constant = unreal.LinearColor(c["r"],c["g"],c["b"],c["a"])
        multiply = unreal.MaterialEditingLibrary.create_material_expression(mat, unreal.MaterialExpressionMultiply, -100, 0)
        if not unreal.MaterialEditingLibrary.connect_material_expressions(expression,"RGB",multiply,"A") or not unreal.MaterialEditingLibrary.connect_material_expressions(tint,"",multiply,"B"):
            raise RuntimeError("Base color tint connection failed")
        expression = multiply; output = ""
    if not unreal.MaterialEditingLibrary.connect_material_property(expression, output, unreal.MaterialProperty.MP_BASE_COLOR if standard else unreal.MaterialProperty.MP_EMISSIVE_COLOR):
        raise RuntimeError("Material expression connection failed")
    if standard:
        for prop,value in ((unreal.MaterialProperty.MP_METALLIC,source["metallic"]),(unreal.MaterialProperty.MP_ROUGHNESS,1-source["smoothness"])):
            scalar = unreal.MaterialEditingLibrary.create_material_expression(mat,unreal.MaterialExpressionConstant,-300,300)
            scalar.set_editor_property("r",value)
            if not unreal.MaterialEditingLibrary.connect_material_property(scalar,"",prop): raise RuntimeError("PBR scalar connection failed")
    unreal.MaterialEditingLibrary.recompile_material(mat)
    unreal.EditorAssetLibrary.save_loaded_asset(mat)
    materials[source["id"]] = mat
    if shared.enabled:shared.materials[source["id"]]=mat
    unreal.EditorAssetLibrary.set_metadata_tag(mat,"EngineMigratorSourceShader",source.get("sourceShader", ""))
    record(source["id"], mat, "Material", [source["texture"]] if source.get("texture") else [],source_name=source["name"])
    unreal.EditorAssetLibrary.save_loaded_asset(mat)

for source in package["meshes"]:
    if shared.enabled and source["id"] in shared.meshes:
        mesh=shared.meshes[source["id"]];meshes[source["id"]]=mesh;record(source["id"],mesh,"StaticMesh",source_name=source["name"]);continue
    options = unreal.FbxImportUI()
    options.import_mesh = True
    options.import_as_skeletal = False
    options.import_materials = False
    options.import_textures = False
    options.automated_import_should_detect_type = False
    options.mesh_type_to_import = unreal.FBXImportType.FBXIT_STATIC_MESH
    data = options.static_mesh_import_data
    data.convert_scene = False
    data.convert_scene_unit = False
    data.import_uniform_scale = 1.0
    data.combine_meshes = True
    if len(source.get("normals",[]))==len(source["vertices"]):data.normal_import_method=unreal.FBXNormalImportMethod.FBXNIM_IMPORT_NORMALS
    data.auto_generate_collision = False
    task = unreal.AssetImportTask()
    task.filename = file_path(source["file"])
    task.destination_path = (shared.root if shared.enabled else base) + "/Meshes"
    task.destination_name = "SM_" + source["id"]
    task.automated = True
    task.save = True
    task.factory = unreal.FbxFactory()
    task.options = options
    mesh = imported(task)
    if not isinstance(mesh, unreal.StaticMesh):
        raise RuntimeError("Imported mesh has wrong type")
    if hasattr(unreal.EMBlueprintLibrary,"read_mesh_source"):
        geometry=unreal.EMBlueprintLibrary.read_mesh_source(mesh)
        if geometry.startswith("ERROR:"):raise RuntimeError(geometry)
        geometry=json.loads(geometry)
        def faces(data):
            result=[];vertices=data["vertices"];uv=data.get("uv",[]);normals=data.get("normals",[])
            for i in range(0,len(data["triangles"]),3):
                corners=[]
                for index in data["triangles"][i:i+3]:
                    values=[vertices[index][a] for a in ("x","y","z")]
                    if len(uv)==len(vertices):values.extend(uv[index][a] for a in ("x","y"))
                    if len(normals)==len(vertices):values.extend(normals[index][a] for a in ("x","y","z"))
                    corners.append(tuple(round(v,4) for v in values))
                face=tuple(corners);result.append(min(face,face[1:]+face[:1],face[2:]+face[:2]))
            return sorted(result)
        if faces(source)!=faces(geometry):raise RuntimeError("Imported mesh positions/UV/normals/winding differ from source: "+source["id"])
    box = mesh.get_bounding_box()
    vertices = source["vertices"]
    expected = [(max(v[k] for v in vertices) - min(v[k] for v in vertices)) * 100 for k in ("z", "x", "y")]
    actual = [box.max.x - box.min.x, box.max.y - box.min.y, box.max.z - box.min.z]
    if any(abs(a-b) > 0.05 for a,b in zip(actual, expected)):
        raise RuntimeError("Mesh axis/unit mismatch: actual=%s expected=%s" % (actual, expected))
    meshes[source["id"]] = mesh
    if shared.enabled:shared.meshes[source["id"]]=mesh
    record(source["id"], mesh, "StaticMesh",source_name=source["name"])
    unreal.EditorAssetLibrary.save_loaded_asset(mesh)

world = unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
timing.apply_world(world,package)
subsystem = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
physics.apply_world(world,package)
for source in package["nodes"]:
    v, q, s = source["position"], source["rotation"], source["scale"]
    location = unreal.Vector(v["z"]*100, v["x"]*100, v["y"]*100)
    rotation = unreal.Quat(q["z"], q["x"], q["y"], q["w"]).rotator()
    actor = subsystem.spawn_actor_from_class(scene.actor_class(source), location, rotation)
    actor.set_actor_label(source["name"])
    actor.tags = [unreal.Name(source["id"])]+([unreal.Name("EMRuntimeTemplate")] if source.get("runtimeTemplate") else [])+([unreal.Name("EMRuntimePrefab:"+source["runtimePrefab"])] if source.get("runtimePrefab") else [])
    if not source["active"]:actor.tags=list(actor.tags)+[unreal.Name("EMInactiveSelf")]
    physics.apply(actor,source,base)
    if source.get("hasPhysics"):actor.set_actor_location_and_rotation(location,rotation,False,True)
    actor.set_actor_scale3d(unreal.Vector(s["z"], s["x"], s["y"]))
    actor.set_actor_hidden_in_game(not source.get("effectiveActive",source["active"]))
    component = scene.mesh_component(actor)
    if component:
        component.set_mobility(unreal.ComponentMobility.MOVABLE)
        component.set_collision_enabled(unreal.CollisionEnabled.NO_COLLISION)
        component.set_visibility(source.get("hasRenderer",bool(source.get("materials"))) and source.get("rendererEnabled",True),False)
        component.set_cast_shadow(source.get("castShadows",True))
    if source.get("mesh"):
        component.set_static_mesh(meshes[source["mesh"]])
    for i, mid in enumerate(source.get("materials", [])):
        if not mid:
            raise RuntimeError("Missing material slot")
        component.set_material(i, materials[mid])
    # Persist the profile as well as the runtime flag so reload cannot restore BlockAll.
    if component:
        component.set_collision_profile_name(unreal.Name("NoCollision"))
        component.set_collision_enabled(unreal.CollisionEnabled.NO_COLLISION)
    scene.apply(actor,source)
    actors[source["id"]] = actor

for source in package["nodes"]:
    if source.get("parent"):
        actors[source["id"]].attach_to_actor(actors[source["parent"]], unreal.Name("None"), unreal.AttachmentRule.KEEP_WORLD, unreal.AttachmentRule.KEEP_WORLD, unreal.AttachmentRule.KEEP_WORLD, False)

environment_actors=scene.import_environment(package,world,subsystem)
behaviour_actors=scripts.import_all(package,actors,subsystem,base)
animation_actors=animations.import_all(package,actors,subsystem,base)
ui_actors=ui.import_all(package,subsystem,base,textures)
runtime_policy=policy.apply(world,base)
map_path = base + "/Maps/" + package["scene"]
if not unreal.EditorLoadingAndSavingUtils.save_map(world, map_path):
    raise RuntimeError("Failed saving generated map")
for source in package["nodes"]: unreal.EditorAssetLibrary.set_metadata_tag(actors[source["id"]],scene.NODE_KEY,json.dumps(source,separators=(",",":")))
if config.get("projectUnit"):unreal.EditorAssetLibrary.set_metadata_tag(world,"EngineMigratorProjectUnit",json.dumps(config["projectUnit"]))
if package.get("hasEnvironment"): unreal.EditorAssetLibrary.set_metadata_tag(world,scene.ENV_KEY,json.dumps(package["environment"],separators=(",",":")))
for actor in environment_actors: unreal.EditorAssetLibrary.set_metadata_tag(actor,scene.ENV_KEY,"helper")
scripts.persist(package,world,behaviour_actors)
animations.persist(package,animation_actors,actors)
ui.persist(ui_actors)
if package.get("hasTiming"):unreal.EditorAssetLibrary.set_metadata_tag(world,"EngineMigratorTiming",json.dumps(package["timing"]))
if not unreal.EditorLoadingAndSavingUtils.save_map(world,map_path): raise RuntimeError("Failed saving mapped scene metadata")
unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)
world = unreal.EditorLoadingAndSavingUtils.load_map(map_path)
timing.verify_world(world,package)
if package.get("hasTiming") and (world.get_world_settings().get_editor_property("MinUndilatedFrameTime")!=0 or abs(world.get_world_settings().get_editor_property("MaxUndilatedFrameTime")-package["timing"]["maximumDeltaTime"])>1e-6):raise RuntimeError("Native frame time limits did not survive map reload")
reloaded = {str(actor.tags[0]): actor for actor in subsystem.get_all_level_actors() if actor.tags and str(actor.tags[0]).startswith("node_")}
reloaded = {key:value for key,value in reloaded.items() if not scripts.is_behaviour(value)}
behaviour_bindings=scripts.verify_all(package,reloaded,subsystem.get_all_level_actors())
animations.verify_all(subsystem.get_all_level_actors(),{actor:identity for identity,actor in reloaded.items()},package)
ui.verify_all(subsystem.get_all_level_actors(),package)
for source in package["nodes"]:
    actor = reloaded[source["id"]]
    v = source["position"]
    p = actor.get_actor_location()
    if max(abs(p.x-v["z"]*100), abs(p.y-v["x"]*100), abs(p.z-v["y"]*100)) > 0.05:
        raise RuntimeError("Reloaded transform mismatch")
    q = source["rotation"]; actual_q = actor.get_actor_rotation().quaternion()
    expected_q = [q["z"],q["x"],q["y"],q["w"]]; actual_values = [actual_q.x,actual_q.y,actual_q.z,actual_q.w]
    if min(sum((a-b)**2 for a,b in zip(actual_values,expected_q)), sum((a+b)**2 for a,b in zip(actual_values,expected_q))) > 0.000001:
        raise RuntimeError("Reloaded rotation mismatch")
    expected_s = source["scale"]; actual_s = actor.get_actor_scale3d()
    if max(abs(actual_s.x-expected_s["z"]),abs(actual_s.y-expected_s["x"]),abs(actual_s.z-expected_s["y"])) > 0.00001:
        raise RuntimeError("Reloaded scale mismatch")
    if source.get("parent") and actor.get_attach_parent_actor() != reloaded[source["parent"]]:
        raise RuntimeError("Parent reference missing after reload")
    scene.verify(actor,source)
    physics.verify(actor,source)
    component = scene.mesh_component(actor)
    if component and (component.get_editor_property("visible")!=(source.get("hasRenderer",bool(source.get("materials"))) and source.get("rendererEnabled",True)) or component.get_editor_property("cast_shadow")!=source.get("castShadows",True)):raise RuntimeError("Renderer visibility/shadow state failed reload")
    if component and component.get_collision_enabled() != unreal.CollisionEnabled.NO_COLLISION:
        raise RuntimeError("Unexpected collision after reload: " + actor.get_path_name() + " profile=" + str(component.get_collision_profile_name()))
    if source.get("mesh") and component.static_mesh != meshes[source["mesh"]]:
        raise RuntimeError("Mesh reference missing after reload")
    for i, mid in enumerate(source.get("materials", [])):
        material = component.get_material(i)
        if material != materials[mid]:
            raise RuntimeError("Material reference missing after reload")
        expected_material = next(m for m in package["materials"] if m["id"] == mid)
        tex_id = expected_material.get("texture")
        standard = expected_material["shader"] == "standard"
        input_node = unreal.MaterialEditingLibrary.get_material_property_input_node(material, unreal.MaterialProperty.MP_BASE_COLOR if standard else unreal.MaterialProperty.MP_EMISSIVE_COLOR)
        if tex_id:
            inputs = unreal.MaterialEditingLibrary.get_inputs_for_material_expression(material,input_node) if isinstance(input_node,unreal.MaterialExpressionMultiply) else [input_node]
            if not any(isinstance(n,unreal.MaterialExpressionTextureSample) and n.texture == textures[tex_id] for n in inputs):
                raise RuntimeError("Serialized material graph-to-texture reference missing")
        if standard:
            for prop,value in ((unreal.MaterialProperty.MP_METALLIC,expected_material["metallic"]),(unreal.MaterialProperty.MP_ROUGHNESS,1-expected_material["smoothness"])):
                scalar = unreal.MaterialEditingLibrary.get_material_property_input_node(material,prop)
                if not isinstance(scalar,unreal.MaterialExpressionConstant) or abs(scalar.get_editor_property("r")-value)>0.00001: raise RuntimeError("PBR scalar reload mismatch")
    record(source["id"], actor, actor.get_class().get_name(), [x for x in [source.get("parent"), source.get("mesh")] + source.get("materials", []) if x])
    if source.get("hasVolume"):
        bindings.append({"sourceId":source["volume"]["profileId"],"target":actor.get_path_name()+".Settings","kind":"PostProcessSettings","references":[source["id"]],"mappedProperties":list(scene.post_process_values(source["volume"])),"renderingEquivalent":False})
if package.get("hasEnvironment") and scene.export_environment(world,subsystem.get_all_level_actors()) is None: raise RuntimeError("Environment metadata did not survive map reload")
if policy.read(world)!=runtime_policy:raise RuntimeError("Runtime game mode policy failed save/reload")
bindings.append({"sourceId":package["scene"]+".runtimePolicy","target":runtime_policy["class"],"kind":"GameMode without implicit Pawn/Spectator/HUD","references":[]})
bindings.extend(behaviour_bindings)
physical_materials={}
for source in package["nodes"]:
    if source.get("hasPhysics") and source["physics"].get("customMaterial"):
        identity=source["physics"]["materialId"];actual=json.loads(unreal.EMBlueprintLibrary.read_physics(reloaded[source["id"]]))["nativeMaterial"]
        if identity in physical_materials and physical_materials[identity]!=actual:raise RuntimeError("Shared PhysicsMaterial reference duplicated")
        physical_materials[identity]=actual
for identity,path in physical_materials.items():bindings.append({"sourceId":identity,"target":path,"kind":"PhysicalMaterial","references":[]})
for source in package["nodes"]:
    if source.get("hasPhysics"):
        shape=next(c for c in reloaded[source["id"]].get_components_by_class(unreal.ShapeComponent) if c.get_name()=="EM_Physics")
        bindings.append({"sourceId":source["id"]+".physics","target":shape.get_path_name(),"kind":shape.get_class().get_name(),"references":[source["id"]]+([source["physics"]["materialId"]] if source["physics"].get("customMaterial") else [])})
for actor in subsystem.get_all_level_actors():
    animation=unreal.EditorAssetLibrary.get_metadata_tag(actor,animations.KEY)
    if animation:
        data=json.loads(animation);sequence=actor.get_sequence()
        bindings.append({"sourceId":data["clip"]["id"],"target":sequence.get_path_name(),"kind":"LevelSequence","references":[data["owner"]]+[b.get_name() for b in sequence.get_bindings()]})
    widget=unreal.EditorAssetLibrary.get_metadata_tag(actor,ui.KEY)
    if widget:
        data=json.loads(widget)
        bindings.append({"sourceId":data["owner"]+".canvas","target":data["widget"],"kind":"WidgetBlueprint","references":[n["id"] for n in ui.members(package,data["owner"])]})
        for node in ui.members(package,data["owner"]):
            visual=node["ui"]
            if visual.get("texture"):
                bindings.append({"sourceId":node["id"]+".ui.texture","target":data["widget"]+":"+node["id"]+"_Visual.Brush","kind":"Texture2D / UV region","references":[visual["texture"]]+([visual["sprite"]] if visual.get("sprite") else []),"uv":visual["uv"],"spriteRect":visual.get("spriteRect"),"spritePivot":visual.get("spritePivot"),"pixelsPerUnit":visual.get("pixelsPerUnit")})
            for index,call in enumerate(node["ui"].get("onClick",[])):
                bindings.append({"sourceId":node["id"]+".onClick."+str(index),"target":data["widget"]+":"+node["id"]+"_Visual → "+call["className"]+"::"+call["method"],"kind":"Button callback / "+call["callState"],"references":[call["target"],call["behaviour"]]})
result = {"sourceEngine": package["sourceEngine"], "targetVersion": unreal.SystemLibrary.get_engine_version(), "savedMap": map_path, "savedAndReloaded": True, "referencesVerified": True, "sceneComponentsVerified":True,"behaviourInstancesVerified":len(behaviour_bindings),"renderingEquivalent":False,"warnings":package.get("warnings",[]), "projectPortComplete": False, "bindings": bindings}
import runtime_templates
result["runtimePrefabPrototypes"]=[{"sourceId":n["runtimePrefab"],"target":reloaded[n["id"]].get_path_name(),"representation":"dormant level prototype cloned at runtime"} for n in package["nodes"] if n.get("runtimePrefab")]
runtime_templates.suppress(world,actors,subsystem,map_path)
result["runtimePrototypeNodesVerified"]=sum(bool(n.get("runtimeTemplate")) for n in package["nodes"])
result["bindings"]=[b for b in result["bindings"] if not b["sourceId"].startswith("node_rt_")]
result["bindings"].extend({"sourceId":p["sourceId"],"target":p["target"],"kind":"Runtime prefab prototype / dormant object, script, physics, transform-animation and UI hierarchy","references":[]} for p in result["runtimePrefabPrototypes"])
with open(os.path.join(root, "unreal-import-result.json"), "w", encoding="utf-8") as stream:
    json.dump(result, stream, indent=2)
if config.get("probeObj"):
    mesh = next(iter(meshes.values()))
    task = unreal.AssetExportTask()
    task.object = mesh
    task.filename = os.path.join(root, "probe.obj")
    task.automated = True
    task.prompt = False
    task.replace_identical = True
    task.exporter = unreal.StaticMeshExporterOBJ()
    if not unreal.Exporter.run_asset_export_task(task):
        raise RuntimeError("Native mesh export probe failed")
unreal.log("ENGINE_MIGRATOR_RESOURCES_VERIFIED")

