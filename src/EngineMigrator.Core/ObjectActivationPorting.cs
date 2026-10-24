namespace EngineMigrator.Core;
internal static class ObjectActivationPorting
{
    internal static string BeginPlayBody=>BehaviourActivationPorting.BeginPlayBody.Replace("Super::BeginPlay();","Super::BeginPlay(); EM_ApplyTickOrder();").Replace("else if(EM_BehaviourEnabled)","else if(EM_BehaviourEnabled&&EM_ObjectActiveInHierarchy(EM_ComponentOwner(this)))");
    internal static string TickBody=>BehaviourActivationPorting.TickBody.Replace("&&EM_BehaviourEnabled","&&EM_ObjectActiveInHierarchy(EM_ComponentOwner(this))&&EM_BehaviourEnabled");
    internal static string Helpers(string name)=>BehaviourActivationPorting.Helpers(name)
        .Replace("if(Tags.Contains(TEXT(\"EMRuntimeTemplate\")))return;EM_Initialized=true;","if(EM_Initialized||Tags.Contains(TEXT(\"EMRuntimeTemplate\"))||!EM_ObjectActiveInHierarchy(EM_ComponentOwner(this)))return;EM_Initialized=true;")
        .Replace("||!EM_BehaviourEnabled||EM_Finalized","||!EM_BehaviourEnabled||!EM_ObjectActiveInHierarchy(EM_ComponentOwner(this))||EM_Finalized")
        .Replace("if(!EM_Started&&EM_BehaviourEnabled","if(!EM_Started&&EM_ObjectActiveInHierarchy(EM_ComponentOwner(this))&&EM_BehaviourEnabled")
        + """
public:
UFUNCTION()
void EM_RefreshObjectActivation(){
 if(EM_ObjectActiveInHierarchy(EM_ComponentOwner(this))){EM_InitializeBehaviour();EM_NotifyEnable();if(HasActorBegunPlay()&&EM_BehaviourEnabled&&!EM_Started)EM_DeferStart();}
 else EM_NotifyDisable();
}
protected:
static int32 EM_ScriptOrder(AActor* EM_Actor){for(const FName& EM_Tag:EM_Actor->Tags){const FString EM_Text=EM_Tag.ToString();if(EM_Text.StartsWith(TEXT("EMScriptOrder:")))return FCString::Atoi(*EM_Text.RightChop(14));}return 0;}
void EM_ApplyTickOrder(){TArray<AActor*> EM_All;UGameplayStatics::GetAllActorsOfClass(this,AActor::StaticClass(),EM_All);for(AActor* EM_Actor:EM_All)if(EM_Actor!=this&&EM_Actor->GetClass()->FindPropertyByName(TEXT("EM_BehaviourEnabled"))&&EM_ScriptOrder(EM_Actor)<EM_ScriptOrder(this))AddTickPrerequisiteActor(EM_Actor);}
void EM_InheritScriptOrder(AActor* EM_Actor){const FString EM_Prefix=FString(TEXT("EMScriptClassOrder:"))+EM_Actor->GetClass()->GetName()+TEXT("=");for(const FName& EM_Tag:Tags){const FString EM_Text=EM_Tag.ToString();if(EM_Text.StartsWith(TEXT("EMScriptClassOrder:")))EM_Actor->Tags.AddUnique(EM_Tag);if(EM_Text.StartsWith(EM_Prefix))EM_Actor->Tags.Add(FName(*(FString(TEXT("EMScriptOrder:"))+EM_Text.RightChop(EM_Prefix.Len()))));}}
static bool EM_ObjectActiveSelf(AActor* EM_Object){return !EM_Object->Tags.Contains(TEXT("EMInactiveSelf"));}
static bool EM_ObjectActiveInHierarchy(AActor* EM_Object){for(AActor* EM_It=EM_Object;EM_It;EM_It=EM_It->GetAttachParentActor())if(!EM_ObjectActiveSelf(EM_It))return false;return true;}
static void EM_RefreshObjectTree(AActor* EM_Root){
 TArray<AActor*> EM_All;EM_All.Add(EM_Root);for(int32 EM_i=0;EM_i<EM_All.Num();++EM_i){TArray<AActor*> EM_Children;EM_All[EM_i]->GetAttachedActors(EM_Children);EM_All.Append(EM_Children);}
 for(AActor* EM_Actor:EM_All){const bool EM_Active=EM_ObjectActiveInHierarchy(EM_Actor);EM_Actor->SetActorHiddenInGame(!EM_Active);EM_Actor->SetActorEnableCollision(EM_Active);}
 for(AActor* EM_Actor:EM_All)if(UFunction* EM_Function=EM_Actor->FindFunction(TEXT("EM_RefreshObjectActivation")))EM_Actor->ProcessEvent(EM_Function,nullptr);
}
void EM_SetObjectActive(AActor* EM_Object,bool EM_Value){
 if(EM_ObjectActiveSelf(EM_Object)==EM_Value)return;
 if(EM_Value)EM_Object->Tags.Remove(TEXT("EMInactiveSelf"));else EM_Object->Tags.AddUnique(TEXT("EMInactiveSelf"));
 TArray<AActor*> EM_Children;EM_Object->GetAttachedActors(EM_Children);for(AActor* EM_Child:EM_Children)if(EM_Child!=this)EM_Child->AddTickPrerequisiteActor(this);
 EM_RefreshObjectTree(EM_Object);
}

""";
    internal static string ComponentHelpers=>RuntimeComponentPorting.Helpers.Replace("EM_New->Tags.Add(TEXT(\"EMRuntimeAdded\"));","EM_InheritScriptOrder(EM_New);EM_New->Tags.Add(TEXT(\"EMRuntimeAdded\"));");
    internal static string CreationHelpers=>RuntimeCreationPorting.Helpers.Replace(" else EM_Object->DetachFromActor(EM_World?FDetachmentTransformRules::KeepWorldTransform:FDetachmentTransformRules::KeepRelativeTransform);"," else EM_Object->DetachFromActor(EM_World?FDetachmentTransformRules::KeepWorldTransform:FDetachmentTransformRules::KeepRelativeTransform);\n EM_RefreshObjectTree(EM_Object);");
}
