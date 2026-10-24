namespace EngineMigrator.Core;
internal static class TimeScalePorting
{
 internal const string Includes="#include \"Engine/GameInstance.h\"\n#include \"GameFramework/WorldSettings.h\"\n";
 internal const string LegacyRuntime="""
#ifndef ENGINE_MIGRATOR_TIME_SCALE_RUNTIME
#define ENGINE_MIGRATOR_TIME_SCALE_RUNTIME
struct FEMTimeScaleRuntime {
 static TMap<TWeakObjectPtr<UObject>,float>& Values(){static TMap<TWeakObjectPtr<UObject>,float> Data;return Data;}
 static TWeakObjectPtr<UObject> Key(UWorld* World){UObject* Instance=World->GetGameInstance();return Instance?Instance:static_cast<UObject*>(World);}
 static float Get(UWorld* World){if(auto* Value=Values().Find(Key(World)))return *Value;return 1.0f;}
 static void Apply(UWorld* World,float Value){AWorldSettings* Settings=World->GetWorldSettings();Settings->MinGlobalTimeDilation=0.0f;Settings->MaxGlobalTimeDilation=FMath::Max(Settings->MaxGlobalTimeDilation,Value);Settings->SetTimeDilation(Value);}
 static void Set(UWorld* World,float Value){checkf(FMath::IsFinite(Value),TEXT("Unsupported nonfinite Unity time scale"));if(Value<0.0f)return;Values().FindOrAdd(Key(World))=Value;Apply(World,Value);}
 static void Cleanup(UWorld* World,bool,bool){Values().Remove(TWeakObjectPtr<UObject>(World));for(auto It=Values().CreateIterator();It;++It)if(!It.Key().IsValid())It.RemoveCurrent();}
 static void Register(UWorld* World){if(World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)return;Apply(World,Get(World));static bool Bound=false;if(!Bound){Bound=true;FWorldDelegates::OnWorldCleanup.AddStatic(&Cleanup);}}
};
#endif

""";
 internal static readonly string Runtime=LegacyRuntime
  .Replace("if(auto* Value=Values().Find(Key(World)))return *Value;return 1.0f;", "if(auto* Value=Values().Find(Key(World)))return *Value;for(const FName& Tag:World->GetWorldSettings()->Tags){FString Text=Tag.ToString();if(Text.StartsWith(TEXT(\"EMTimeScale:\"))){float Value=FCString::Atof(*Text.RightChop(12));checkf(FMath::IsFinite(Value)&&Value>=0.0f,TEXT(\"Invalid initial time scale\"));return Value;}}return 1.0f;")
  .Replace(" static void Register(UWorld* World){if(World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)return;Apply(World,Get(World));static bool Bound=false;if(!Bound){Bound=true;FWorldDelegates::OnWorldCleanup.AddStatic(&Cleanup);}}", " static void TickStart(UWorld* World,ELevelTick,float){if(World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)return;float Value=Get(World);Values().FindOrAdd(Key(World))=Value;Apply(World,Value);}\n static void Bind(){static bool Bound=false;if(!Bound){Bound=true;FWorldDelegates::OnWorldTickStart.AddStatic(&TickStart);FWorldDelegates::OnWorldCleanup.AddStatic(&Cleanup);}}\n static void Register(UWorld* World){if(World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)return;TickStart(World,LEVELTICK_All,0.0f);Bind();}");
 internal static string ModuleCode=>"\n#include \"Engine/World.h\"\n"+Includes+Runtime+"\nnamespace {struct FEMProjectTimingBootstrap{FEMProjectTimingBootstrap(){FEMTimeScaleRuntime::Bind();}};static FEMProjectTimingBootstrap EMProjectTimingBootstrap;}\n";
 internal static bool NeedsRuntime(System.Text.Json.Nodes.JsonNode? package)=>package?["hasTiming"]?.GetValue<bool>()==true&&(package["timing"]?["timeScale"]?.GetValue<float>()??1f)!=1f;
 internal const string Helpers="""
protected:
float EM_TimeScale() const{return FEMTimeScaleRuntime::Get(GetWorld());}
void EM_SetTimeScale(float EM_Value){FEMTimeScaleRuntime::Set(GetWorld(),EM_Value);}

""";
 internal const string BeginSuffix=" FEMTimeScaleRuntime::Register(GetWorld());";
}
