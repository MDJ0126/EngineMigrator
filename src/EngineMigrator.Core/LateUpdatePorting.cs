namespace EngineMigrator.Core;
internal static class LateUpdatePorting
{
 internal const string LegacyRuntime="""
#ifndef ENGINE_MIGRATOR_LATE_RUNTIME
#define ENGINE_MIGRATOR_LATE_RUNTIME
struct FEMLateRuntime {
 static TMap<TWeakObjectPtr<UWorld>,TArray<TWeakObjectPtr<AActor>>>& Worlds(){static TMap<TWeakObjectPtr<UWorld>,TArray<TWeakObjectPtr<AActor>>> Data;return Data;}
 static int32 Order(AActor* Actor){for(const FName& Tag:Actor->Tags){FString Text=Tag.ToString();if(Text.StartsWith(TEXT("EMScriptOrder:")))return FCString::Atoi(*Text.RightChop(14));}return 0;}
 static void Cleanup(UWorld* World,bool,bool){Worlds().Remove(TWeakObjectPtr<UWorld>(World));}
 static void Tick(UWorld* World,ELevelTick Type,float Delta){if(Type!=LEVELTICK_All||GFrameCounter==0||(World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE))return;auto* State=Worlds().Find(TWeakObjectPtr<UWorld>(World));if(!State)return;State->RemoveAll([](const TWeakObjectPtr<AActor>& Value){return !Value.IsValid();});auto Actors=*State;Actors.StableSort([](const TWeakObjectPtr<AActor>& A,const TWeakObjectPtr<AActor>& B){return Order(A.Get())<Order(B.Get());});for(const auto& Weak:Actors){if(World->bIsTearingDown)return;AActor* Actor=Weak.Get();if(!IsValid(Actor))continue;if(UFunction* Function=Actor->FindFunction(TEXT("EM_RuntimeLateTick"))){struct{float Delta;}Args{Delta};Actor->ProcessEvent(Function,&Args);}}}
 static void Register(AActor* Actor){UWorld* World=Actor->GetWorld();if((World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)||Actor->Tags.Contains(TEXT("EMRuntimeTemplate")))return;Worlds().FindOrAdd(TWeakObjectPtr<UWorld>(World)).AddUnique(Actor);static bool Bound=false;if(!Bound){Bound=true;FWorldDelegates::OnWorldPostActorTick.AddStatic(&Tick);FWorldDelegates::OnWorldCleanup.AddStatic(&Cleanup);}}
};
#endif

""";
 internal const string LegacyHelpers="""
public:
UFUNCTION()
void EM_RuntimeLateTick(float EM_Delta){
 if(Tags.Contains(TEXT("EMRuntimeTemplate"))||!EM_ObjectActiveInHierarchy(EM_ComponentOwner(this))||!EM_BehaviourEnabled||EM_PendingDestroy||EM_Finalized)return;
 if(!EM_Started){EM_StartQueued=false;EM_StartOnce();}
 if(EM_Started)EM_LateUpdate(EM_Delta);
}
protected:

""";
 internal const string BeforeStart="const bool EM_BeforeUpdate=!GetWorld()->bInTick||FEMLateRuntime::IsDispatching(GetWorld());";
 internal const string BeforeDestroy="const bool EM_NextPre=EM_IsPostStartDispatch()||FEMLateRuntime::IsDispatching(GetWorld());";
 internal static string PreviousRuntime=>LegacyRuntime.Replace(" static void Cleanup", " static TWeakObjectPtr<UWorld>& DispatchWorld(){static TWeakObjectPtr<UWorld> Value;return Value;}\n static bool IsDispatching(UWorld* World){return DispatchWorld().Get()==World;}\n static void Cleanup")
  .Replace("auto Actors=*State;Actors.StableSort", "auto Actors=*State;Actors.RemoveAll([](const TWeakObjectPtr<AActor>& Weak){AActor* Actor=Weak.Get();if(!IsValid(Actor))return true;UFunction* Function=Actor->FindFunction(TEXT(\"EM_RuntimeLateEligible\"));if(!Function)return true;struct{bool ReturnValue;}Args{false};Actor->ProcessEvent(Function,&Args);return !Args.ReturnValue;});TGuardValue<TWeakObjectPtr<UWorld>> Guard(DispatchWorld(),TWeakObjectPtr<UWorld>(World));Actors.StableSort");
 internal const string PreviousEligibility="""
UFUNCTION()
bool EM_RuntimeLateEligible(){return !Tags.Contains(TEXT("EMRuntimeTemplate"))&&EM_ObjectActiveInHierarchy(EM_ComponentOwner(this))&&EM_BehaviourEnabled&&!EM_PendingDestroy&&!EM_Finalized;}
""";
 internal static string PreviousHelpers=>LegacyHelpers.Replace("\r\n","\n").Replace("public:\nUFUNCTION()", "public:\n"+PreviousEligibility+"\nUFUNCTION()");
 internal const string EnableStamp="if(FEMLateRuntime::IsDispatching(GetWorld()))EM_LateDeferredFrame=GFrameCounter;";
 internal const string DeferStart="void EM_DeferStart(){\n if(EM_StartQueued||EM_Started)return;EM_StartQueued=true;FEMLateRuntime::QueueStart(this);\n}\n";
 internal static string LegacyStartHelper(string name)=>System.Text.RegularExpressions.Regex.Match(WholeObjectDestructionPorting.Helpers(name),@"void EM_DeferStart\(\)\{[\s\S]*?\r?\n\}\r?\n").Value;
 internal static string Helpers=>PreviousHelpers.Replace("public:\n", "private:\nuint64 EM_LateDeferredFrame=MAX_uint64;\npublic:\n",StringComparison.Ordinal)
  .Replace("bool EM_RuntimeLateEligible(){return ","bool EM_RuntimeLateEligible(){return EM_LateDeferredFrame!=GFrameCounter&&",StringComparison.Ordinal)
  .Replace("void EM_RuntimeLateTick(float EM_Delta){", "void EM_RuntimeStartNow(bool EM_BeforeUpdate){EM_StartQueued=false;if(EM_BeforeUpdate)EM_EnabledFrame=MAX_uint64;if(!EM_BeforeUpdate)Tags.AddUnique(TEXT(\"EMPostStartDispatch\"));EM_StartOnce();if(IsValid(this)&&!EM_BeforeUpdate)Tags.Remove(TEXT(\"EMPostStartDispatch\"));}\nUFUNCTION()\nvoid EM_RuntimeLateTick(float EM_Delta){\n if(EM_LateDeferredFrame==GFrameCounter)return;",StringComparison.Ordinal)
  .Replace("EM_StartQueued=false;EM_StartOnce();", "EM_StartQueued=false;FEMLateRuntime::ForgetStart(this);EM_StartOnce();",StringComparison.Ordinal);
 internal const string Runtime="""
#ifndef ENGINE_MIGRATOR_LATE_RUNTIME
#define ENGINE_MIGRATOR_LATE_RUNTIME
struct FEMLateRuntime {
 using FQueue=TMap<TWeakObjectPtr<UWorld>,TArray<TWeakObjectPtr<AActor>>>;
 static FQueue& Worlds(){static FQueue Data;return Data;}
 static FQueue& PreStarts(){static FQueue Data;return Data;}
 static FQueue& PostStarts(){static FQueue Data;return Data;}
 static TWeakObjectPtr<UWorld>& DispatchWorld(){static TWeakObjectPtr<UWorld> Value;return Value;}
 static TWeakObjectPtr<UWorld>& PreStartWorld(){static TWeakObjectPtr<UWorld> Value;return Value;}
 static bool IsDispatching(UWorld* World){return DispatchWorld().Get()==World;}
 static int32 Order(AActor* Actor){for(const FName& Tag:Actor->Tags){FString Text=Tag.ToString();if(Text.StartsWith(TEXT("EMScriptOrder:")))return FCString::Atoi(*Text.RightChop(14));}return 0;}
 static void QueueStart(AActor* Actor){UWorld* World=Actor->GetWorld();const bool Before=!World->bInTick||IsDispatching(World)||PreStartWorld().Get()==World;(Before?PreStarts():PostStarts()).FindOrAdd(TWeakObjectPtr<UWorld>(World)).AddUnique(Actor);}
 static void ForgetStart(AActor* Actor){const TWeakObjectPtr<UWorld> Key=Actor->GetWorld();const TWeakObjectPtr<AActor> Value=Actor;for(FQueue* Queues:{&PreStarts(),&PostStarts()})if(auto* Queue=Queues->Find(Key))Queue->Remove(Value);}
 static void StartActor(AActor* Actor,bool Before){if(IsValid(Actor))if(UFunction* Function=Actor->FindFunction(TEXT("EM_RuntimeStartNow"))){struct{bool BeforeUpdate;}Args{Before};Actor->ProcessEvent(Function,&Args);}}
 static void FlushStarts(UWorld* World,bool Before){
  TGuardValue<TWeakObjectPtr<UWorld>> Guard(PreStartWorld(),Before?TWeakObjectPtr<UWorld>(World):PreStartWorld());FQueue& Queues=Before?PreStarts():PostStarts();const TWeakObjectPtr<UWorld> Key=World;
  if(!Before){auto* Pending=Queues.Find(Key);if(!Pending)return;auto Batch=*Pending;Queues.Remove(Key);Batch.RemoveAll([](const TWeakObjectPtr<AActor>& Value){return !Value.IsValid();});Batch.StableSort([](const TWeakObjectPtr<AActor>& A,const TWeakObjectPtr<AActor>& B){return Order(A.Get())<Order(B.Get());});for(const auto& Weak:Batch){if(!IsValid(World)||World->bIsTearingDown)return;StartActor(Weak.Get(),false);}return;}
  while(IsValid(World)&&!World->bIsTearingDown){auto* Queue=Queues.Find(Key);if(!Queue)return;Queue->RemoveAll([](const TWeakObjectPtr<AActor>& Value){return !Value.IsValid();});if(Queue->Num()==0){Queues.Remove(Key);return;}Queue->StableSort([](const TWeakObjectPtr<AActor>& A,const TWeakObjectPtr<AActor>& B){return Order(A.Get())<Order(B.Get());});TWeakObjectPtr<AActor> Weak=(*Queue)[0];Queue->RemoveAt(0);AActor* Actor=Weak.Get();if(IsValid(Actor))if(UFunction* Function=Actor->FindFunction(TEXT("EM_RuntimeStartNow"))){struct{bool BeforeUpdate;}Args{Before};Actor->ProcessEvent(Function,&Args);}}
 }
 static void Cleanup(UWorld* World,bool,bool){const TWeakObjectPtr<UWorld> Key=World;Worlds().Remove(Key);PreStarts().Remove(Key);PostStarts().Remove(Key);}
 static void PreTick(UWorld* World,ELevelTick Type,float){if(Type==LEVELTICK_All&&GFrameCounter!=0&&(World->WorldType==EWorldType::Game||World->WorldType==EWorldType::PIE))FlushStarts(World,true);}
 static void Tick(UWorld* World,ELevelTick Type,float Delta){
  if(Type!=LEVELTICK_All||GFrameCounter==0||(World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE))return;FlushStarts(World,false);auto* State=Worlds().Find(TWeakObjectPtr<UWorld>(World));if(!State)return;State->RemoveAll([](const TWeakObjectPtr<AActor>& Value){return !Value.IsValid();});auto Actors=*State;
  Actors.RemoveAll([](const TWeakObjectPtr<AActor>& Weak){AActor* Actor=Weak.Get();if(!IsValid(Actor))return true;UFunction* Function=Actor->FindFunction(TEXT("EM_RuntimeLateEligible"));if(!Function)return true;struct{bool ReturnValue;}Args{false};Actor->ProcessEvent(Function,&Args);return !Args.ReturnValue;});
  Actors.StableSort([](const TWeakObjectPtr<AActor>& A,const TWeakObjectPtr<AActor>& B){return Order(A.Get())<Order(B.Get());});TGuardValue<TWeakObjectPtr<UWorld>> Guard(DispatchWorld(),TWeakObjectPtr<UWorld>(World));
  for(const auto& Weak:Actors){if(World->bIsTearingDown)return;AActor* Actor=Weak.Get();if(!IsValid(Actor))continue;if(UFunction* Function=Actor->FindFunction(TEXT("EM_RuntimeLateTick"))){struct{float Delta;}Args{Delta};Actor->ProcessEvent(Function,&Args);}}
 }
 static void Register(AActor* Actor){UWorld* World=Actor->GetWorld();if((World->WorldType!=EWorldType::Game&&World->WorldType!=EWorldType::PIE)||Actor->Tags.Contains(TEXT("EMRuntimeTemplate")))return;Worlds().FindOrAdd(TWeakObjectPtr<UWorld>(World)).AddUnique(Actor);static bool Bound=false;if(!Bound){Bound=true;FWorldDelegates::OnWorldPreActorTick.AddStatic(&PreTick);FWorldDelegates::OnWorldPostActorTick.AddStatic(&Tick);FWorldDelegates::OnWorldCleanup.AddStatic(&Cleanup);}}
};
#endif

""";
 internal const string PreviousTickOrder="void EM_ApplyTickOrder(){TArray<AActor*> EM_All;UGameplayStatics::GetAllActorsOfClass(this,AActor::StaticClass(),EM_All);for(AActor* EM_Actor:EM_All)if(EM_Actor!=this&&EM_Actor->GetClass()->FindPropertyByName(TEXT(\"EM_BehaviourEnabled\"))&&EM_ScriptOrder(EM_Actor)<EM_ScriptOrder(this))AddTickPrerequisiteActor(EM_Actor);}";
 internal const string TickOrder="void EM_ApplyTickOrder(){TArray<AActor*> EM_All;UGameplayStatics::GetAllActorsOfClass(this,AActor::StaticClass(),EM_All);for(AActor* EM_Actor:EM_All)if(EM_Actor!=this&&EM_Actor->GetClass()->FindPropertyByName(TEXT(\"EM_BehaviourEnabled\"))){const int32 EM_Other=EM_ScriptOrder(EM_Actor),EM_This=EM_ScriptOrder(this);if(EM_Other<EM_This)AddTickPrerequisiteActor(EM_Actor);else if(EM_Other>EM_This)EM_Actor->AddTickPrerequisiteActor(this);}}";
 internal const string BeginSuffix=" FEMLateRuntime::Register(this);";
}
