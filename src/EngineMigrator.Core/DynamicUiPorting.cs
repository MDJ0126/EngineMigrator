namespace EngineMigrator.Core;

internal static class DynamicUiPorting
{
    internal static string PreviousHelpers=>WeakUiPorting.Adapt(UiComponentPorting.RuntimeHelpers)+WeakUiPorting.Helpers;
    internal static string RuntimeHelpers=>PreviousHelpers
        .Replace(WeakUiPorting.Helpers,Helpers)
        .Replace("return IsValid(EM_Owner)&&EM_Owner->Tags.Contains(TEXT(\"EMUI:Canvas\"))?EM_Owner:nullptr;","return EM_UIFind(EM_Owner,TEXT(\"Canvas\"));")
        .Replace("return IsValid(EM_Owner)&&EM_Owner->Tags.Contains(TEXT(\"EMUI:Button\"))?EM_Owner:nullptr;","return EM_UIFind(EM_Owner,TEXT(\"Button\"));")
        .Replace("return !EM_Owner->Tags.Contains(TEXT(\"EMUICanvasDisabled\"));","return !EM_UIOwner(EM_Owner)->Tags.Contains(TEXT(\"EMUICanvasDisabled\"));")
        .Replace("return !EM_Owner->Tags.Contains(TEXT(\"EMUIButtonNonInteractable\"));","return !EM_UIOwner(EM_Owner)->Tags.Contains(TEXT(\"EMUIButtonNonInteractable\"));")
        .Replace("return !EM_Owner->Tags.Contains(TEXT(\"EMUIButtonDisabled\"));","return !EM_UIOwner(EM_Owner)->Tags.Contains(TEXT(\"EMUIButtonDisabled\"));")
        .Replace("if(EM_Enabled)EM_Owner->Tags.Remove(FName(EM_Tag));","EM_Owner=EM_UIOwner(EM_Owner);if(EM_Enabled)EM_Owner->Tags.Remove(FName(EM_Tag));")
        .Replace("static AActor* EM_UiOwner(AActor* EM_Owner){return EM_Owner;}","static AActor* EM_UiOwner(AActor* EM_Owner){return EM_UIOwner(EM_Owner);}");

    private const string Helpers="""
protected:
static AActor* EM_UIOwner(AActor* EM_Value){return IsValid(EM_Value)&&EM_Value->Tags.Contains(TEXT("EMUIProxy"))?EM_Value->GetAttachParentActor():EM_Value;}
static AActor* EM_UIFind(AActor* EM_Owner,const TCHAR* EM_Kind){
 if(!IsValid(EM_Owner))return nullptr;const FName EM_Dynamic(FString(TEXT("EMUIDynamic:"))+EM_Kind);
 if(EM_Owner->Tags.Contains(EM_Dynamic)){TArray<AActor*> EM_Children;EM_Owner->GetAttachedActors(EM_Children);const FName EM_Tag(FString(TEXT("EMUIRef:"))+EM_Kind);for(AActor* EM_Child:EM_Children)if(IsValid(EM_Child)&&EM_Child->Tags.Contains(EM_Tag))return EM_Child;return nullptr;}
 return EM_Owner->Tags.Contains(FName(FString(TEXT("EMUI:"))+EM_Kind))?EM_Owner:nullptr;
}
static bool EM_UIValid(const TWeakObjectPtr<AActor>& EM_Value,const TCHAR* EM_Kind){AActor* EM_Ref=EM_Value.Get();if(!IsValid(EM_Ref))return false;AActor* EM_Owner=EM_UIOwner(EM_Ref);return IsValid(EM_Owner)&&EM_UIFind(EM_Owner,EM_Kind)==EM_Ref;}
static bool EM_UIEqual(const TWeakObjectPtr<AActor>& EM_A,const TCHAR* EM_AKind,const TWeakObjectPtr<AActor>& EM_B,const TCHAR* EM_BKind){if(EM_A.IsExplicitlyNull())return !EM_UIValid(EM_B,EM_BKind);if(EM_B.IsExplicitlyNull())return !EM_UIValid(EM_A,EM_AKind);return FCString::Strcmp(EM_AKind,EM_BKind)==0&&EM_A.HasSameIndexAndSerialNumber(EM_B);}
AActor* EM_UIAdd(AActor* EM_Owner,const TCHAR* EM_Kind){
 if(!IsValid(EM_Owner)||EM_UIFind(EM_Owner,EM_Kind))return nullptr;
 FActorSpawnParameters EM_Params;EM_Params.SpawnCollisionHandlingOverride=ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
 AActor* EM_Ref=GetWorld()->SpawnActor<AActor>(AActor::StaticClass(),EM_Owner->GetActorTransform(),EM_Params);if(!EM_Ref)return nullptr;
 auto* EM_Root=NewObject<USceneComponent>(EM_Ref);EM_Ref->SetRootComponent(EM_Root);EM_Root->RegisterComponent();EM_Ref->AttachToActor(EM_Owner,FAttachmentTransformRules::KeepWorldTransform);
 EM_Ref->Tags={TEXT("EMUIProxy"),FName(FString(TEXT("EMUIRef:"))+EM_Kind)};
 EM_Owner->Tags.AddUnique(FName(FString(TEXT("EMUIDynamic:"))+EM_Kind));EM_Owner->Tags.AddUnique(FName(FString(TEXT("EMUI:"))+EM_Kind));
 if(FCString::Strcmp(EM_Kind,TEXT("Canvas"))==0)EM_Owner->Tags.Remove(TEXT("EMUICanvasDisabled"));else{EM_Owner->Tags.Remove(TEXT("EMUIButtonDisabled"));EM_Owner->Tags.Remove(TEXT("EMUIButtonNonInteractable"));}
 EM_RefreshRuntimeUI(EM_Owner);return EM_Ref;
}
AActor* EM_UiAddCanvas(AActor* EM_Owner){return EM_UIAdd(EM_Owner,TEXT("Canvas"));}
AActor* EM_UiAddButton(AActor* EM_Owner){return EM_UIAdd(EM_Owner,TEXT("Button"));}
void EM_QueueUIDestroy(AActor* EM_Ref,const TCHAR* EM_Kind){
 if(!IsValid(EM_Ref)||!EM_UIValid(TWeakObjectPtr<AActor>(EM_Ref),EM_Kind))return;AActor* EM_Owner=EM_UIOwner(EM_Ref);const FName EM_Pending(FString(TEXT("EMUIRemoveQueued:"))+EM_Kind);if(EM_Ref->Tags.Contains(EM_Pending))return;EM_Ref->Tags.AddUnique(EM_Pending);
 TWeakObjectPtr<AActor> EM_Target=EM_Ref;TWeakObjectPtr<AActor> EM_Parent=EM_Owner;TWeakObjectPtr<UWorld> EM_World=GetWorld();FString EM_KindName=EM_Kind;auto EM_Handle=MakeShared<FDelegateHandle>();
 *EM_Handle=FWorldDelegates::OnWorldPostActorTick.AddLambda([EM_Target,EM_Parent,EM_World,EM_KindName,EM_Pending,EM_Handle](UWorld* EM_Ticking,ELevelTick,float){
 if(EM_Ticking!=EM_World.Get()){if(!EM_World.IsValid())FWorldDelegates::OnWorldPostActorTick.Remove(*EM_Handle);return;}
 AActor* EM_Value=EM_Target.Get();AActor* EM_Owner=EM_Parent.Get();const FString EM_Kind=EM_KindName;const FName EM_RemovePending=EM_Pending;const FDelegateHandle EM_RemoveHandle=*EM_Handle;FWorldDelegates::OnWorldPostActorTick.Remove(EM_RemoveHandle);
 if(!IsValid(EM_Value)||!IsValid(EM_Owner))return;EM_Value->Tags.Remove(EM_RemovePending);EM_Owner->Tags.Remove(FName(FString(TEXT("EMUI:"))+EM_Kind));EM_Owner->Tags.AddUnique(EM_Kind==TEXT("Canvas")?FName(TEXT("EMUICanvasDisabled")):FName(TEXT("EMUIButtonDisabled")));
 if(EM_Value->Tags.Contains(TEXT("EMUIProxy"))){EM_Value->Tags.Remove(FName(FString(TEXT("EMUIRef:"))+EM_Kind));EM_Value->Destroy();}EM_RefreshRuntimeUI(EM_Owner);
 });
}
void EM_DestroyCanvas(AActor* EM_Ref){EM_QueueUIDestroy(EM_Ref,TEXT("Canvas"));}
void EM_DestroyButton(AActor* EM_Ref){EM_QueueUIDestroy(EM_Ref,TEXT("Button"));}

""";
}
