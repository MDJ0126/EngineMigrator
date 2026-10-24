namespace EngineMigrator.Core;
internal static class FixedUpdatePorting
{
    internal const string Includes="#include \"Engine/GameInstance.h\"\n";
    internal const string PreviousRuntime="""
#ifndef ENGINE_MIGRATOR_FIXED_RUNTIME
#define ENGINE_MIGRATOR_FIXED_RUNTIME
struct FEMFixedRuntime {
 static constexpr int64 Rate=141120000;
 struct FClock{int64 Elapsed=0;int64 Completed=0;int64 StepTicks=2822399;float Step=0.02f;uint64 LastFrame=MAX_uint64;};
 struct FState{TArray<TWeakObjectPtr<AActor>> Actors;};
 static TMap<TWeakObjectPtr<UWorld>,FState>& Worlds(){static TMap<TWeakObjectPtr<UWorld>,FState> Data;return Data;}
 static TMap<TWeakObjectPtr<UObject>,FClock>& Clocks(){static TMap<TWeakObjectPtr<UObject>,FClock> Data;return Data;}
 static TWeakObjectPtr<UObject> Key(UWorld* World){UObject* Instance=World->GetGameInstance();return Instance?Instance:static_cast<UObject*>(World);}
 static float Config(AActor* Actor){for(const FName& Tag:Actor->Tags){FString Value=Tag.ToString();if(Value.StartsWith(TEXT("EMFixedStep:"))){float Step=FCString::Atof(*Value.RightChop(12));checkf(FMath::IsFinite(Step)&&Step>0.0f,TEXT("Invalid fixed time step"));return Step;}}return float(2822399.0/double(Rate));}
 static float Step(UWorld* World,AActor* Actor){auto& Data=Clocks();auto Instance=Key(World);if(auto* Clock=Data.Find(Instance))return Clock->Step;FClock Clock;Clock.Step=Config(Actor);Clock.StepTicks=FMath::RoundToInt64(double(Clock.Step)*Rate);for(const FName& Tag:Actor->Tags){FString Text=Tag.ToString();if(Text.StartsWith(TEXT("EMFixedTicks:")))Clock.StepTicks=FCString::Atoi64(*Text.RightChop(13));}checkf(Clock.StepTicks>0,TEXT("Invalid fixed time ticks"));Data.Add(Instance,Clock);return Clock.Step;}
 static int32 Order(AActor* Actor){for(const FName& Tag:Actor->Tags){FString Text=Tag.ToString();if(Text.StartsWith(TEXT("EMScriptOrder:")))return FCString::Atoi(*Text.RightChop(14));}return 0;}
 static void Cleanup(UWorld* World,bool,bool){Worlds().Remove(TWeakObjectPtr<UWorld>(World));Clocks().Remove(TWeakObjectPtr<UObject>(World));for(auto It=Clocks().CreateIterator();It;++It)if(!It.Key().IsValid())It.RemoveCurrent();}
 static void Tick(UWorld* World,ELevelTick Type,float Delta){
  if(Type!=LEVELTICK_All||GFrameCounter==0)return;
  if((World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)||Delta<=0.0f)return;auto* Clock=Clocks().Find(Key(World));if(!Clock||Clock->LastFrame==GFrameCounter)return;Clock->LastFrame=GFrameCounter;
  const float Fixed=Clock->Step;Clock->Elapsed+=FMath::FloorToInt64(double(Delta)*Rate);const int64 Desired=Clock->Elapsed/Clock->StepTicks+1;const int64 Count=FMath::Max<int64>(0,Desired-Clock->Completed);Clock->Completed=Desired;TWeakObjectPtr<UWorld> WorldKey=World;
  for(int64 Index=0;Index<Count;++Index){auto* State=Worlds().Find(WorldKey);if(!State||World->bIsTearingDown)return;State->Actors.RemoveAll([](const TWeakObjectPtr<AActor>& Value){return !Value.IsValid();});auto Actors=State->Actors;
   Actors.StableSort([](const TWeakObjectPtr<AActor>& A,const TWeakObjectPtr<AActor>& B){return Order(A.Get())<Order(B.Get());});
   for(const auto& Weak:Actors){if(World->bIsTearingDown)return;AActor* Actor=Weak.Get();if(!IsValid(Actor))continue;if(UFunction* Function=Actor->FindFunction(TEXT("EM_RuntimeFixedTick"))){struct{float Delta;}Args{Fixed};Actor->ProcessEvent(Function,&Args);}}
  }
 }
 static void Register(AActor* Actor){UWorld* World=Actor->GetWorld();if((World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)||Actor->Tags.Contains(TEXT("EMRuntimeTemplate")))return;Step(World,Actor);Worlds().FindOrAdd(TWeakObjectPtr<UWorld>(World)).Actors.AddUnique(Actor);
  static bool Bound=false;if(!Bound){Bound=true;FWorldDelegates::OnWorldPreActorTick.AddStatic(&Tick);FWorldDelegates::OnWorldCleanup.AddStatic(&Cleanup);}
 }
};
#endif

""";
    // Preserve the observed startup time-zero fixed callback even when initial timeScale is zero.
    internal static readonly string StartupRuntime=PreviousRuntime.Replace("||Delta<=0.0f)return;","||Delta<0.0f)return;",StringComparison.Ordinal);
    // Unity quantizes the rendered frame before scaling it, not the already-scaled world float.
    internal static readonly string FourthRuntime=StartupRuntime.Replace("Clock->Elapsed+=FMath::FloorToInt64(double(Delta)*Rate);","const float Unscaled=World->DeltaRealTimeSeconds;const int64 RenderTicks=FMath::FloorToInt64(double(Unscaled)*Rate);const double FrameScale=Unscaled>0.0f?double(Delta/Unscaled):0.0;Clock->Elapsed+=FMath::FloorToInt64(double(RenderTicks)*FrameScale);",StringComparison.Ordinal);
    internal static readonly string Runtime=FourthRuntime.Replace("\r\n","\n").Replace("\r","\n").Replace("Actor->ProcessEvent(Function,&Args);}}\n  }\n }\n static void Register", "Actor->ProcessEvent(Function,&Args);}}\n   if(World->bIsTearingDown)return;State=Worlds().Find(WorldKey);if(!State)return;auto Pending=State->Actors;Pending.RemoveAll([](const TWeakObjectPtr<AActor>& Value){return !Value.IsValid();});Pending.StableSort([](const TWeakObjectPtr<AActor>& A,const TWeakObjectPtr<AActor>& B){return Order(A.Get())<Order(B.Get());});for(const auto& Weak:Pending){if(World->bIsTearingDown)return;AActor* Actor=Weak.Get();if(IsValid(Actor))if(UFunction* Function=Actor->FindFunction(TEXT(\"EM_RuntimeFixedBoundaryStart\")))Actor->ProcessEvent(Function,nullptr);}\n  }\n }\n static void Register",StringComparison.Ordinal);
    internal const string LegacyRuntime="""
#ifndef ENGINE_MIGRATOR_FIXED_RUNTIME
#define ENGINE_MIGRATOR_FIXED_RUNTIME
struct FEMFixedRuntime {
 static constexpr int64 Rate=141120000;
 struct FState{int64 Elapsed=0;int64 Completed=0;int64 StepTicks=2822399;float Step=0.02f;TArray<TWeakObjectPtr<AActor>> Actors;};
 static TMap<TWeakObjectPtr<UWorld>,FState>& Worlds(){static TMap<TWeakObjectPtr<UWorld>,FState> Data;return Data;}
 static float Config(AActor* Actor){for(const FName& Tag:Actor->Tags){FString Value=Tag.ToString();if(Value.StartsWith(TEXT("EMFixedStep:"))){float Step=FCString::Atof(*Value.RightChop(12));checkf(FMath::IsFinite(Step)&&Step>0.0f,TEXT("Invalid fixed time step"));return Step;}}return float(2822399.0/double(Rate));}
 static float Step(UWorld* World,AActor* Actor){auto& Data=Worlds();TWeakObjectPtr<UWorld> Key=World;if(auto* State=Data.Find(Key))return State->Step;FState State;State.Step=Config(Actor);State.StepTicks=FMath::RoundToInt64(double(State.Step)*Rate);for(const FName& Tag:Actor->Tags){FString Text=Tag.ToString();if(Text.StartsWith(TEXT("EMFixedTicks:")))State.StepTicks=FCString::Atoi64(*Text.RightChop(13));}checkf(State.StepTicks>0,TEXT("Invalid fixed time ticks"));Data.Add(Key,State);return State.Step;}
 static int32 Order(AActor* Actor){for(const FName& Tag:Actor->Tags){FString Text=Tag.ToString();if(Text.StartsWith(TEXT("EMScriptOrder:")))return FCString::Atoi(*Text.RightChop(14));}return 0;}
 static void Cleanup(UWorld* World,bool,bool){Worlds().Remove(TWeakObjectPtr<UWorld>(World));}
 static void Tick(UWorld* World,ELevelTick,float Delta){
  if(World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)return;TWeakObjectPtr<UWorld> Key=World;auto* State=Worlds().Find(Key);if(!State)return;
  if(Delta<=0.0f)return;const float Fixed=State->Step;State->Elapsed+=FMath::FloorToInt64(double(Delta)*Rate);const int64 Desired=State->Elapsed/State->StepTicks+1;const int64 Count=FMath::Max<int64>(0,Desired-State->Completed);State->Completed=Desired;
  for(int64 Index=0;Index<Count;++Index){State=Worlds().Find(Key);if(!State||World->bIsTearingDown)return;State->Actors.RemoveAll([](const TWeakObjectPtr<AActor>& Value){return !Value.IsValid();});auto Actors=State->Actors;
   Actors.StableSort([](const TWeakObjectPtr<AActor>& A,const TWeakObjectPtr<AActor>& B){return Order(A.Get())<Order(B.Get());});
   for(const auto& Weak:Actors){if(World->bIsTearingDown)return;AActor* Actor=Weak.Get();if(!IsValid(Actor))continue;if(UFunction* Function=Actor->FindFunction(TEXT("EM_RuntimeFixedTick"))){struct{float Delta;}Args{Fixed};Actor->ProcessEvent(Function,&Args);}}
  }
 }
 static void Register(AActor* Actor){UWorld* World=Actor->GetWorld();if((World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)||Actor->Tags.Contains(TEXT("EMRuntimeTemplate")))return;Step(World,Actor);Worlds().FindChecked(TWeakObjectPtr<UWorld>(World)).Actors.AddUnique(Actor);
  static bool Bound=false;if(!Bound){Bound=true;FWorldDelegates::OnWorldPreActorTick.AddStatic(&Tick);FWorldDelegates::OnWorldCleanup.AddStatic(&Cleanup);}
 }
};
#endif

""";
    internal const string Helpers="""
public:
UFUNCTION()
void EM_RuntimeFixedTick(float EM_Delta){
 if(Tags.Contains(TEXT("EMRuntimeTemplate"))||!EM_ObjectActiveInHierarchy(EM_ComponentOwner(this))||!EM_BehaviourEnabled||EM_PendingDestroy||EM_Finalized)return;
 if(!EM_Started){EM_StartQueued=false;EM_StartOnce();}
 if(EM_Started&&EM_EnabledFrame!=GFrameCounter)EM_FixedUpdate(EM_Delta);
}
protected:
float EM_FixedStep() const{return FEMFixedRuntime::Step(GetWorld(),const_cast<AActor*>(static_cast<const AActor*>(this)));}

""";
    internal static string CurrentHelpers(bool late)
    {
        var context=late?"FEMLateRuntime::PreStartWorld()":"FEMStartRuntime::PreWorld()";
        var guard="TGuardValue<TWeakObjectPtr<UWorld>> EM_FixedStartupScope("+context+",TWeakObjectPtr<UWorld>(GetWorld()));";
        var start=(late?"FEMLateRuntime::ForgetStart(this);":"")+"EM_RuntimeStartNow(true);";
        return Helpers.Replace("\r\n","\n").Replace("\r","\n").Replace(" if(!EM_Started){EM_StartQueued=false;EM_StartOnce();}"," "+guard+"\n if(!EM_Started){"+start+"}",StringComparison.Ordinal)
            .Replace("protected:\nfloat EM_FixedStep()", "UFUNCTION()\nvoid EM_RuntimeFixedBoundaryStart(){if(EM_PendingDestroy||EM_Finalized||!EM_BehaviourEnabled||!EM_ObjectActiveInHierarchy(EM_ComponentOwner(this)))return;if(EM_Started){if(EM_EnabledFrame==GFrameCounter)EM_EnabledFrame=MAX_uint64;return;}if(!EM_StartQueued)return;"+guard+start+"}\nprotected:\nfloat EM_FixedStep()",StringComparison.Ordinal);
    }
    internal const string BeginSuffix=" FEMFixedRuntime::Register(this);";
}
