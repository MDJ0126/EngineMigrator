namespace EngineMigrator.Core;
internal static class PhaseLifecyclePorting
{
    internal static string Helpers(string name)=>ObjectActivationPorting.Helpers(name)
        .Replace("void EM_PrepareDestroy(){EM_PendingDestroy=true;EM_BehaviourEnabled=false;EM_NotifyDisable();}","void EM_PrepareDestroy(){const bool EM_EarlyDisable=EM_InAwake&&EM_Initialized&&!EM_PendingDestroy&&!EM_EnableNotified&&EM_BehaviourEnabled&&EM_ObjectActiveInHierarchy(EM_ComponentOwner(this));EM_PendingDestroy=true;EM_BehaviourEnabled=false;if(EM_EarlyDisable)EM_OnDisable();else EM_NotifyDisable();}")
        .Replace(DeferredStart(name),PreOrPostStart(name));
    private static string DeferredStart(string name)=>"""
void EM_DeferStart(){
 if(EM_StartQueued||EM_Started)return;EM_StartQueued=true;
 TWeakObjectPtr<EM_CLASS> EM_Self=this;TWeakObjectPtr<UWorld> EM_World=GetWorld();auto EM_Handle=MakeShared<FDelegateHandle>();
 *EM_Handle=FWorldDelegates::OnWorldPostActorTick.AddLambda([EM_Self,EM_World,EM_Handle](UWorld* EM_Ticking,ELevelTick,float){
  if(EM_Ticking!=EM_World.Get())return;EM_CLASS* EM_Actor=EM_Self.Get();FDelegateHandle EM_Remove=*EM_Handle;
  FWorldDelegates::OnWorldPostActorTick.Remove(EM_Remove);if(IsValid(EM_Actor)){EM_Actor->EM_StartQueued=false;EM_Actor->EM_StartOnce();}
 });
}
""".Replace("EM_CLASS","A"+name);
    private static string PreOrPostStart(string name)=>"""
void EM_DeferStart(){
 if(EM_StartQueued||EM_Started)return;EM_StartQueued=true;const bool EM_BeforeUpdate=!GetWorld()->bInTick;
 TWeakObjectPtr<EM_CLASS> EM_Self=this;TWeakObjectPtr<UWorld> EM_World=GetWorld();auto EM_Handle=MakeShared<FDelegateHandle>();
 auto EM_Callback=[EM_Self,EM_World,EM_Handle,EM_BeforeUpdate](UWorld* EM_Ticking,ELevelTick,float){
  if(EM_Ticking!=EM_World.Get())return;EM_CLASS* EM_Actor=EM_Self.Get();FDelegateHandle EM_Remove=*EM_Handle;const bool EM_RunBefore=EM_BeforeUpdate;
  if(EM_RunBefore)FWorldDelegates::OnWorldPreActorTick.Remove(EM_Remove);else FWorldDelegates::OnWorldPostActorTick.Remove(EM_Remove);
  if(IsValid(EM_Actor)){EM_Actor->EM_StartQueued=false;if(EM_RunBefore)EM_Actor->EM_EnabledFrame=MAX_uint64;EM_Actor->EM_StartOnce();}
 };
 if(EM_BeforeUpdate)*EM_Handle=FWorldDelegates::OnWorldPreActorTick.AddLambda(EM_Callback);else *EM_Handle=FWorldDelegates::OnWorldPostActorTick.AddLambda(EM_Callback);
}
""".Replace("EM_CLASS","A"+name);
}
