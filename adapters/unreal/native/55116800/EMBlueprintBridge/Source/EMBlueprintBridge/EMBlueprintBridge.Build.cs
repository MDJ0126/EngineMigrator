using UnrealBuildTool;
public class EMBlueprintBridge : ModuleRules {
 public EMBlueprintBridge(ReadOnlyTargetRules Target) : base(Target) {
  PCHUsage=PCHUsageMode.UseExplicitOrSharedPCHs;
  PublicDependencyModuleNames.AddRange(new[]{"Core","CoreUObject","Engine"});
  PrivateDependencyModuleNames.AddRange(new[]{"UnrealEd","BlueprintGraph","KismetCompiler","Json","JsonUtilities","UMG","UMGEditor","Slate","SlateCore","LevelSequence","MovieScene","PhysicsCore","MeshDescription","StaticMeshDescription"});
 }
}
