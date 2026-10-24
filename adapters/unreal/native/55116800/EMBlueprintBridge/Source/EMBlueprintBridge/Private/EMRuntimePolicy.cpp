#include "EMBlueprintLibrary.h"
#include "Engine/World.h"
#include "Engine/Blueprint.h"
#include "Engine/BlueprintGeneratedClass.h"
#include "Engine/SimpleConstructionScript.h"
#include "GameFramework/WorldSettings.h"
#include "GameFramework/GameModeBase.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/SpectatorPawn.h"
#include "GameFramework/HUD.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "K2Node_Event.h"
#include "K2Node_FunctionEntry.h"
#include "K2Node_FunctionResult.h"
#include "K2Node_CallParentFunction.h"
#include "EdGraphSchema_K2.h"
#include "Serialization/JsonSerializer.h"
#include "Misc/PackageName.h"
#include "UObject/SavePackage.h"
#include "UObject/UnrealType.h"

FString UEMBlueprintLibrary::RuntimePolicy(UWorld* World,const FString& Json){
 TSharedPtr<FJsonObject> Input;if(!World||!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Json),Input))return TEXT("ERROR: invalid runtime policy input");
 auto* Settings=World->GetWorldSettings();const auto Mode=Input->GetStringField(TEXT("mode"));UBlueprint* BP=nullptr;
 if(Mode==TEXT("create")){
  auto Path=Input->GetStringField(TEXT("asset"));if(!Path.StartsWith(TEXT("/Game/"))||Path.Contains(TEXT(".."))||FPackageName::DoesPackageExist(Path))return TEXT("ERROR: invalid/existing runtime policy asset");
  BP=FKismetEditorUtilities::CreateBlueprint(AGameModeBase::StaticClass(),CreatePackage(*Path),*FPackageName::GetLongPackageAssetName(Path),BPTYPE_Normal,UBlueprint::StaticClass(),UBlueprintGeneratedClass::StaticClass());
  BP->UbergraphPages.Empty();FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(BP);FKismetEditorUtilities::CompileBlueprint(BP);if(BP->Status==BS_Error)return TEXT("ERROR: runtime policy compile failed");
  auto* Defaults=BP->GeneratedClass->GetDefaultObject<AGameModeBase>();Defaults->DefaultPawnClass=nullptr;Defaults->SpectatorClass=nullptr;Defaults->HUDClass=nullptr;
  FSavePackageArgs Args;Args.TopLevelFlags=RF_Public|RF_Standalone;if(!UPackage::SavePackage(BP->GetOutermost(),BP,*FPackageName::LongPackageNameToFilename(Path,FPackageName::GetAssetPackageExtension()),Args))return TEXT("ERROR: runtime policy save failed");Settings->DefaultGameMode=BP->GeneratedClass;World->MarkPackageDirty();
 }else if(Mode!=TEXT("read"))return TEXT("ERROR: unsupported runtime policy mode");
 auto Result=MakeShared<FJsonObject>();
 if(!Settings->DefaultGameMode){Result->SetStringField(TEXT("policy"),TEXT("inherited"));Result->SetBoolField(TEXT("projectPortComplete"),false);}
 else {
  BP=Cast<UBlueprint>(Settings->DefaultGameMode->ClassGeneratedBy);if(!BP||BP->ParentClass!=AGameModeBase::StaticClass())return TEXT("ERROR: unmapped game mode class requires conversion");
  if(BP->NewVariables.Num()||BP->MacroGraphs.Num()||BP->ImplementedInterfaces.Num())return TEXT("ERROR: additional game mode state/macros/interfaces require conversion");
  for(UEdGraph* Graph:BP->UbergraphPages)for(UEdGraphNode* Node:Graph->Nodes){if(!Node->IsA<UK2Node_Event>())return TEXT("ERROR: additional game mode graph logic requires conversion");for(UEdGraphPin* Pin:Node->Pins)if(Pin->PinType.PinCategory==UEdGraphSchema_K2::PC_Exec&&!Pin->LinkedTo.IsEmpty())return TEXT("ERROR: connected game mode events require conversion");}
  for(UEdGraph* Graph:BP->FunctionGraphs){if(Graph->GetFName()!=TEXT("UserConstructionScript"))return TEXT("ERROR: additional game mode functions require conversion");for(UEdGraphNode* Node:Graph->Nodes){if(Node->IsA<UK2Node_FunctionEntry>()||Node->IsA<UK2Node_FunctionResult>())continue;auto* Parent=Cast<UK2Node_CallParentFunction>(Node);if(!Parent||Parent->FunctionReference.GetMemberName()!=TEXT("UserConstructionScript"))return TEXT("ERROR: game mode construction logic requires conversion");}}
  auto* Defaults=Settings->DefaultGameMode->GetDefaultObject<AGameModeBase>();auto* Base=GetDefault<AGameModeBase>();if(Defaults->DefaultPawnClass||Defaults->SpectatorClass||Defaults->HUDClass)return TEXT("ERROR: implicit Pawn/Spectator/HUD requires explicit conversion");
  for(TFieldIterator<FProperty> It(AGameModeBase::StaticClass());It;++It){auto* Property=*It;if(Property->GetOwnerClass()!=AGameModeBase::StaticClass()||!Property->HasAnyPropertyFlags(CPF_Edit)||Property->HasAnyPropertyFlags(CPF_Transient|CPF_EditorOnly))continue;const auto Name=Property->GetFName();if(Name==TEXT("DefaultPawnClass")||Name==TEXT("SpectatorClass")||Name==TEXT("HUDClass"))continue;if(!Property->Identical_InContainer(Defaults,Base))return TEXT("ERROR: edited game mode property requires conversion: ")+Name.ToString();}
  Result->SetStringField(TEXT("policy"),TEXT("noImplicitPawnSpectatorHUD"));Result->SetStringField(TEXT("asset"),BP->GetPathName());Result->SetStringField(TEXT("class"),Settings->DefaultGameMode->GetPathName());Result->SetBoolField(TEXT("noImplicitCollisionPawn"),true);Result->SetBoolField(TEXT("projectPortComplete"),false);
 }
 FString Output;FJsonSerializer::Serialize(Result,TJsonWriterFactory<>::Create(&Output));return Output;
}
