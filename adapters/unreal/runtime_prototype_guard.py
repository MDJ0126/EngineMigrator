"""Reject stale per-level prototype state instead of silently losing definition edits."""
import copy,json
import unreal

KEY = "EngineMigratorRuntimePrototypeSnapshotV1"

def _path(value):
    return value.get_path_name() if value is not None else ""

def _vector(value, axes):
    return [round(float(value.get_editor_property(axis)), 6) for axis in axes]

def snapshot(subsystem, definition=False, include_ui=False):
    result = []
    all_actors=subsystem.get_all_level_actors()
    node_ids={a:json.loads(raw)["id"] for a in all_actors if (raw:=unreal.EditorAssetLibrary.get_metadata_tag(a,"EngineMigratorSceneNode"))}
    for actor in all_actors:
        tags = sorted(str(t) for t in actor.tags)
        node = unreal.EditorAssetLibrary.get_metadata_tag(actor, "EngineMigratorSceneNode")
        behaviour = unreal.EditorAssetLibrary.get_metadata_tag(actor, "EngineMigratorBehaviour")
        animation = unreal.EditorAssetLibrary.get_metadata_tag(actor,"EngineMigratorAnimation")
        ui=unreal.EditorAssetLibrary.get_metadata_tag(actor,"EngineMigratorUI") if include_ui and (definition or "EMRuntimeUI" in tags) else ""
        if not (node or behaviour or animation or ui) or (not definition and "EMRuntimeTemplate" not in tags):
            continue
        row = {"actor": _path(actor), "class": _path(actor.get_class()), "tags": tags,
               "parent": _path(actor.get_attach_parent_actor()),
               "position": _vector(actor.get_actor_location(), ("x", "y", "z")),
               "rotation": _vector(actor.get_actor_rotation().quaternion(), ("x", "y", "z", "w")),
               "scale": _vector(actor.get_actor_scale3d(), ("x", "y", "z")),
               "components": sorted(_path(c.get_class()) for c in actor.get_components_by_class(unreal.ActorComponent))}
        mesh = actor.get_component_by_class(unreal.StaticMeshComponent)
        if mesh:
            row["mesh"] = _path(mesh.static_mesh)
            row["materials"] = [_path(mesh.get_material(i)) for i in range(mesh.get_num_materials())]
        if any(c.get_name()=="EM_Physics" for c in actor.get_components_by_class(unreal.ShapeComponent)):
            row["physics"]=json.loads(unreal.EMBlueprintLibrary.read_physics(actor))
        if animation:
            import animation_components
            source=json.loads(animation);package={"nodes":[{"id":source["owner"],"animation":{"clips":[copy.deepcopy(source["clip"])]}}]}
            animation_components.export_all([actor],node_ids,package)
            row["animation"]={"owner":_path(actor.get_owner()),"sequence":_path(actor.get_sequence()),"tickEnabled":actor.is_actor_tick_enabled(),"clip":package["nodes"][0]["animation"]["clips"][0]}
        if ui:
            import ui_components
            source=json.loads(ui);package={"nodes":[json.loads(raw) for a in all_actors if (raw:=unreal.EditorAssetLibrary.get_metadata_tag(a,"EngineMigratorSceneNode"))]}
            ui_components.export_all([actor],package)
            widget_class=actor.get_editor_property("EM_WidgetClass") if "EMRuntimeUI" in tags else unreal.load_class(None,source["widget"]+"_C")
            row["ui"]={"owner":_path(actor.get_owner()),"widgetClass":_path(widget_class),"nodes":ui_components.members(package,source["owner"])}
        if behaviour:
            try:row["behaviourEnabled"]=actor.get_editor_property("EM_BehaviourEnabled")
            except Exception:pass
            fields = {}
            for field in json.loads(behaviour)["source"]["fields"]:
                import script_components
                value = actor.get_editor_property(script_components.native_field(actor,field["name"]))
                if field["kind"] in ("node", "transform", "prefab", "component"):
                    value = _path(value)
                elif field["kind"] in ("vector3", "quaternion"):
                    value = _vector(value, ("X", "Y", "Z", "W") if field["kind"] == "quaternion" else ("X", "Y", "Z"))
                fields[field["name"]] = value
            row["fields"] = fields
        result.append(row)
    return sorted(result, key=lambda row: row["actor"])

def persist(world, subsystem, map_path, definition=False):
    data = {"definition": definition, "includeUI":True, "actors": snapshot(subsystem, definition,True)}
    unreal.EditorAssetLibrary.set_metadata_tag(world, KEY, json.dumps(data, sort_keys=True))
    if not unreal.EditorLoadingAndSavingUtils.save_map(world, map_path):
        raise RuntimeError("Runtime prototype snapshot could not be saved")

def verify(world, subsystem, required=False):
    raw = unreal.EditorAssetLibrary.get_metadata_tag(world, KEY)
    if not raw:
        if required or snapshot(subsystem):
            raise RuntimeError("Runtime prototype baseline missing; reimport before reverse porting")
        return
    data = json.loads(raw)
    if data["actors"] != snapshot(subsystem, data["definition"],data.get("includeUI",False)):
        raise RuntimeError("Runtime prefab definition/prototype state changed independently; reimport to synchronize before reverse porting")
