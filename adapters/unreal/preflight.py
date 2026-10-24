import unreal, json, os
result = {"engine": unreal.SystemLibrary.get_engine_version()}
for name in ["AssetImportTask", "FbxImportUI", "FbxFactory", "MaterialEditingLibrary", "EditorLoadingAndSavingUtils", "EditorActorSubsystem", "EditorLevelLibrary", "StaticMeshActor", "TextureExporterPNG", "StaticMeshExporterOBJ"]:
    result[name] = hasattr(unreal, name)
world = unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
result["world"] = world.get_path_name()
with open(os.path.join(os.path.dirname(__file__), "../../artifacts/unreal-preflight.json"), "w") as f:
    json.dump(result, f, indent=2)
unreal.log("ENGINE_MIGRATOR_PREFLIGHT_OK")
