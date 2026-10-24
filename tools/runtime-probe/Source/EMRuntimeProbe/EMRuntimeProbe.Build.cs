using UnrealBuildTool;
public class EMRuntimeProbe : ModuleRules {
 public EMRuntimeProbe(ReadOnlyTargetRules Target) : base(Target) {
  PCHUsage=PCHUsageMode.UseExplicitOrSharedPCHs;
  PrivateDependencyModuleNames.AddRange(new[]{"Core","CoreUObject","Engine","UMG","Json"});
 }
}
