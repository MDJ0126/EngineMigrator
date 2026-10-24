namespace EngineMigrator.Core;

/// <summary>Runtime helpers are part of the validated generated Actor source, never metadata-only substitutes.</summary>
public static class RuntimeObjectPorting
{
    internal static string PlacementHelpers=>PlacementWrappers+BaseHelpers.Replace("AActor* EM_Instantiate(AActor* EM_Source) {","AActor* EM_InstantiateCore(AActor* EM_Source,AActor* EM_Parent,int32 EM_Mode,FVector3f EM_Position,FVector4f EM_Rotation) {").Replace(" for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning(EM_New->GetActorTransform(),true);",PlacementTransforms+"\n for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning(EM_New->GetActorTransform(),true);");
    internal static string Helpers=>PlacementHelpers.Replace(" return EM_Copies[0];", " if(EM_Source->Tags.Contains(FName(TEXT(\"EMRuntimeCreated\")))){const FString EM_Name=EM_GetObjectName(EM_Source)+TEXT(\"(Clone)\");EM_SetObjectName(EM_Copies[0],*EM_Name);}\n return EM_Copies[0];");
    private const string PlacementWrappers="""
protected:
AActor* EM_Instantiate(AActor* EM_Source){return EM_InstantiateCore(EM_Source,nullptr,0,FVector3f::ZeroVector,FVector4f(0,0,0,1));}
AActor* EM_InstantiateParent(AActor* EM_Source,AActor* EM_Parent){return EM_InstantiateCore(EM_Source,EM_Parent,1,FVector3f::ZeroVector,FVector4f(0,0,0,1));}
AActor* EM_InstantiateParentWorld(AActor* EM_Source,AActor* EM_Parent,bool EM_World){return EM_InstantiateCore(EM_Source,EM_Parent,EM_World?2:1,FVector3f::ZeroVector,FVector4f(0,0,0,1));}
AActor* EM_InstantiatePose(AActor* EM_Source,FVector3f EM_Pos,FVector4f EM_Rot){return EM_InstantiateCore(EM_Source,nullptr,3,EM_Pos,EM_Rot);}
AActor* EM_InstantiatePoseParent(AActor* EM_Source,FVector3f EM_Pos,FVector4f EM_Rot,AActor* EM_Parent){return EM_InstantiateCore(EM_Source,EM_Parent,3,EM_Pos,EM_Rot);}

""";
    private const string PlacementTransforms="""
 const FTransform EM_SourceWorld=EM_Source->GetActorTransform(); FTransform EM_Desired=EM_Source->GetRootComponent()->GetRelativeTransform();
 if(EM_Mode==2 && IsValid(EM_Parent)) EM_Desired=EM_SourceWorld;
 if(EM_Mode==1 && IsValid(EM_Parent)) EM_Desired=EM_Source->GetRootComponent()->GetRelativeTransform()*EM_Parent->GetActorTransform();
 if(EM_Mode==3){EM_Desired.SetLocation(FVector(EM_Position.Z*100.0,EM_Position.X*100.0,EM_Position.Y*100.0));EM_Desired.SetRotation(FQuat(EM_Rotation.Z,EM_Rotation.X,EM_Rotation.Y,EM_Rotation.W));if(IsValid(EM_Parent))EM_Desired.SetScale3D(EM_Source->GetRootComponent()->GetRelativeScale3D()*EM_Parent->GetActorScale3D());}
 for(int32 EM_i=0;EM_i<EM_Copies.Num();++EM_i){const FTransform EM_Relative=EM_Originals[EM_i]->GetActorTransform().GetRelativeTransform(EM_SourceWorld);EM_Copies[EM_i]->SetActorTransform(EM_Relative*EM_Desired);}
 if(IsValid(EM_Parent))EM_Copies[0]->AttachToActor(EM_Parent,FAttachmentTransformRules::KeepWorldTransform);
""";
    internal static string PreviousHelpers=>BaseHelpers.Replace("  AActor* EM_Root=EM_Pending.Get(); FDelegateHandle EM_RemoveHandle=*EM_Handle;\n  FWorldDelegates::OnWorldPostActorTick.Remove(EM_RemoveHandle);\n  if(!IsValid(EM_Root))return;\n  TArray<AActor*> EM_All; EM_All.Add(EM_Root);","  FWorldDelegates::OnWorldPostActorTick.Remove(*EM_Handle);\n  if(!EM_Pending.IsValid())return;\n  TArray<AActor*> EM_All; EM_All.Add(EM_Pending.Get());");
    internal static string LegacyHelpers=>PreviousHelpers.Replace(" EM_New->DetachFromActor(FDetachmentTransformRules::KeepWorldTransform);","").Replace("  if(EM_New->Tags.Contains(TEXT(\"EMRuntimeTemplate\"))){EM_New->Tags.Remove(TEXT(\"EMRuntimeTemplate\"));EM_New->SetActorHiddenInGame(false);EM_New->SetActorEnableCollision(true);EM_New->PrimaryActorTick.bStartWithTickEnabled=true;EM_New->SetActorTickEnabled(true);}\n","");
    internal const string SceneIndexHelper="void EM_LoadSceneIndex(int32 EM_Index){const FString EM_Key=FString::Printf(TEXT(\"#%d\"),EM_Index);EM_LoadScene(*EM_Key); }\n";
    internal static string Includes=>LegacyIncludes+"#include \"UObject/Package.h\"\n";
    internal const string LegacyIncludes="""
#include "Engine/World.h"
#include "Kismet/GameplayStatics.h"
#include "Serialization/ArchiveReplaceObjectRef.h"
#include "UObject/UnrealType.h"

""";
    internal const string BaseHelpers="""
protected:
AActor* EM_Instantiate(AActor* EM_Source) {
 if (!IsValid(EM_Source)) { UE_LOG(LogTemp, Error, TEXT("EngineMigrator Instantiate null source")); return nullptr; }
 TArray<AActor*> EM_Originals; EM_Originals.Add(EM_Source);
 for (int32 EM_i=0; EM_i<EM_Originals.Num(); ++EM_i) { TArray<AActor*> EM_Children; EM_Originals[EM_i]->GetAttachedActors(EM_Children); EM_Originals.Append(EM_Children); }
 TMap<UObject*,UObject*> EM_Map; TArray<AActor*> EM_Copies;
 for (AActor* EM_Old:EM_Originals) {
  FActorSpawnParameters EM_Params; EM_Params.Template=EM_Old; EM_Params.bDeferConstruction=true; EM_Params.SpawnCollisionHandlingOverride=ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
  AActor* EM_New=GetWorld()->SpawnActor<AActor>(EM_Old->GetClass(),FTransform::Identity,EM_Params);
  if (!EM_New) { for (AActor* EM_Created:EM_Copies) EM_Created->Destroy(); return nullptr; }
  EM_Map.Add(EM_Old,EM_New); EM_Copies.Add(EM_New); EM_New->Tags.AddUnique(TEXT("EMRuntimeClone")); EM_New->DetachFromActor(FDetachmentTransformRules::KeepWorldTransform); EM_New->SetActorTransform(EM_Old->GetActorTransform());
  if(EM_New->Tags.Contains(TEXT("EMRuntimeTemplate"))){EM_New->Tags.Remove(TEXT("EMRuntimeTemplate"));EM_New->SetActorHiddenInGame(false);EM_New->SetActorEnableCollision(true);EM_New->PrimaryActorTick.bStartWithTickEnabled=true;EM_New->SetActorTickEnabled(true);}
  for (TFieldIterator<FProperty> EM_P(EM_New->GetClass()); EM_P; ++EM_P) if (EM_P->GetOwnerClass()->GetOutermost()->GetName()==TEXT("/Script/EMActorPort") && !EM_P->HasAnyPropertyFlags(CPF_Edit)) EM_P->CopyCompleteValue_InContainer(EM_New,EM_New->GetClass()->GetDefaultObject());
 }
 for (int32 EM_i=0; EM_i<EM_Copies.Num(); ++EM_i) {
  AActor* EM_New=EM_Copies[EM_i]; AActor* EM_Parent=EM_Originals[EM_i]->GetAttachParentActor();
  if (EM_i>0 && EM_Map.Contains(EM_Parent)) EM_New->AttachToActor(CastChecked<AActor>(EM_Map[EM_Parent]),FAttachmentTransformRules::KeepWorldTransform);
  for (TFieldIterator<FObjectPropertyBase> EM_P(EM_New->GetClass()); EM_P; ++EM_P) if (EM_P->GetOwnerClass()->GetOutermost()->GetName()==TEXT("/Script/EMActorPort")) { UObject* EM_Value=EM_P->GetObjectPropertyValue_InContainer(EM_New); if (UObject** EM_Remapped=EM_Map.Find(EM_Value)) EM_P->SetObjectPropertyValue_InContainer(EM_New,*EM_Remapped); }
 }
 for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning(EM_New->GetActorTransform(),true);
 return EM_Copies[0];
}
void EM_Destroy(AActor* EM_Value) {
 if (!IsValid(EM_Value)) return;
 TWeakObjectPtr<AActor> EM_Pending=EM_Value; TWeakObjectPtr<UWorld> EM_World=GetWorld();
 auto EM_Handle=MakeShared<FDelegateHandle>();
 *EM_Handle=FWorldDelegates::OnWorldPostActorTick.AddLambda([EM_Pending,EM_World,EM_Handle](UWorld* EM_Ticking,ELevelTick,float){
  if(EM_Ticking!=EM_World.Get())return;
  AActor* EM_Root=EM_Pending.Get(); FDelegateHandle EM_RemoveHandle=*EM_Handle;
  FWorldDelegates::OnWorldPostActorTick.Remove(EM_RemoveHandle);
  if(!IsValid(EM_Root))return;
  TArray<AActor*> EM_All; EM_All.Add(EM_Root);
  for(int32 EM_i=0; EM_i<EM_All.Num(); ++EM_i){ TArray<AActor*> EM_Children; EM_All[EM_i]->GetAttachedActors(EM_Children); EM_All.Append(EM_Children); }
  for(int32 EM_i=EM_All.Num()-1; EM_i>=0; --EM_i) EM_All[EM_i]->Destroy();
 });
}
void EM_LoadScene(const TCHAR* EM_Key) {
 FString EM_Prefix=FString(TEXT("EMScene:"))+EM_Key+TEXT("=");
 for(const FName& EM_Tag:Tags) { FString EM_Text=EM_Tag.ToString(); if(EM_Text.StartsWith(EM_Prefix)){UGameplayStatics::OpenLevel(this,FName(*EM_Text.RightChop(EM_Prefix.Len())));return;} }
 UE_LOG(LogTemp, Error, TEXT("EngineMigrator scene unavailable: %s"),EM_Key);
}

""";
}


