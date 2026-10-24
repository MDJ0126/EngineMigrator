#include "CoreMinimal.h"
#include "UObject/Package.h"
#include "Modules/ModuleManager.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "GameFramework/Actor.h"
#include "Engine/StaticMeshActor.h"
#include "Engine/StaticMesh.h"
#include "Materials/MaterialInterface.h"
#include "Components/StaticMeshComponent.h"
#include "Components/BoxComponent.h"
#include "Components/SphereComponent.h"
#include "Components/CapsuleComponent.h"
#include "UObject/UnrealType.h"
#include "Serialization/JsonSerializer.h"
#include "Misc/FileHelper.h"
#include "Misc/Parse.h"
#include "Misc/CommandLine.h"
#include "Blueprint/UserWidget.h"
#include "Blueprint/WidgetTree.h"
#include "Components/Button.h"
#include "Components/TextBlock.h"
#include "Components/PanelWidget.h"
#include "UObject/UObjectIterator.h"
namespace {
FString ObjectDisplayName(AActor* Actor){
 if(!Actor->Tags.Contains(TEXT("EMRuntimeCreated")))return Actor->GetName();
 FString Name;for(int32 Part=0;;++Part){const FString Prefix=FString::Printf(TEXT("EMRuntimeName:%d:"),Part);FString Hex;bool Found=false;
  for(const FName& Tag:Actor->Tags){const FString Text=Tag.ToString();if(Text.StartsWith(Prefix)){Hex=Text.RightChop(Prefix.Len());Found=true;break;}}
  if(!Found)break;for(int32 Offset=0;Offset+8<=Hex.Len();Offset+=8){const FString Unit=Hex.Mid(Offset,8);Name.AppendChar(TCHAR(FCString::Strtoui64(*Unit,nullptr,16)));}
 }return Name;
}
TArray<TSharedPtr<FJsonValue>> Frames;
FString EM_ProbeOutput;
int32 TickCount=0;
TSharedPtr<FJsonObject> VectorJson(FVector V,double Divisor=1){auto P=MakeShared<FJsonObject>();P->SetNumberField(TEXT("x"),V.Y/Divisor);P->SetNumberField(TEXT("y"),V.Z/Divisor);P->SetNumberField(TEXT("z"),V.X/Divisor);return P;}
TSharedPtr<FJsonObject> QuatJson(FQuat Q){auto P=MakeShared<FJsonObject>();P->SetNumberField(TEXT("x"),Q.Y);P->SetNumberField(TEXT("y"),Q.Z);P->SetNumberField(TEXT("z"),Q.X);P->SetNumberField(TEXT("w"),Q.W);return P;}
TSharedPtr<FJsonObject> PhysicsJson(AActor* A){
 TArray<UShapeComponent*> Shapes;A->GetComponents(Shapes);UShapeComponent* C=nullptr;for(auto* Shape:Shapes)if(Shape->GetFName()==TEXT("EM_Physics"))C=Shape;if(!C)return nullptr;
 auto P=MakeShared<FJsonObject>();P->SetBoolField(TEXT("dynamic"),C->BodyInstance.bSimulatePhysics);P->SetBoolField(TEXT("gravity"),C->IsGravityEnabled());P->SetBoolField(TEXT("enabled"),C->GetCollisionEnabled()!=ECollisionEnabled::NoCollision);P->SetBoolField(TEXT("trigger"),C->GetGenerateOverlapEvents());P->SetNumberField(TEXT("mass"),C->BodyInstance.GetMassOverride());P->SetObjectField(TEXT("velocity"),VectorJson(C->GetPhysicsLinearVelocity(),100));P->SetObjectField(TEXT("size"),VectorJson(FVector::ZeroVector));P->SetNumberField(TEXT("radius"),0);P->SetNumberField(TEXT("height"),0);
 if(auto* Box=Cast<UBoxComponent>(C)){P->SetStringField(TEXT("shape"),TEXT("box"));P->SetObjectField(TEXT("size"),VectorJson(Box->GetUnscaledBoxExtent()*2,100));}
 if(auto* Sphere=Cast<USphereComponent>(C)){P->SetStringField(TEXT("shape"),TEXT("sphere"));P->SetNumberField(TEXT("radius"),Sphere->GetUnscaledSphereRadius()/100);}
 if(auto* Capsule=Cast<UCapsuleComponent>(C)){P->SetStringField(TEXT("shape"),TEXT("capsule"));P->SetNumberField(TEXT("radius"),Capsule->GetUnscaledCapsuleRadius()/100);P->SetNumberField(TEXT("height"),Capsule->GetUnscaledCapsuleHalfHeight()/50);}
 return P;
}
int32 UIInt(AActor* Actor,const TCHAR* Name){if(!Actor)return 0;for(TFieldIterator<FIntProperty> P(Actor->GetClass());P;++P)if(P->GetName().Equals(Name,ESearchCase::IgnoreCase))return P->GetPropertyValue_InContainer(Actor);return 0;}
AActor* UIComponent(AActor* Owner,const TCHAR* Class){checkf(IsValid(Owner),TEXT("Runtime UI owner is missing"));TArray<AActor*> Children;Owner->GetAttachedActors(Children);if(Owner->GetClass()->GetName()==Class)return Owner;for(auto* A:Children)if(A->GetClass()->GetName()==Class)return A;return nullptr;}
bool UIActive(AActor* Actor){if(!IsValid(Actor))return false;for(auto* It=Actor;It;It=It->GetAttachParentActor())if(It->Tags.Contains(TEXT("EMInactiveSelf")))return false;return true;}
TArray<TSharedPtr<FJsonValue>> ObserveUI(UWorld* World){
 TArray<TSharedPtr<FJsonValue>> Rows;int32 Frame=0;bool Api=false;for(TActorIterator<AActor> It(World);It;++It)if(It->GetClass()->GetName()==TEXT("RuntimeUIDriver")||It->GetClass()->GetName()==TEXT("RuntimeUIApiDriver")){Frame=UIInt(*It,TEXT("Frame"));Api=It->GetClass()->GetName()==TEXT("RuntimeUIApiDriver");}if(!Frame)return Rows;
 const bool Click=Frame==3||Frame==5||Frame==7||Frame==11||Frame==13||Frame==16||Frame==18;
 for(TActorIterator<AActor> It(World);It;++It){AActor* Presenter=*It;if(!Presenter->Tags.Contains(TEXT("EMRuntimeUI"))||Presenter->Tags.Contains(TEXT("EMRuntimeTemplate")))continue;
  AActor* Owner=Presenter->GetOwner();auto* Counter=UIComponent(Owner,Api?TEXT("RuntimeUIApiCounter"):TEXT("RuntimeUICounter"));check(Counter);AActor* ButtonActor=Cast<AActor>(FindFProperty<FObjectPropertyBase>(Counter->GetClass(),TEXT("Button"))->GetObjectPropertyValue_InContainer(Counter));auto* Child=UIComponent(ButtonActor,Api?TEXT("RuntimeUIApiChild"):TEXT("RuntimeUIChild"));check(Child);
  auto* Widget=Cast<UUserWidget>(FindFProperty<FObjectPropertyBase>(Presenter->GetClass(),TEXT("EM_Widget"))->GetObjectPropertyValue_InContainer(Presenter));UButton* Button=nullptr;UTextBlock* Label=nullptr;if(Widget){TArray<UWidget*> Widgets;Widget->WidgetTree->GetAllWidgets(Widgets);for(auto* W:Widgets){if(auto* B=Cast<UButton>(W))Button=B;if(auto* T=Cast<UTextBlock>(W))Label=T;}}
  const bool Visible=Widget&&Widget->GetVisibility()!=ESlateVisibility::Collapsed;bool ButtonVisible=Button!=nullptr;for(UWidget* W=Button;W;W=W->GetParent())if(W->GetVisibility()==ESlateVisibility::Collapsed||W->GetVisibility()==ESlateVisibility::Hidden)ButtonVisible=false;
  if(Click&&Visible&&ButtonVisible&&Button->GetIsEnabled())Button->OnClicked.Broadcast();
  auto Row=MakeShared<FJsonObject>();Row->SetStringField(TEXT("owner"),Owner->GetPathName());Row->SetNumberField(TEXT("awakes"),UIInt(Counter,TEXT("Awakes")));Row->SetNumberField(TEXT("enables"),UIInt(Counter,TEXT("Enables")));Row->SetNumberField(TEXT("starts"),UIInt(Counter,TEXT("Starts")));Row->SetNumberField(TEXT("disables"),UIInt(Counter,TEXT("Disables")));Row->SetNumberField(TEXT("hits"),UIInt(Counter,TEXT("Hits")));Row->SetNumberField(TEXT("order"),UIInt(Counter,TEXT("Order")));Row->SetNumberField(TEXT("offHits"),UIInt(Counter,TEXT("OffHits")));Row->SetNumberField(TEXT("childHits"),UIInt(Child,TEXT("Hits")));Row->SetBoolField(TEXT("visible"),Visible);Row->SetBoolField(TEXT("buttonActive"),UIActive(ButtonActor));Row->SetBoolField(TEXT("interactable"),!ButtonActor->Tags.Contains(TEXT("EMUIButtonNonInteractable")));Row->SetBoolField(TEXT("canvasEnabled"),!Owner->Tags.Contains(TEXT("EMUICanvasDisabled")));Row->SetBoolField(TEXT("buttonEnabled"),!ButtonActor->Tags.Contains(TEXT("EMUIButtonDisabled")));Row->SetStringField(TEXT("text"),Label?Label->GetText().ToString():TEXT(""));Row->SetBoolField(TEXT("hasWidget"),Widget!=nullptr);Rows.Add(MakeShared<FJsonValueObject>(Row));
 }return Rows;
}
void Observe(UWorld* World,ELevelTick,float) {
 if(!World||!World->IsGameWorld()||EM_ProbeOutput.IsEmpty())return;
 auto Frame=MakeShared<FJsonObject>();Frame->SetStringField(TEXT("map"),World->GetMapName());Frame->SetArrayField(TEXT("ui"),ObserveUI(World));int32 UIWidgets=0;for(TObjectIterator<UUserWidget> It;It;++It)if(It->IsInViewport())++UIWidgets;Frame->SetNumberField(TEXT("uiViewportWidgets"),UIWidgets);Frame->SetBoolField(TEXT("uiViewportWorldIndependent"),true);
 TArray<TSharedPtr<FJsonValue>> Actors,Objects,Templates;bool Done=false;
 for(TActorIterator<AActor> It(World);It;++It){AActor* A=*It;if(A->Tags.Num()==0||!A->Tags[0].ToString().StartsWith(TEXT("node_"))||(A->GetClass()->GetName().StartsWith(TEXT("Runtime"))||A->GetClass()->GetName().StartsWith(TEXT("Placement"))))continue;auto O=MakeShared<FJsonObject>();O->SetStringField(TEXT("id"),A->GetPathName());O->SetStringField(TEXT("parent"),A->GetAttachParentActor()?A->GetAttachParentActor()->GetPathName():TEXT(""));O->SetStringField(TEXT("name"),ObjectDisplayName(A));auto P=MakeShared<FJsonObject>();auto V=A->GetActorLocation();P->SetNumberField(TEXT("x"),V.Y/100);P->SetNumberField(TEXT("y"),V.Z/100);P->SetNumberField(TEXT("z"),V.X/100);O->SetObjectField(TEXT("position"),P);O->SetObjectField(TEXT("rotation"),QuatJson(A->GetActorQuat()));O->SetObjectField(TEXT("scale"),VectorJson(A->GetActorScale3D()));O->SetObjectField(TEXT("localPosition"),VectorJson(A->GetRootComponent()->GetRelativeLocation(),100));O->SetObjectField(TEXT("localRotation"),QuatJson(A->GetRootComponent()->GetRelativeRotation().Quaternion()));O->SetObjectField(TEXT("localScale"),VectorJson(A->GetRootComponent()->GetRelativeScale3D()));auto C=A->FindComponentByClass<UStaticMeshComponent>();O->SetStringField(TEXT("mesh"),C&&C->GetStaticMesh()?C->GetStaticMesh()->GetPathName():TEXT(""));O->SetStringField(TEXT("material"),C&&C->GetMaterial(0)?C->GetMaterial(0)->GetPathName():TEXT(""));O->SetBoolField(TEXT("hidden"),A->IsHidden());if(auto Physics=PhysicsJson(A))O->SetObjectField(TEXT("physics"),Physics);if(A->Tags.Contains(TEXT("EMRuntimeTemplate")))Templates.Add(MakeShared<FJsonValueObject>(O));else Objects.Add(MakeShared<FJsonValueObject>(O));}
 for(TActorIterator<AActor> It(World);It;++It){AActor* A=*It;if(!(A->GetClass()->GetName().StartsWith(TEXT("Runtime"))||A->GetClass()->GetName().StartsWith(TEXT("Placement")))||A->Tags.Contains(TEXT("EMRuntimeTemplate")))continue;
  auto Value=MakeShared<FJsonObject>();Value->SetStringField(TEXT("class"),A->GetClass()->GetName());Value->SetStringField(TEXT("id"),A->GetPathName());
  auto Owner=A->GetAttachParentActor();Value->SetStringField(TEXT("owner"),Owner?Owner->GetPathName():A->GetPathName());
  auto State=MakeShared<FJsonObject>();
  for(TFieldIterator<FProperty> P(A->GetClass());P;++P){if(P->GetOwnerClass()->GetOutermost()->GetName()!=TEXT("/Script/EMActorPort")||P->GetName().StartsWith(TEXT("EM_")))continue;
   if(auto I=CastField<FIntProperty>(*P))State->SetNumberField(P->GetName(),I->GetPropertyValue_InContainer(A));
   else if(auto B=CastField<FBoolProperty>(*P))State->SetBoolField(P->GetName(),B->GetPropertyValue_InContainer(A));
   else if(auto N=CastField<FNumericProperty>(*P)){const void* Data=P->ContainerPtrToValuePtr<void>(A);State->SetNumberField(P->GetName(),N->IsFloatingPoint()?N->GetFloatingPointPropertyValue(Data):double(N->GetSignedIntPropertyValue(Data)));}
   else if(auto O=CastField<FObjectPropertyBase>(*P)){UObject* Ref=O->GetObjectPropertyValue_InContainer(A);State->SetStringField(P->GetName(),IsValid(Ref)?Ref->GetPathName():TEXT(""));}
   else if(auto S=CastField<FStructProperty>(*P)){auto J=MakeShared<FJsonObject>();if(S->Struct->GetFName()==TEXT("Vector3f")){const FVector3f& V=*S->ContainerPtrToValuePtr<FVector3f>(A);J->SetNumberField(TEXT("x"),V.X);J->SetNumberField(TEXT("y"),V.Y);J->SetNumberField(TEXT("z"),V.Z);State->SetObjectField(P->GetName(),J);}else if(S->Struct->GetFName()==TEXT("Vector4f")){const FVector4f& V=*S->ContainerPtrToValuePtr<FVector4f>(A);J->SetNumberField(TEXT("x"),V.X);J->SetNumberField(TEXT("y"),V.Y);J->SetNumberField(TEXT("z"),V.Z);J->SetNumberField(TEXT("w"),V.W);State->SetObjectField(P->GetName(),J);}}
  }
  Value->SetObjectField(TEXT("state"),State);Actors.Add(MakeShared<FJsonValueObject>(Value));
  if(A->GetClass()->GetName()==TEXT("RuntimeFinal")&&State->GetNumberField(TEXT("Frame"))>=5)Done=true;
 }
 TArray<TSharedPtr<FJsonValue>> AnimationPresenters;for(TActorIterator<AActor> It(World);It;++It){AActor* A=*It;if(!A->Tags.Contains(TEXT("EMRuntimeAnimation"))||A->Tags.Contains(TEXT("EMRuntimeTemplate")))continue;auto J=MakeShared<FJsonObject>();J->SetStringField(TEXT("id"),A->GetPathName());J->SetStringField(TEXT("owner"),IsValid(A->GetOwner())?A->GetOwner()->GetPathName():TEXT(""));J->SetBoolField(TEXT("managed"),A->Tags.Contains(TEXT("EMAnimationDriven")));J->SetBoolField(TEXT("tickEnabled"),A->IsActorTickEnabled());AnimationPresenters.Add(MakeShared<FJsonValueObject>(J));}Frame->SetArrayField(TEXT("animationPresenters"),AnimationPresenters);Frame->SetArrayField(TEXT("actors"),Actors);Frame->SetArrayField(TEXT("objects"),Objects);Frame->SetArrayField(TEXT("templates"),Templates);Frames.Add(MakeShared<FJsonValueObject>(Frame));
 if(Done||++TickCount>500){auto Root=MakeShared<FJsonObject>();Root->SetStringField(TEXT("status"),Done?TEXT("CAPTURED"):TEXT("TIMEOUT"));Root->SetArrayField(TEXT("frames"),Frames);FString Text;auto Writer=TJsonWriterFactory<>::Create(&Text);FJsonSerializer::Serialize(Root,Writer);FFileHelper::SaveStringToFile(Text,*EM_ProbeOutput);FPlatformMisc::RequestExit(false);}
}
class FProbeModule:public FDefaultGameModuleImpl {
 void StartupModule()override{FParse::Value(FCommandLine::Get(),TEXT("EMRuntimeProbe="),EM_ProbeOutput);if(!EM_ProbeOutput.IsEmpty())FWorldDelegates::OnWorldTickEnd.AddStatic(&Observe);}
};
}
IMPLEMENT_PRIMARY_GAME_MODULE(FProbeModule,EMPortHost,"RuntimeWorld");



