namespace EngineMigrator.Core;
internal static class StartQueuePorting
{
 internal const string Includes="#include \"Engine/GameInstance.h\"\n";
 internal const string Runtime="""
#ifndef ENGINE_MIGRATOR_START_RUNTIME
#define ENGINE_MIGRATOR_START_RUNTIME
struct FEMStartRuntime {
 using FQueue=TMap<TWeakObjectPtr<UWorld>,TArray<TWeakObjectPtr<AActor>>>;
 static FQueue& Pre(){static FQueue Value;return Value;}
 static FQueue& Post(){static FQueue Value;return Value;}
 struct FClock{int64 Elapsed=0;int64 Completed=0;int64 StepTicks=2822399;uint64 LastFrame=MAX_uint64;};
 static TMap<TWeakObjectPtr<UObject>,FClock>& Clocks(){static TMap<TWeakObjectPtr<UObject>,FClock> Value;return Value;}
 static TWeakObjectPtr<UObject> ClockKey(UWorld* World){UObject* Instance=World->GetGameInstance();return Instance?Instance:static_cast<UObject*>(World);}
 static void Configure(AActor* Actor){const auto Key=ClockKey(Actor->GetWorld());if(Clocks().Contains(Key))return;FClock Clock;for(const FName& Tag:Actor->Tags){const FString Text=Tag.ToString();if(Text.StartsWith(TEXT("EMFixedStep:")))Clock.StepTicks=FMath::RoundToInt64(double(FCString::Atof(*Text.RightChop(12)))*141120000);}for(const FName& Tag:Actor->Tags){const FString Text=Tag.ToString();if(Text.StartsWith(TEXT("EMFixedTicks:")))Clock.StepTicks=FCString::Atoi64(*Text.RightChop(13));}checkf(Clock.StepTicks>0,TEXT("Invalid startup fixed time ticks"));Clocks().Add(Key,Clock);}
 static int64 StartupPasses(UWorld* World,float Delta){auto& Clock=Clocks().FindChecked(ClockKey(World));if(Clock.LastFrame==GFrameCounter)return 1;Clock.LastFrame=GFrameCounter;const float Unscaled=World->DeltaRealTimeSeconds;const int64 RenderTicks=FMath::FloorToInt64(double(Unscaled)*141120000);const double Scale=Unscaled>0.0f?double(Delta/Unscaled):0.0;Clock.Elapsed+=FMath::FloorToInt64(double(RenderTicks)*Scale);const int64 Desired=Clock.Elapsed/Clock.StepTicks+1;const int64 Count=FMath::Max<int64>(1,Desired-Clock.Completed);Clock.Completed=Desired;return Count;}
 static TWeakObjectPtr<UWorld>& PreWorld(){static TWeakObjectPtr<UWorld> Value;return Value;}
 static int32 Order(AActor* Actor){for(const FName& Tag:Actor->Tags){const FString Text=Tag.ToString();if(Text.StartsWith(TEXT("EMScriptOrder:")))return FCString::Atoi(*Text.RightChop(14));}return 0;}
 static bool Before(UWorld* World){return !World->bInTick||PreWorld().Get()==World;}
 static void Queue(AActor* Actor,bool Early){(Early?Pre():Post()).FindOrAdd(TWeakObjectPtr<UWorld>(Actor->GetWorld())).AddUnique(Actor);}
 static void Forget(AActor* Actor){const TWeakObjectPtr<UWorld> Key=Actor->GetWorld();const TWeakObjectPtr<AActor> Value=Actor;for(FQueue* Queues:{&Pre(),&Post()})if(auto* Pending=Queues->Find(Key))Pending->Remove(Value);}
 static void Flush(UWorld* World,bool Early){
  const TWeakObjectPtr<UWorld> Key=World;FQueue& Queues=Early?Pre():Post();auto* Pending=Queues.Find(Key);if(!Pending)return;auto Batch=*Pending;Queues.Remove(Key);Batch.RemoveAll([](const TWeakObjectPtr<AActor>& Value){return !Value.IsValid();});Batch.StableSort([](const TWeakObjectPtr<AActor>& A,const TWeakObjectPtr<AActor>& B){return Order(A.Get())<Order(B.Get());});TGuardValue<TWeakObjectPtr<UWorld>> Guard(PreWorld(),Early?Key:PreWorld());
  for(const auto& Weak:Batch){if(!IsValid(World)||World->bIsTearingDown)return;AActor* Actor=Weak.Get();if(IsValid(Actor))if(UFunction* Function=Actor->FindFunction(TEXT("EM_RuntimeStartNow"))){struct{bool BeforeUpdate;}Args{Early};Actor->ProcessEvent(Function,&Args);}}
  if(!Early)if(auto* Remainder=Post().Find(Key)){auto Next=*Remainder;Post().Remove(Key);for(const auto& Weak:Next)if(AActor* Actor=Weak.Get()){if(UFunction* Function=Actor->FindFunction(TEXT("EM_RuntimeStartBeforeNextUpdate")))Actor->ProcessEvent(Function,nullptr);Pre().FindOrAdd(Key).AddUnique(Weak);}}
 }
 static void PreTick(UWorld* World,ELevelTick Type,float Delta){if(Type==LEVELTICK_All&&GFrameCounter!=0&&(World->WorldType==EWorldType::Game||World->WorldType==EWorldType::PIE)&&Clocks().Contains(ClockKey(World))){const int64 Count=StartupPasses(World,Delta);for(int64 Index=0;Index<Count;++Index)Flush(World,true);}}
 static void PostTick(UWorld* World,ELevelTick Type,float){if(Type==LEVELTICK_All&&GFrameCounter!=0&&(World->WorldType==EWorldType::Game||World->WorldType==EWorldType::PIE))Flush(World,false);}
 static void EndTick(UWorld* World,ELevelTick Type,float){if(Type==LEVELTICK_All&&GFrameCounter!=0&&(World->WorldType==EWorldType::Game||World->WorldType==EWorldType::PIE))Flush(World,true);}
 static void Cleanup(UWorld* World,bool,bool){const TWeakObjectPtr<UWorld> Key=World;Pre().Remove(Key);Post().Remove(Key);Clocks().Remove(TWeakObjectPtr<UObject>(World));for(auto It=Clocks().CreateIterator();It;++It)if(!It.Key().IsValid())It.RemoveCurrent();}
 static void Register(AActor* Actor){UWorld* World=Actor->GetWorld();if((World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)||Actor->Tags.Contains(TEXT("EMRuntimeTemplate")))return;Configure(Actor);static bool Bound=false;if(!Bound){Bound=true;FWorldDelegates::OnWorldPreActorTick.AddStatic(&PreTick);FWorldDelegates::OnWorldPostActorTick.AddStatic(&PostTick);FWorldDelegates::OnWorldTickEnd.AddStatic(&EndTick);FWorldDelegates::OnWorldCleanup.AddStatic(&Cleanup);}}
};
#endif

""";
 internal const string Helpers="""
private:
bool EM_StartBeforeUpdate=false;
public:
UFUNCTION()
void EM_RuntimeStartNow(bool EM_Early){EM_StartQueued=false;EM_StartBeforeUpdate=false;FEMStartRuntime::Forget(this);if(EM_Early)EM_EnabledFrame=MAX_uint64;else Tags.AddUnique(TEXT("EMPostStartDispatch"));EM_StartOnce();if(IsValid(this)&&!EM_Early)Tags.Remove(TEXT("EMPostStartDispatch"));}
UFUNCTION()
void EM_RuntimeStartBeforeNextUpdate(){EM_StartBeforeUpdate=true;}
protected:

""";
 internal const string DeferStart="void EM_DeferStart(){\n if(EM_StartQueued||EM_Started)return;EM_StartQueued=true;EM_StartBeforeUpdate=FEMStartRuntime::Before(GetWorld());FEMStartRuntime::Queue(this,EM_StartBeforeUpdate);\n}\n";
 internal const string TickPrefix="if(EM_StartQueued&&EM_StartBeforeUpdate)EM_RuntimeStartNow(true); ";
 internal const string OldStartup="void EM_StartupStart(){if(!GetWorld()->bInTick)EM_EnabledFrame=MAX_uint64;EM_StartOnce();}";
 internal const string Startup="void EM_StartupStart(){TGuardValue<TWeakObjectPtr<UWorld>> EM_Guard(FEMStartRuntime::PreWorld(),TWeakObjectPtr<UWorld>(GetWorld()));if(!GetWorld()->bInTick)EM_EnabledFrame=MAX_uint64;EM_StartOnce();}";
 internal const string BeginSuffix=" FEMStartRuntime::Register(this);";
}
