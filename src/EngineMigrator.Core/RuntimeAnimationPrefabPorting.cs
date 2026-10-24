namespace EngineMigrator.Core;
internal static class RuntimeAnimationPrefabPorting
{
    internal const string Includes="#include \"LevelSequenceActor.h\"\n#include \"LevelSequencePlayer.h\"\n#include \"MovieSceneObjectBindingID.h\"\n";
    internal static string RuntimeHelpers=>RuntimePhysicsPrefabPorting.RuntimeHelpers
        .Replace(" TMap<UObject*,UObject*> EM_Map; TArray<AActor*> EM_Copies;"," EM_CollectRuntimeAnimations(EM_Originals);\n TMap<UObject*,UObject*> EM_Map; TArray<AActor*> EM_Copies;")
        .Replace(" for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning(EM_New->GetActorTransform(),true);"," for(AActor* EM_New:EM_Copies)if(EM_New->Tags.Contains(TEXT(\"EMRuntimeAnimation\"))){EM_New->SetActorTickEnabled(false);EM_New->Tags.Remove(TEXT(\"EMAnimationDriven\"));EM_New->Tags.Remove(TEXT(\"EMAnimationReset\"));}\n for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning(EM_New->GetActorTransform(),true);\n for(int32 EM_i=0;EM_i<EM_Copies.Num();++EM_i)EM_BindRuntimeAnimation(EM_Originals[EM_i],EM_Copies[EM_i],EM_Map);")
        .Replace("EM_All.Append(EM_Children);}}","EM_All.Append(EM_Children);}EM_CollectRuntimeAnimations(EM_All);}")+AnimationHelpers;
    internal static string Helpers(string name)=>RuntimePhysicsPrefabPorting.Helpers(name).Replace("EM_RefreshRuntimePhysics(EM_Actor,EM_Active);","EM_RefreshRuntimePhysics(EM_Actor,EM_Active);EM_NotifyRuntimeAnimationActivation(EM_Actor,EM_Active);").Replace("void EM_DispatchStartup(){","void EM_DispatchStartup(){\n EM_SuppressAnimationTemplates(this);");
    private const string AnimationHelpers="""
static void EM_SuppressAnimationTemplates(UObject* EM_Context){
 TArray<AActor*> EM_Actors;UGameplayStatics::GetAllActorsOfClass(EM_Context,AActor::StaticClass(),EM_Actors);
 for(AActor* EM_Actor:EM_Actors)if(EM_Actor->Tags.Contains(TEXT("EMRuntimeAnimation"))&&EM_Actor->Tags.Contains(TEXT("EMRuntimeTemplate"))){EM_Actor->SetActorTickEnabled(false);if(auto* EM_Sequence=Cast<ALevelSequenceActor>(EM_Actor))if(auto* EM_Player=EM_Sequence->GetSequencePlayer())EM_Player->Stop();}
}
static void EM_CollectRuntimeAnimations(TArray<AActor*>& EM_All){
 if(!EM_All.Num())return;TArray<AActor*> EM_Actors;UGameplayStatics::GetAllActorsOfClass(EM_All[0],AActor::StaticClass(),EM_Actors);
 for(AActor* EM_Actor:EM_Actors)if(EM_Actor->Tags.Contains(TEXT("EMRuntimeAnimation"))&&EM_All.Contains(EM_Actor->GetOwner()))EM_All.AddUnique(EM_Actor);
}
static void EM_NotifyRuntimeAnimationActivation(AActor* EM_Owner,bool EM_Active){
 if(EM_Active)return;TArray<AActor*> EM_Actors;UGameplayStatics::GetAllActorsOfClass(EM_Owner,AActor::StaticClass(),EM_Actors);
 for(AActor* EM_Actor:EM_Actors)if(EM_Actor->GetOwner()==EM_Owner&&EM_Actor->Tags.Contains(TEXT("EMRuntimeAnimation")))EM_Actor->Tags.AddUnique(TEXT("EMAnimationReset"));
}
struct EM_AnimationClock{float Time=0;};
static void EM_BindRuntimeAnimation(AActor* EM_Old,AActor* EM_New,const TMap<UObject*,UObject*>& EM_Map){
 if(!EM_New->Tags.Contains(TEXT("EMRuntimeAnimation")))return;
 ALevelSequenceActor* EM_Sequence=Cast<ALevelSequenceActor>(EM_New);if(!EM_Sequence)return;
 UObject* const* EM_MappedOwner=EM_Map.Find(EM_Old->GetOwner());if(!EM_MappedOwner)return;AActor* EM_Owner=CastChecked<AActor>(*EM_MappedOwner);EM_Sequence->SetOwner(EM_Owner);EM_Sequence->SetActorTickEnabled(false);
 for(const FName& EM_Tag:EM_Sequence->Tags){FString EM_Text=EM_Tag.ToString();if(!EM_Text.StartsWith(TEXT("EMAnimationBinding:")))continue;FString EM_GuidText,EM_TargetId;if(!EM_Text.RightChop(19).Split(TEXT(":"),&EM_GuidText,&EM_TargetId))continue;FGuid EM_Guid;if(!FGuid::Parse(EM_GuidText,EM_Guid))continue;
  for(const auto& EM_Entry:EM_Map){AActor* EM_Target=Cast<AActor>(EM_Entry.Key);if(EM_Target&&EM_Target->Tags.Num()&&EM_Target->Tags[0]==FName(*EM_TargetId)){TArray<AActor*> EM_Targets{CastChecked<AActor>(EM_Entry.Value)};EM_Sequence->SetBinding(FMovieSceneObjectBindingID(UE::MovieScene::FRelativeObjectBindingID(EM_Guid)),EM_Targets,false);break;}}
 }
 ULevelSequencePlayer* EM_Player=EM_Sequence->GetSequencePlayer();if(!EM_Player)return;EM_Player->Stop();
 if(!EM_Sequence->Tags.Contains(TEXT("EMAnimationAutoPlay")))return;
 float EM_Length=0;bool EM_Loop=false;for(const FName& EM_Tag:EM_Sequence->Tags){const FString EM_Text=EM_Tag.ToString();if(EM_Text.StartsWith(TEXT("EMAnimationLength:")))EM_Length=FCString::Atof(*EM_Text.RightChop(18));if(EM_Text==TEXT("EMAnimationWrap:Loop"))EM_Loop=true;}
 EM_Sequence->Tags.AddUnique(TEXT("EMAnimationDriven"));auto EM_Clock=MakeShared<EM_AnimationClock>();auto EM_Handle=MakeShared<FDelegateHandle>();auto EM_Cleanup=MakeShared<FDelegateHandle>();TWeakObjectPtr<AActor> EM_ActorWeak=EM_Sequence,EM_OwnerWeak=EM_Owner;TWeakObjectPtr<ULevelSequencePlayer> EM_PlayerWeak=EM_Player;TWeakObjectPtr<UWorld> EM_World=EM_Sequence->GetWorld();UWorld* EM_OwnerWorld=EM_Sequence->GetWorld();
 *EM_Cleanup=FWorldDelegates::OnWorldCleanup.AddLambda([EM_Handle,EM_Cleanup,EM_OwnerWorld](UWorld* EM_Closing,bool,bool){if(EM_Closing!=EM_OwnerWorld)return;const FDelegateHandle EM_RemoveTick=*EM_Handle,EM_RemoveCleanup=*EM_Cleanup;UE_LOG(LogTemp,Log,TEXT("EngineMigrator runtime animation clock world cleanup"));FWorldDelegates::OnWorldPostActorTick.Remove(EM_RemoveTick);FWorldDelegates::OnWorldCleanup.Remove(EM_RemoveCleanup);});
 *EM_Handle=FWorldDelegates::OnWorldPostActorTick.AddLambda([EM_ActorWeak,EM_OwnerWeak,EM_PlayerWeak,EM_World,EM_Clock,EM_Handle,EM_Cleanup,EM_Length,EM_Loop](UWorld* EM_Ticking,ELevelTick,float EM_Delta){
  AActor* EM_Actor=EM_ActorWeak.Get();AActor* EM_Root=EM_OwnerWeak.Get();ULevelSequencePlayer* EM_Current=EM_PlayerWeak.Get();if(!IsValid(EM_Actor)||!IsValid(EM_Root)||!IsValid(EM_Current)||!EM_World.IsValid()){const FDelegateHandle EM_Remove=*EM_Handle,EM_RemoveCleanup=*EM_Cleanup;FWorldDelegates::OnWorldPostActorTick.Remove(EM_Remove);FWorldDelegates::OnWorldCleanup.Remove(EM_RemoveCleanup);return;}if(EM_Ticking!=EM_World.Get())return;
  if(EM_Actor->Tags.Contains(TEXT("EMAnimationReset"))){EM_Clock->Time=0;EM_Actor->Tags.Remove(TEXT("EMAnimationReset"));}
  if(!EM_ObjectActiveInHierarchy(EM_Root)){EM_Clock->Time=0;return;}
  float EM_Time=EM_Clock->Time;EM_Clock->Time+=EM_Delta;if(EM_Length>0)EM_Time=EM_Loop?FMath::Fmod(EM_Time,EM_Length):FMath::Min(EM_Time,EM_Length);
  EM_Current->SetPlaybackPosition(FMovieSceneSequencePlaybackParams(EM_Time,EUpdatePositionMethod::Jump));
 });
}

""";
}
