#include "EMBlueprintLibrary.h"
#include "LevelSequence.h"
#include "GameFramework/Actor.h"
#include "LevelSequenceActor.h"
#include "LevelSequencePlayer.h"
#include "Engine/Blueprint.h"
#include "Engine/BlueprintGeneratedClass.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "K2Node_Event.h"
#include "K2Node_CallFunction.h"
#include "EdGraphSchema_K2.h"
#include "Misc/PackageName.h"
#include "UObject/SavePackage.h"
bool UEMBlueprintLibrary::BindAnimation(ULevelSequence* Sequence,FGuid BindingId,AActor* Actor){if(!Sequence||!Actor||!BindingId.IsValid())return false;Sequence->Modify();Sequence->UnbindPossessableObjects(BindingId);Sequence->BindPossessableObject(BindingId,*Actor,Actor->GetWorld());return true;}
FString UEMBlueprintLibrary::CreateAnimationPresenter(const FString& AssetPath){
 if(!AssetPath.StartsWith(TEXT("/Game/"))||AssetPath.Contains(TEXT(".."))||FPackageName::DoesPackageExist(AssetPath))return TEXT("ERROR: invalid animation presenter path");
 auto* BP=FKismetEditorUtilities::CreateBlueprint(ALevelSequenceActor::StaticClass(),CreatePackage(*AssetPath),*FPackageName::GetLongPackageAssetName(AssetPath),BPTYPE_Normal,UBlueprint::StaticClass(),UBlueprintGeneratedClass::StaticClass());BP->UbergraphPages.Empty();auto* G=FBlueprintEditorUtils::CreateNewGraph(BP,TEXT("EventGraph"),UEdGraph::StaticClass(),UEdGraphSchema_K2::StaticClass());FBlueprintEditorUtils::AddUbergraphPage(BP,G);
 auto* E=NewObject<UK2Node_Event>(G);G->AddNode(E);E->CreateNewGuid();E->EventReference.SetExternalMember(TEXT("ReceiveTick"),AActor::StaticClass());E->bOverrideFunction=true;E->AllocateDefaultPins();
 auto Call=[G](UClass* Class,FName Name){auto* N=NewObject<UK2Node_CallFunction>(G);G->AddNode(N);N->CreateNewGuid();N->SetFromFunction(Class->FindFunctionByName(Name));N->AllocateDefaultPins();return N;};
 auto* Get=Call(ALevelSequenceActor::StaticClass(),TEXT("GetSequencePlayer"));auto* Play=Call(UMovieSceneSequencePlayer::StaticClass(),TEXT("Play"));auto* Disable=Call(AActor::StaticClass(),TEXT("SetActorTickEnabled"));Disable->FindPinChecked(TEXT("bEnabled"))->DefaultValue=TEXT("false");auto Link=[](UEdGraphPin* A,UEdGraphPin* B){check(A&&B);GetDefault<UEdGraphSchema_K2>()->TryCreateConnection(A,B);};Link(E->FindPinChecked(UEdGraphSchema_K2::PN_Then),Play->GetExecPin());Link(Get->GetReturnValuePin(),Play->FindPinChecked(UEdGraphSchema_K2::PN_Self));Link(Play->GetThenPin(),Disable->GetExecPin());
 FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(BP);FKismetEditorUtilities::CompileBlueprint(BP);if(BP->Status==BS_Error)return TEXT("ERROR: animation presenter compile failed");auto* CDO=CastChecked<AActor>(BP->GeneratedClass->GetDefaultObject());CDO->PrimaryActorTick.bCanEverTick=true;CDO->PrimaryActorTick.bStartWithTickEnabled=true;BP->SetFlags(RF_Public|RF_Standalone);FSavePackageArgs Args;Args.TopLevelFlags=RF_Public|RF_Standalone;if(!UPackage::SavePackage(BP->GetOutermost(),BP,*FPackageName::LongPackageNameToFilename(AssetPath,FPackageName::GetAssetPackageExtension()),Args))return TEXT("ERROR: animation presenter save failed");return BP->GeneratedClass->GetPathName();
}
