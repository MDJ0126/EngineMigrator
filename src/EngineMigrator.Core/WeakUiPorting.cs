namespace EngineMigrator.Core;
internal static class WeakUiPorting
{
    private const string CloneFinish=" for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning";
    private const string CloneHook="""
 for(int32 EM_I=0;EM_I<EM_Copies.Num();++EM_I){AActor* EM_Copy=EM_Copies[EM_I];EM_Copy->Tags.RemoveAll([](const FName& EM_Tag){return EM_Tag.ToString().StartsWith(TEXT("EMUIRemoveQueued:"));});for(UClass* EM_Class=EM_Copy->GetClass();EM_Class;EM_Class=EM_Class->GetSuperClass())if(UFunction* EM_Function=EM_Copy->FindFunction(FName(*(FString(TEXT("EM_NormalizeClonedUI_"))+EM_Class->GetName())))){struct{AActor* Source;}EM_Args{EM_Originals[EM_I]};EM_Copy->ProcessEvent(EM_Function,&EM_Args);}}
 for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning
""";
    internal static string AdaptRuntime(string value)=>value.Replace(CloneFinish,CloneHook);
    internal static string RestoreRuntime(string value)=>value.Replace(CloneHook,CloneFinish);
    internal static string ClassHelpers(string name,IEnumerable<(string Name,string Kind)> fields)
    {
        var statements=fields.Select(f=>"if(!EM_UIValid("+f.Name+",TEXT(\""+f.Kind+"\")))"+f.Name+"="+(f.Kind=="Canvas"?"EM_Old->"+f.Name:"nullptr")+";").ToArray();
        return statements.Length==0?"":"public:\nUFUNCTION()\nvoid EM_NormalizeClonedUI_"+name+"(AActor* EM_Source){auto* EM_Old=Cast<A"+name+">(EM_Source);if(!EM_Old)return;"+string.Join("",statements)+"}\n";
    }
    internal static string Adapt(string value)=>value.Replace("using EM_UIRef_Canvas=AActor*;","using EM_UIRef_Canvas=TWeakObjectPtr<AActor>;").Replace("using EM_UIRef_Button=AActor*;","using EM_UIRef_Button=TWeakObjectPtr<AActor>;");
    internal static string Restore(string value)=>value.Replace("using EM_UIRef_Canvas=TWeakObjectPtr<AActor>;","using EM_UIRef_Canvas=AActor*;").Replace("using EM_UIRef_Button=TWeakObjectPtr<AActor>;","using EM_UIRef_Button=AActor*;");
    internal static string Helpers=>LegacyHelpers.Replace("FWorldDelegates::OnWorldPostActorTick.Remove(*EM_Handle);if(AActor* EM_Actor=EM_Target.Get()){EM_Actor->Tags.Remove(EM_PendingTag);EM_Actor->Tags.Remove(EM_ComponentTag);EM_Actor->Tags.AddUnique(EM_Canvas?", "AActor* EM_Actor=EM_Target.Get();const FName EM_RemovePending=EM_PendingTag;const FName EM_RemoveComponent=EM_ComponentTag;const bool EM_DisableCanvas=EM_Canvas;const FDelegateHandle EM_RemoveHandle=*EM_Handle;FWorldDelegates::OnWorldPostActorTick.Remove(EM_RemoveHandle);if(EM_Actor){EM_Actor->Tags.Remove(EM_RemovePending);EM_Actor->Tags.Remove(EM_RemoveComponent);EM_Actor->Tags.AddUnique(EM_DisableCanvas?");
    internal const string LegacyHelpers="""
protected:
static bool EM_UIValid(const TWeakObjectPtr<AActor>& EM_Value,const TCHAR* EM_Kind){AActor* EM_Owner=EM_Value.Get();return IsValid(EM_Owner)&&EM_Owner->Tags.Contains(FName(FString(TEXT("EMUI:"))+EM_Kind));}
static bool EM_UIEqual(const TWeakObjectPtr<AActor>& EM_A,const TCHAR* EM_AKind,const TWeakObjectPtr<AActor>& EM_B,const TCHAR* EM_BKind){if(EM_A.IsExplicitlyNull())return !EM_UIValid(EM_B,EM_BKind);if(EM_B.IsExplicitlyNull())return !EM_UIValid(EM_A,EM_AKind);return FCString::Strcmp(EM_AKind,EM_BKind)==0&&EM_A.HasSameIndexAndSerialNumber(EM_B);}
void EM_QueueUIDestroy(AActor* EM_Owner,const TCHAR* EM_Kind){
 if(!IsValid(EM_Owner))return;const FName EM_ComponentTag(FString(TEXT("EMUI:"))+EM_Kind);const FName EM_PendingTag(FString(TEXT("EMUIRemoveQueued:"))+EM_Kind);
 if(!EM_Owner->Tags.Contains(EM_ComponentTag)||EM_Owner->Tags.Contains(EM_PendingTag))return;EM_Owner->Tags.AddUnique(EM_PendingTag);
 const bool EM_Canvas=FCString::Strcmp(EM_Kind,TEXT("Canvas"))==0;TWeakObjectPtr<AActor> EM_Target=EM_Owner;TWeakObjectPtr<UWorld> EM_World=EM_Owner->GetWorld();auto EM_Handle=MakeShared<FDelegateHandle>();
 *EM_Handle=FWorldDelegates::OnWorldPostActorTick.AddLambda([EM_Target,EM_World,EM_Handle,EM_ComponentTag,EM_PendingTag,EM_Canvas](UWorld* EM_Ticking,ELevelTick,float){
  if(EM_Ticking!=EM_World.Get()){if(!EM_World.IsValid())FWorldDelegates::OnWorldPostActorTick.Remove(*EM_Handle);return;}
  FWorldDelegates::OnWorldPostActorTick.Remove(*EM_Handle);if(AActor* EM_Actor=EM_Target.Get()){EM_Actor->Tags.Remove(EM_PendingTag);EM_Actor->Tags.Remove(EM_ComponentTag);EM_Actor->Tags.AddUnique(EM_Canvas?FName(TEXT("EMUICanvasDisabled")):FName(TEXT("EMUIButtonDisabled")));EM_RefreshRuntimeUI(EM_Actor);}
 });
}
void EM_DestroyCanvas(AActor* EM_Owner){EM_QueueUIDestroy(EM_Owner,TEXT("Canvas"));}
void EM_DestroyButton(AActor* EM_Owner){EM_QueueUIDestroy(EM_Owner,TEXT("Button"));}

""";
}
