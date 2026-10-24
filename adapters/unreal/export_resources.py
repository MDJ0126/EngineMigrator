"""Export an actual Unreal static unlit level into the shared resource package."""
import hashlib, json, os, re, sys
import unreal
sys.path.insert(0,os.path.dirname(__file__))
import timing_components as timing_settings
import scene_components as scene
import script_components as scripts
import physics_components as physics
import animation_components as animations
import ui_components as ui
import runtime_policy as policy

project = unreal.Paths.project_dir()
with open(os.path.join(project, "EngineMigratorRun.json"), encoding="utf-8-sig") as stream:
    config = json.load(stream)
root = os.path.abspath(config["reversePackage"])
os.makedirs(os.path.join(root, "textures"), exist_ok=True)
os.makedirs(os.path.join(root, "meshes"), exist_ok=True)
world = unreal.EditorLoadingAndSavingUtils.load_map(config["map"])
if not world:
    raise RuntimeError("Source map could not be loaded")
import runtime_prototype_guard
runtime_prototype_guard.verify(world,unreal.get_editor_subsystem(unreal.EditorActorSubsystem))
package = {"schema":1,"sourceEngine":"Unreal","sourceVersion":unreal.SystemLibrary.get_engine_version(),"scene":world.get_name(),"textures":[],"materials":[],"meshes":[],"nodes":[],"blockers":[]}
textures, materials, meshes = {}, {}, {}

def asset_id(asset):
    original = unreal.EditorAssetLibrary.get_metadata_tag(asset, "EngineMigratorSourceId")
    if original and not re.fullmatch(r"[A-Za-z0-9_]+", original): raise RuntimeError("Unsafe source asset identifier")
    return original or hashlib.sha256(asset.get_path_name().encode("utf-8")).hexdigest()[:32]

def asset_name(asset):
    return unreal.EditorAssetLibrary.get_metadata_tag(asset,"EngineMigratorSourceName") or asset.get_name()

def export_asset(asset, relative, exporter):
    task = unreal.AssetExportTask()
    task.object = asset
    task.filename = os.path.join(root, relative)
    task.automated = True
    task.prompt = False
    task.replace_identical = False
    task.exporter = exporter
    if os.path.exists(task.filename) or not unreal.Exporter.run_asset_export_task(task):
        raise RuntimeError("Export failed or output exists: " + task.filename)

def texture_data(texture):
    key = asset_id(texture)
    if key not in textures:
        data = {"id":key,"name":asset_name(texture),"file":"textures/"+key+".png","srgb":texture.get_editor_property("srgb")}
        data["filter"] = {unreal.TextureFilter.TF_NEAREST:"Point",unreal.TextureFilter.TF_BILINEAR:"Bilinear",unreal.TextureFilter.TF_TRILINEAR:"Trilinear"}[texture.get_editor_property("filter")]
        address = {unreal.TextureAddress.TA_WRAP:"Repeat",unreal.TextureAddress.TA_CLAMP:"Clamp",unreal.TextureAddress.TA_MIRROR:"Mirror"}
        data["wrapU"] = address[texture.get_editor_property("address_x")]; data["wrapV"] = address[texture.get_editor_property("address_y")]
        mip = texture.get_editor_property("mip_gen_settings")
        if mip not in (unreal.TextureMipGenSettings.TMGS_FROM_TEXTURE_GROUP,unreal.TextureMipGenSettings.TMGS_NO_MIPMAPS): raise RuntimeError("Unsupported texture mip generation setting")
        data["mipmaps"] = mip != unreal.TextureMipGenSettings.TMGS_NO_MIPMAPS
        export_asset(texture, data["file"], unreal.TextureExporterPNG())
        textures[key] = data
        package["textures"].append(data)
    return key

def material_data(material):
    key = asset_id(material)
    if key not in materials:
        if not isinstance(material, unreal.Material): raise RuntimeError("Material instances are unsupported")
        if material.get_editor_property("two_sided") or material.get_editor_property("material_domain")!=unreal.MaterialDomain.MD_SURFACE:raise RuntimeError("Two-sided/non-surface material domains are unsupported")
        model = material.get_editor_property("shading_model")
        if model not in (unreal.MaterialShadingModel.MSM_UNLIT,unreal.MaterialShadingModel.MSM_DEFAULT_LIT): raise RuntimeError("Unsupported shading model")
        if material.get_editor_property("blend_mode") != unreal.BlendMode.BLEND_OPAQUE: raise RuntimeError("Transparent/masked materials are unsupported")
        standard = model == unreal.MaterialShadingModel.MSM_DEFAULT_LIT
        for prop in (unreal.MaterialProperty.MP_NORMAL,unreal.MaterialProperty.MP_OPACITY,unreal.MaterialProperty.MP_OPACITY_MASK,unreal.MaterialProperty.MP_AMBIENT_OCCLUSION,unreal.MaterialProperty.MP_SPECULAR):
            if unreal.MaterialEditingLibrary.get_material_property_input_node(material,prop): raise RuntimeError("Additional material inputs are unsupported: " + str(prop))
        if hasattr(unreal.MaterialProperty,"MP_WORLD_POSITION_OFFSET"):
            offset=bool(unreal.MaterialEditingLibrary.get_material_property_input_node(material,unreal.MaterialProperty.MP_WORLD_POSITION_OFFSET))
        elif hasattr(unreal,"EMBlueprintLibrary") and hasattr(unreal.EMBlueprintLibrary,"has_world_position_offset"):
            offset=unreal.EMBlueprintLibrary.has_world_position_offset(material)
        else:raise RuntimeError("This engine requires its matching native bridge to inspect hidden WorldPositionOffset material inputs")
        if offset:raise RuntimeError("WorldPositionOffset material inputs are unsupported")
        if standard and unreal.MaterialEditingLibrary.get_material_property_input_node(material,unreal.MaterialProperty.MP_EMISSIVE_COLOR): raise RuntimeError("PBR emission is unsupported")
        node = unreal.MaterialEditingLibrary.get_material_property_input_node(material, unreal.MaterialProperty.MP_BASE_COLOR if standard else unreal.MaterialProperty.MP_EMISSIVE_COLOR)
        data = {"id":key,"name":asset_name(material),"shader":"standard" if standard else "unlit","color":{"r":1,"g":1,"b":1,"a":1},"texture":None}
        data["sourceShader"]=unreal.EditorAssetLibrary.get_metadata_tag(material,"EngineMigratorSourceShader")
        if isinstance(node, unreal.MaterialExpressionTextureSample):
            data["texture"] = texture_data(node.texture)
        elif isinstance(node, (unreal.MaterialExpressionConstant3Vector,unreal.MaterialExpressionConstant4Vector)):
            c = node.constant
            data["color"] = {"r":c.r,"g":c.g,"b":c.b,"a":c.a}
        elif isinstance(node,unreal.MaterialExpressionMultiply):
            inputs = unreal.MaterialEditingLibrary.get_inputs_for_material_expression(material,node)
            if len(inputs)!=2 or sum(isinstance(n,unreal.MaterialExpressionTextureSample) for n in inputs)!=1 or sum(isinstance(n,(unreal.MaterialExpressionConstant3Vector,unreal.MaterialExpressionConstant4Vector)) for n in inputs)!=1: raise RuntimeError("Unsupported base color graph")
            data["texture"] = texture_data(next(n.texture for n in inputs if isinstance(n,unreal.MaterialExpressionTextureSample)))
            c = next(n.constant for n in inputs if isinstance(n,(unreal.MaterialExpressionConstant3Vector,unreal.MaterialExpressionConstant4Vector))); data["color"] = {"r":c.r,"g":c.g,"b":c.b,"a":c.a}
        else:
            raise RuntimeError("Unsupported unlit material expression graph")
        if standard:
            for prop,field in ((unreal.MaterialProperty.MP_METALLIC,"metallic"),(unreal.MaterialProperty.MP_ROUGHNESS,"smoothness")):
                scalar = unreal.MaterialEditingLibrary.get_material_property_input_node(material,prop)
                if not isinstance(scalar,unreal.MaterialExpressionConstant): raise RuntimeError("Unsupported PBR scalar graph")
                value = scalar.get_editor_property("r"); data[field] = 1-value if field=="smoothness" else value
        materials[key] = data
        package["materials"].append(data)
    return key

def mesh_data(mesh):
    key = asset_id(mesh)
    if key in meshes:
        return key
    if mesh.get_num_lods()!=1:raise RuntimeError("Multiple static mesh LODs are unsupported")
    if hasattr(unreal.EMBlueprintLibrary,"read_mesh_source"):
        result=unreal.EMBlueprintLibrary.read_mesh_source(mesh)
        if result.startswith("ERROR:"):raise RuntimeError(result+": "+mesh.get_path_name())
        data=json.loads(result);data.update(id=key,name=asset_name(mesh),file="meshes/"+key+".obj")
        if not data["vertices"] or not data["triangles"]:raise RuntimeError("Source mesh contains no triangles")
        # The interchange arrays are authoritative. Keep an OBJ companion in the same basis.
        with open(os.path.join(root,data["file"]),"w",encoding="utf-8") as f:
            for p in data["vertices"]:f.write("v %s %s %s\n"%(p["z"]*100,-p["x"]*100,p["y"]*100))
            for uv in data["uv"]:f.write("vt %s %s\n"%(uv["x"],uv["y"]))
            for n in data["normals"]:f.write("vn %s %s %s\n"%(n["z"],-n["x"],n["y"]))
            for i in range(0,len(data["triangles"]),3):f.write("f "+" ".join(str(v+1)+"/"+str(v+1)+"/"+str(v+1) if data["uv"] else str(v+1)+"//"+str(v+1) for v in data["triangles"][i:i+3])+"\n")
        meshes[key]=data;package["meshes"].append(data);return key
    file = "meshes/" + key + ".obj"
    export_asset(mesh, file, unreal.StaticMeshExporterOBJ())
    # The installed exporter writes vertex normals to its render-data companion,
    # while its main OBJ omits them. Parse the actual exported render geometry.
    internal=os.path.splitext(file)[0]+"_Internal.obj"
    if os.path.isfile(os.path.join(root,internal)):file=internal
    positions, uvs, normals, vertices, output_uvs, output_normals, indices, mapping = [], [], [], [], [], [], [], {}
    with open(os.path.join(root, file)) as stream:
        for line in stream:
            fields = line.split()
            if not fields: continue
            if fields[0] == "v":
                # Installed Unreal 5.8 OBJ exporter emits (UE.x, UE.z, -UE.y).
                x,y,z = map(float, fields[1:4]); positions.append({"x":-z/100,"y":y/100,"z":x/100})
            elif fields[0] == "vt":
                u,v = map(float, fields[1:3]); uvs.append({"x":u,"y":v})
            elif fields[0] == "vn":
                x,y,z = map(float, fields[1:4]); normals.append({"x":-z,"y":y,"z":x})
            elif fields[0] == "f":
                face = []
                for token in fields[1:]:
                    if token not in mapping:
                        values = token.split("/")
                        vi = int(values[0]); ti = int(values[1]) if len(values)>1 and values[1] else None; ni = int(values[2]) if len(values)>2 and values[2] else None
                        mapping[token] = len(vertices)
                        vertices.append(positions[vi-1 if vi>0 else vi])
                        output_uvs.append(uvs[ti-1 if ti>0 else ti] if ti else {"x":0,"y":0})
                        if ni: output_normals.append(normals[ni-1 if ni>0 else ni])
                    face.append(mapping[token])
                for i in range(1,len(face)-1): indices.extend([face[0],face[i],face[i+1]])
    if not vertices or not indices:
        raise RuntimeError("OBJ exported no geometry")
    data = {"id":key,"name":asset_name(mesh),"file":file,"vertices":vertices,"uv":output_uvs,"normals":output_normals,"triangles":indices}
    meshes[key] = data; package["meshes"].append(data)
    return key

subsystem = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
source_actors = [a for a in subsystem.get_all_level_actors() if "EMRuntimeTemplate" not in [str(t) for t in a.tags]]
supported = [actor for actor in source_actors if (isinstance(actor, (unreal.StaticMeshActor,unreal.CameraActor,unreal.DirectionalLight,unreal.PostProcessVolume)) or (actor.get_class()==unreal.Actor.static_class() and actor.get_component_by_class(unreal.ShapeComponent)) or scripts.native_blueprint(actor)) and not scene.is_environment(actor)]
for actor in source_actors:
    if actor not in supported and not scene.is_environment(actor) and not animations.helper(actor) and not ui.helper(actor) and not scripts.is_behaviour(actor) and actor.get_class().get_name() not in ("WorldSettings","Brush"):
        package["blockers"].append("Unsupported actor: " + actor.get_path_name())
node_ids={};used_ids=set()
for i,actor in enumerate(supported):
    metadata=unreal.EditorAssetLibrary.get_metadata_tag(actor,scene.NODE_KEY)
    candidate=json.loads(metadata)["id"] if metadata else "node_"+str(i)
    if candidate in used_ids:candidate="node_new_"+hashlib.sha256(actor.get_path_name().encode()).hexdigest()[:20]
    if not re.fullmatch(r"[A-Za-z0-9_]+",candidate) or candidate in used_ids:raise RuntimeError("Invalid/duplicate scene node identity")
    used_ids.add(candidate);node_ids[actor]=candidate
for actor in supported:
    parent = actor.get_attach_parent_actor()
    if parent and parent not in node_ids: raise RuntimeError("Unsupported attachment parent")
    p = actor.get_actor_location(); q = actor.get_actor_rotation().quaternion(); s = actor.get_actor_scale3d()
    component = scene.mesh_component(actor)
    if component and component.static_mesh and (component.is_simulating_physics() or component.get_collision_enabled() != unreal.CollisionEnabled.NO_COLLISION):
        raise RuntimeError("Physics/collision porting is not implemented for " + actor.get_path_name())
    if component:
        for additional in actor.get_components_by_class(unreal.ActorComponent):
            if additional != component and not (isinstance(additional,unreal.ShapeComponent) and additional.get_name()=="EM_Physics") and not physics.structural_component(actor,additional): raise RuntimeError("Unsupported actor component: " + additional.get_path_name())
    node = {"id":node_ids[actor],"name":actor.get_actor_label(),"parent":node_ids.get(parent),"position":{"x":p.y/100,"y":p.z/100,"z":p.x/100},"rotation":{"x":q.y,"y":q.z,"z":q.x,"w":q.w},"scale":{"x":s.y,"y":s.z,"z":s.x},"active":not actor.get_editor_property("hidden"),"mesh":None,"materials":[]}
    node.update(scene.export_node(actor))
    metadata=unreal.EditorAssetLibrary.get_metadata_tag(actor,scene.NODE_KEY)
    node.update(physics.export(actor,json.loads(metadata) if metadata else {}))
    original=json.loads(metadata) if metadata else {}
    if (original.get("prefab") or {}).get("asset"):node["prefab"]=original["prefab"]
    node["hasAnimation"]=original.get("hasAnimation",False)
    if node["hasAnimation"]:node["animation"]=original["animation"]
    node["hasUI"]=original.get("hasUI",False)
    if node["hasUI"]:node["ui"]=original["ui"]
    if component:
        metadata=unreal.EditorAssetLibrary.get_metadata_tag(actor,scene.NODE_KEY);original=json.loads(metadata) if metadata else {}
        node["hasRenderer"]=original.get("hasRenderer",bool(component.static_mesh));node["rendererEnabled"]=component.get_editor_property("visible") if node["hasRenderer"] else True;node["castShadows"]=component.get_editor_property("cast_shadow")
    if component and component.static_mesh:
        if component.get_num_materials() != 1: raise RuntimeError("Multiple material slots/submeshes are unsupported")
        node["mesh"] = mesh_data(component.static_mesh)
        if node.get("hasRenderer",True):node["materials"] = [material_data(component.get_material(i)) for i in range(component.get_num_materials())]
    package["nodes"].append(node)
package["environment"]=scene.export_environment(world,source_actors)
package["gravity"]=physics.export_world(world)
timing=timing_settings.read_world(world)
package["hasTiming"]=timing is not None
if timing is not None:
    package["timing"]=timing
    if world.get_world_settings().get_editor_property("MinUndilatedFrameTime")!=0:raise RuntimeError("Native minimum frame time has no Unity counterpart")
scripts.export_all(world,source_actors,node_ids,package)
animations.export_all(source_actors,node_ids,package)
ui.export_all(source_actors,package,texture_data)
package["runtimePolicy"]=policy.read(world)
package["hasEnvironment"]=package["environment"] is not None
package["warnings"]=["카메라·광원·후처리·환경의 수치/참조 매핑입니다. 렌더링 외형 동일성은 미검증이며 엔진 고유 값은 원본 메타데이터로 왕복 보존합니다."]
with open(os.path.join(root,"package.json"),"w",encoding="utf-8") as stream:
    json.dump(package,stream,indent=2)
if package["blockers"]:
    raise RuntimeError("Unported actors remain: " + str(package["blockers"]))
unreal.log("ENGINE_MIGRATOR_NATIVE_EXPORT_VERIFIED")
