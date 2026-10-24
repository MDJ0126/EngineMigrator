namespace EngineMigrator.Core;
internal static class BehaviourActivationPorting
{
    internal const string InitializeBody="Super::PostInitializeComponents(); EM_InitializeBehaviour();";
    internal const string BeginPlayBody="Super::BeginPlay(); if(Tags.Contains(TEXT(\"EMRuntimeTemplate\")))SetActorTickEnabled(false);else if(EM_BehaviourEnabled){if(Tags.Contains(TEXT(\"EMRuntimeAdded\"))||Tags.Contains(TEXT(\"EMRuntimeClone\")))EM_DeferStart();else EM_StartOnce();}";
    internal const string TickBody="Super::Tick(DeltaSeconds); if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))&&EM_BehaviourEnabled&&EM_Started&&!EM_PendingDestroy&&EM_EnabledFrame!=GFrameCounter)EM_Update(DeltaSeconds);";
    internal const string Finalize="bool EM_Initialized = false;\nbool EM_Finalized = false;\nvoid EM_FinalizeDestroy() { if (EM_Initialized && !EM_Finalized) { EM_Finalized = true; EM_NotifyDisable(); EM_OnDestroy(); } }\n";
    internal static string Helpers(string name)=>"""
public:
UPROPERTY(EditAnywhere, Category="EngineMigrator")
bool EM_BehaviourEnabled=true;
UFUNCTION()
void EM_SetBehaviourEnabled(bool EM_Value){
 if(EM_BehaviourEnabled==EM_Value)return;EM_BehaviourEnabled=EM_Value;if(!EM_Initialized||EM_InAwake)return;
 if(EM_Value){EM_NotifyEnable();if(HasActorBegunPlay()&&!EM_Started)EM_DeferStart();}else EM_NotifyDisable();
}
UFUNCTION()
void EM_PrepareDestroy(){EM_PendingDestroy=true;EM_BehaviourEnabled=false;EM_NotifyDisable();}
protected:
bool EM_Started=false;
bool EM_StartQueued=false;
bool EM_EnableNotified=false;
bool EM_InAwake=false;
bool EM_PendingDestroy=false;
uint64 EM_EnabledFrame=MAX_uint64;
void EM_InitializeBehaviour(){if(Tags.Contains(TEXT("EMRuntimeTemplate")))return;EM_Initialized=true;EM_InAwake=true;EM_Awake();EM_InAwake=false;if(EM_BehaviourEnabled)EM_NotifyEnable();}
void EM_NotifyEnable(){if(!EM_Initialized||EM_InAwake||EM_EnableNotified||!EM_BehaviourEnabled||EM_Finalized||EM_PendingDestroy)return;EM_EnableNotified=true;if(HasActorBegunPlay())EM_EnabledFrame=GFrameCounter;EM_OnEnable();}
void EM_NotifyDisable(){if(!EM_EnableNotified)return;EM_EnableNotified=false;EM_OnDisable();}
void EM_StartOnce(){if(!EM_Started&&EM_BehaviourEnabled&&!EM_Finalized&&!EM_PendingDestroy){EM_Started=true;EM_Start();}}
void EM_DeferStart(){
 if(EM_StartQueued||EM_Started)return;EM_StartQueued=true;
 TWeakObjectPtr<EM_CLASS> EM_Self=this;TWeakObjectPtr<UWorld> EM_World=GetWorld();auto EM_Handle=MakeShared<FDelegateHandle>();
 *EM_Handle=FWorldDelegates::OnWorldPostActorTick.AddLambda([EM_Self,EM_World,EM_Handle](UWorld* EM_Ticking,ELevelTick,float){
  if(EM_Ticking!=EM_World.Get())return;EM_CLASS* EM_Actor=EM_Self.Get();FDelegateHandle EM_Remove=*EM_Handle;
  FWorldDelegates::OnWorldPostActorTick.Remove(EM_Remove);if(IsValid(EM_Actor)){EM_Actor->EM_StartQueued=false;EM_Actor->EM_StartOnce();}
 });
}
static bool EM_ReadBehaviourEnabled(AActor* EM_Object){return CastFieldChecked<FBoolProperty>(EM_Object->GetClass()->FindPropertyByName(TEXT("EM_BehaviourEnabled")))->GetPropertyValue_InContainer(EM_Object);}
void EM_WriteBehaviourEnabled(AActor* EM_Object,bool EM_Value){if(EM_Object!=this)EM_Object->AddTickPrerequisiteActor(this);EM_ComponentCall<void>(EM_Object,TEXT("EM_SetBehaviourEnabled"),EM_Value);}

""".Replace("EM_CLASS","A"+name);
    internal static string RuntimeHelpers=>RuntimeObjectPorting.Helpers.Replace(" if (!IsValid(EM_Value)) return;", " if (!IsValid(EM_Value)) return;\n TArray<AActor*> EM_Disabling;EM_Disabling.Add(EM_Value);for(int32 EM_i=0;EM_i<EM_Disabling.Num();++EM_i){TArray<AActor*> EM_Children;EM_Disabling[EM_i]->GetAttachedActors(EM_Children);EM_Disabling.Append(EM_Children);}\n for(AActor* EM_Target:EM_Disabling)if(UFunction* EM_Function=EM_Target->FindFunction(TEXT(\"EM_PrepareDestroy\")))EM_Target->ProcessEvent(EM_Function,nullptr);");
}
