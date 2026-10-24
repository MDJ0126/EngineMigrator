namespace EngineMigrator.Core;
internal static class WholeObjectDestructionPorting
{
    internal static string Helpers(string name)=>StartupLifecyclePorting.Helpers(name)
        .Replace("||EM_Finalized||EM_PendingDestroy)return;EM_EnableNotified", "||EM_Finalized||(EM_PendingDestroy&&!EM_GameObjectPending))return;EM_EnableNotified")
        .Replace("EM_Actor->EM_StartOnce();", "if(!EM_RunBefore)EM_Actor->Tags.AddUnique(TEXT(\"EMPostStartDispatch\"));EM_Actor->EM_StartOnce();if(IsValid(EM_Actor)&&!EM_RunBefore)EM_Actor->Tags.Remove(TEXT(\"EMPostStartDispatch\"));")
        + """
public:
UFUNCTION()
void EM_PrepareGameObjectDestroy(){EM_GameObjectPending=true;EM_PrepareDestroy();}
UFUNCTION()
void EM_FinalizeQueuedDestroy(){EM_FinalizeDestroy();}
protected:
bool EM_GameObjectPending=false;

""";
    internal static string RuntimeHelpers
    {
        get
        {
            var code=StartupLifecyclePorting.RuntimeHelpers.Replace("EM_New->Tags.Remove(TEXT(\"EMStartupDispatched\"));","EM_New->Tags.Remove(TEXT(\"EMStartupDispatched\"));EM_New->Tags.Remove(TEXT(\"EMPostStartDispatch\"));EM_New->Tags.Remove(TEXT(\"EMDestroyQueued\"));");
            var begin=code.IndexOf("void EM_Destroy(AActor* EM_Value) {",StringComparison.Ordinal);var end=code.IndexOf("void EM_LoadScene(",begin,StringComparison.Ordinal);
            if(begin<0||end<0)throw new InvalidOperationException("검증된 삭제 보일러플레이트를 찾을 수 없습니다.");
            return code[..begin]+QueueHelpers+code[end..];
        }
    }
    private const string QueueHelpers="""
bool EM_IsPostStartDispatch(){TArray<AActor*> EM_Actors;UGameplayStatics::GetAllActorsOfClass(this,AActor::StaticClass(),EM_Actors);for(AActor* EM_Actor:EM_Actors)if(EM_Actor->Tags.Contains(TEXT("EMPostStartDispatch")))return true;return false;}
static void EM_CollectDestroyActors(AActor* EM_Root,TArray<AActor*>& EM_All){EM_All.Add(EM_Root);for(int32 EM_i=0;EM_i<EM_All.Num();++EM_i){TArray<AActor*> EM_Children;EM_All[EM_i]->GetAttachedActors(EM_Children);EM_All.Append(EM_Children);}}
static void EM_CommitDestroy(AActor* EM_Root,bool EM_Whole){
 TArray<AActor*> EM_All;EM_CollectDestroyActors(EM_Root,EM_All);
 if(EM_Whole){EM_Root->Tags.AddUnique(TEXT("EMInactiveSelf"));EM_Root->Tags.Remove(TEXT("EMHierarchySuppressed"));EM_RefreshObjectTree(EM_Root);}
 for(AActor* EM_Actor:EM_All)if(IsValid(EM_Actor))if(UFunction* EM_Function=EM_Actor->FindFunction(TEXT("EM_FinalizeQueuedDestroy")))EM_Actor->ProcessEvent(EM_Function,nullptr);
 for(int32 EM_i=EM_All.Num()-1;EM_i>=0;--EM_i)if(IsValid(EM_All[EM_i]))EM_All[EM_i]->Destroy();
}
void EM_QueueDestroy(AActor* EM_Value,bool EM_Whole){
 if(!IsValid(EM_Value)||EM_Value->Tags.Contains(TEXT("EMDestroyQueued")))return;EM_Value->Tags.Add(TEXT("EMDestroyQueued"));
 TArray<AActor*> EM_All;EM_CollectDestroyActors(EM_Value,EM_All);
 for(AActor* EM_Actor:EM_All)if(UFunction* EM_Function=EM_Actor->FindFunction(EM_Whole?TEXT("EM_PrepareGameObjectDestroy"):TEXT("EM_PrepareDestroy")))EM_Actor->ProcessEvent(EM_Function,nullptr);
 const bool EM_NextPre=EM_IsPostStartDispatch();TWeakObjectPtr<AActor> EM_Pending=EM_Value;TWeakObjectPtr<UWorld> EM_World=GetWorld();auto EM_Handle=MakeShared<FDelegateHandle>();
 auto EM_Callback=[EM_Pending,EM_World,EM_Handle,EM_NextPre,EM_Whole](UWorld* EM_Ticking,ELevelTick,float){
  if(EM_Ticking!=EM_World.Get())return;AActor* EM_Root=EM_Pending.Get();FDelegateHandle EM_Remove=*EM_Handle;const bool EM_RemovePre=EM_NextPre;const bool EM_DeleteWhole=EM_Whole;
  if(EM_RemovePre)FWorldDelegates::OnWorldPreActorTick.Remove(EM_Remove);else FWorldDelegates::OnWorldPostActorTick.Remove(EM_Remove);
  if(IsValid(EM_Root))EM_CommitDestroy(EM_Root,EM_DeleteWhole);
 };
 if(EM_NextPre)*EM_Handle=FWorldDelegates::OnWorldPreActorTick.AddLambda(EM_Callback);else *EM_Handle=FWorldDelegates::OnWorldPostActorTick.AddLambda(EM_Callback);
}
void EM_Destroy(AActor* EM_Value){EM_QueueDestroy(EM_Value,false);}
void EM_DestroyGameObject(AActor* EM_Value){EM_QueueDestroy(EM_Value,true);}

""";
}
