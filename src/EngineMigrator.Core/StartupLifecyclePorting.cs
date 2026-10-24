namespace EngineMigrator.Core;
internal static class StartupLifecyclePorting
{
    internal static string RuntimeHelpers=>BehaviourActivationPorting.RuntimeHelpers.Replace(" EM_New->Tags.AddUnique(TEXT(\"EMRuntimeClone\"));"," EM_New->Tags.Remove(TEXT(\"EMHierarchySuppressed\"));EM_New->Tags.Remove(TEXT(\"EMDeactivationBusy\"));EM_New->Tags.Remove(TEXT(\"EMStartupDispatched\"));EM_New->Tags.AddUnique(TEXT(\"EMRuntimeClone\"));").Replace(" for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning(EM_New->GetActorTransform(),true);"," for(AActor* EM_New:EM_Copies){const bool EM_Active=EM_ObjectActiveInHierarchy(EM_New);EM_New->SetActorHiddenInGame(!EM_Active);EM_New->SetActorEnableCollision(EM_Active);}\n for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning(EM_New->GetActorTransform(),true);");
    internal const string InitializeBody="Super::PostInitializeComponents(); if(Tags.Contains(TEXT(\"EMRuntimeAdded\"))||Tags.Contains(TEXT(\"EMRuntimeClone\")))EM_InitializeBehaviour();";
    internal const string BeginPlayBody="Super::BeginPlay(); EM_ApplyTickOrder(); if(Tags.Contains(TEXT(\"EMRuntimeTemplate\")))SetActorTickEnabled(false);else if(Tags.Contains(TEXT(\"EMRuntimeAdded\"))||Tags.Contains(TEXT(\"EMRuntimeClone\"))){if(EM_BehaviourEnabled&&EM_ObjectActiveInHierarchy(EM_ComponentOwner(this)))EM_DeferStart();}else EM_DispatchStartup();";
    internal static string Helpers(string name)=>PhaseLifecyclePorting.Helpers(name)
        .Replace("void EM_NotifyDisable(){if(!EM_EnableNotified)return;EM_EnableNotified=false;EM_OnDisable();}","void EM_NotifyDisable(){if(!EM_EnableNotified)return;AActor* EM_Owner=EM_ComponentOwner(this);const bool EM_OwnBusy=!EM_ObjectActiveInHierarchy(EM_Owner)&&!EM_Owner->Tags.Contains(TEXT(\"EMDeactivationBusy\"));if(EM_OwnBusy)EM_Owner->Tags.Add(TEXT(\"EMDeactivationBusy\"));EM_EnableNotified=false;EM_OnDisable();if(EM_OwnBusy)EM_Owner->Tags.Remove(TEXT(\"EMDeactivationBusy\"));}")
        .Replace("if(!EM_ObjectActiveSelf(EM_It))return false;","if(!EM_ObjectActiveSelf(EM_It)||EM_It->Tags.Contains(TEXT(\"EMHierarchySuppressed\")))return false;")
        .Replace(" if(EM_ObjectActiveSelf(EM_Object)==EM_Value)return;\n if(EM_Value)EM_Object->Tags.Remove(TEXT(\"EMInactiveSelf\"));else EM_Object->Tags.AddUnique(TEXT(\"EMInactiveSelf\"));"," const bool EM_SameSelf=EM_ObjectActiveSelf(EM_Object)==EM_Value;if(EM_SameSelf&&!EM_Object->Tags.Contains(TEXT(\"EMHierarchySuppressed\")))return;\n if(EM_Value)EM_Object->Tags.Remove(TEXT(\"EMInactiveSelf\"));else EM_Object->Tags.AddUnique(TEXT(\"EMInactiveSelf\"));\n if(EM_Value&&EM_Object->Tags.Contains(TEXT(\"EMDeactivationBusy\"))){EM_Object->Tags.AddUnique(TEXT(\"EMHierarchySuppressed\"));UE_LOG(LogTemp,Warning,TEXT(\"EngineMigrator activation requested while GameObject is deactivating; hierarchy transition deferred\"));return;}\n EM_Object->Tags.Remove(TEXT(\"EMHierarchySuppressed\"));")
        + """
public:
UFUNCTION()
void EM_StartupInitialize(){EM_InitializeBehaviour();}
UFUNCTION()
void EM_StartupStart(){if(!GetWorld()->bInTick)EM_EnabledFrame=MAX_uint64;EM_StartOnce();}
protected:
void EM_DispatchStartup(){
 if(Tags.Contains(TEXT("EMStartupDispatched")))return;
 TArray<AActor*> EM_All;UGameplayStatics::GetAllActorsOfClass(this,AActor::StaticClass(),EM_All);
 EM_All.RemoveAll([](AActor* EM_Actor){return !EM_Actor->FindFunction(TEXT("EM_StartupInitialize"))||EM_Actor->Tags.Contains(TEXT("EMRuntimeTemplate"))||EM_Actor->Tags.Contains(TEXT("EMRuntimeAdded"))||EM_Actor->Tags.Contains(TEXT("EMRuntimeClone"));});
 EM_All.Sort([](const AActor& EM_A,const AActor& EM_B){return EM_ScriptOrder(const_cast<AActor*>(&EM_A))<EM_ScriptOrder(const_cast<AActor*>(&EM_B));});
 for(AActor* EM_Actor:EM_All)EM_Actor->Tags.AddUnique(TEXT("EMStartupDispatched"));
 for(AActor* EM_Actor:EM_All)if(IsValid(EM_Actor))EM_Actor->ProcessEvent(EM_Actor->FindFunction(TEXT("EM_StartupInitialize")),nullptr);
 for(AActor* EM_Actor:EM_All)if(IsValid(EM_Actor))EM_Actor->ProcessEvent(EM_Actor->FindFunction(TEXT("EM_StartupStart")),nullptr);
}

""";
}
