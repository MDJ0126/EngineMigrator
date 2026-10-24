namespace EngineMigrator.Core;
internal static class RuntimeUiPrefabPorting
{
    internal static string RuntimeHelpers=>RuntimeAnimationPrefabPorting.RuntimeHelpers
        .Replace(" EM_CollectRuntimeAnimations(EM_Originals);"," EM_CollectRuntimeAnimations(EM_Originals);EM_CollectRuntimeUI(EM_Originals);")
        .Replace(" for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning", " for(int32 EM_I=0;EM_I<EM_Copies.Num();++EM_I)if(UFunction* EM_Function=EM_Copies[EM_I]->FindFunction(TEXT(\"EM_ConfigureRuntimeUI\"))){struct{AActor* Owner;TArray<AActor*> Originals;TArray<AActor*> Copies;} EM_Args{EM_Originals[EM_I]->GetOwner(),EM_Originals,EM_Copies};EM_Copies[EM_I]->ProcessEvent(EM_Function,&EM_Args);}\n for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning")
        .Replace("EM_CollectRuntimeAnimations(EM_All);","EM_CollectRuntimeAnimations(EM_All);EM_CollectRuntimeUI(EM_All);")+UiHelpers;
    internal static string Helpers(string name)=>RuntimeAnimationPrefabPorting.Helpers(name).Replace("EM_NotifyRuntimeAnimationActivation(EM_Actor,EM_Active);","EM_NotifyRuntimeAnimationActivation(EM_Actor,EM_Active);EM_RefreshRuntimeUI(EM_Actor);");
    private const string UiHelpers="""
static void EM_CollectRuntimeUI(TArray<AActor*>& EM_All){
 if(!EM_All.Num())return;TArray<AActor*> EM_Actors;UGameplayStatics::GetAllActorsOfClass(EM_All[0],AActor::StaticClass(),EM_Actors);
 for(AActor* EM_Actor:EM_Actors)if(EM_Actor->Tags.Contains(TEXT("EMRuntimeUI"))&&EM_All.Contains(EM_Actor->GetOwner()))EM_All.AddUnique(EM_Actor);
}
static void EM_RefreshRuntimeUI(UObject* EM_Context){
 TArray<AActor*> EM_Actors;UGameplayStatics::GetAllActorsOfClass(EM_Context,AActor::StaticClass(),EM_Actors);
 for(AActor* EM_Actor:EM_Actors)if(EM_Actor->Tags.Contains(TEXT("EMRuntimeUI")))if(UFunction* EM_Function=EM_Actor->FindFunction(TEXT("EM_RefreshRuntimeUI")))EM_Actor->ProcessEvent(EM_Function,nullptr);
}

""";
}
