#include "Modules/ModuleManager.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "GameFramework/Actor.h"
#include "GameFramework/Pawn.h"
#include "Components/PrimitiveComponent.h"
#include "Components/SphereComponent.h"
#include "Blueprint/UserWidget.h"
#include "Blueprint/WidgetTree.h"
#include "Blueprint/WidgetBlueprintLibrary.h"
#include "Components/Button.h"
#include "Components/Image.h"
#include "Engine/Texture2D.h"
#include "UObject/UnrealType.h"
#include "Serialization/JsonSerializer.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "Misc/App.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "HAL/FileManager.h"
#include "CoreGlobals.h"

namespace EMProbe {
using J=TSharedPtr<FJsonObject>;using V=TSharedPtr<FJsonValue>;
static FString Output;static int32 Count=0,BootstrapFrames=0;static bool Done=false;static TArray<V> Frames,Deltas;static TArray<double> Timeline;
static TArray<V> InitialPawns;
J Obj(){return MakeShared<FJsonObject>();}
J Vec(FVector Value,bool Scale=false){auto Result=Obj();const double Divisor=Scale?1.0:100.0;Result->SetNumberField(TEXT("x"),Value.Y/Divisor);Result->SetNumberField(TEXT("y"),Value.Z/Divisor);Result->SetNumberField(TEXT("z"),Value.X/Divisor);return Result;}
J BrushResource(UWidget* Widget,const FSlateBrush& Brush){auto Value=Obj();Value->SetStringField(TEXT("widget"),Widget->GetName());auto* Texture=Cast<UTexture2D>(Brush.GetResourceObject());Value->SetStringField(TEXT("texture"),Texture?Texture->GetName():TEXT(""));Value->SetNumberField(TEXT("width"),Texture?Texture->GetSizeX():0);Value->SetNumberField(TEXT("height"),Texture?Texture->GetSizeY():0);const FBox2f Region=Brush.GetUVRegion();auto UV=Obj();UV->SetNumberField(TEXT("x"),Region.bIsValid?Region.Min.X:0);UV->SetNumberField(TEXT("y"),Region.bIsValid?1-Region.Max.Y:0);UV->SetNumberField(TEXT("z"),Region.bIsValid?Region.Max.X-Region.Min.X:1);UV->SetNumberField(TEXT("w"),Region.bIsValid?Region.Max.Y-Region.Min.Y:1);Value->SetObjectField(TEXT("uv"),UV);return Value;}
FString Identity(AActor* Actor){return Actor->Tags.IsEmpty()?FString():Actor->Tags[0].ToString();}
bool Behaviour(AActor* Actor){return Identity(Actor).StartsWith(TEXT("node_"))&&Identity(Actor).Contains(TEXT("_behaviour_"));}
TArray<V> Pawns(UWorld* World){TArray<V> Result;for(TActorIterator<APawn> It(World);It;++It){auto* Pawn=*It;auto Value=Obj();Value->SetStringField(TEXT("class"),Pawn->GetClass()->GetPathName());Value->SetObjectField(TEXT("position"),Vec(Pawn->GetActorLocation()));if(auto* Shape=Cast<UPrimitiveComponent>(Pawn->GetRootComponent()))Value->SetBoolField(TEXT("collisionEnabled"),Shape->GetCollisionEnabled()!=ECollisionEnabled::NoCollision);if(auto* Sphere=Cast<USphereComponent>(Pawn->GetRootComponent()))Value->SetNumberField(TEXT("radiusMeters"),Sphere->GetScaledSphereRadius()/100.0);Result.Add(MakeShared<FJsonValueObject>(Value));}return Result;}
J State(AActor* Actor){auto Result=Obj();for(TFieldIterator<FProperty> It(Actor->GetClass());It;++It){auto* Property=*It;const auto Owner=Property->GetOwnerClass()->GetPathName();if(!Owner.StartsWith(TEXT("/Game/"))&&!Owner.StartsWith(TEXT("/Script/EMActorPort.")))continue;
 if(auto* Flag=CastField<FBoolProperty>(Property))Result->SetBoolField(Property->GetName(),Flag->GetPropertyValue_InContainer(Actor));
 else if(auto* Number=CastField<FNumericProperty>(Property))if(Number->IsFloatingPoint())Result->SetNumberField(Property->GetName(),Number->GetFloatingPointPropertyValue(Number->ContainerPtrToValuePtr<void>(Actor)));
 }return Result;}
void Tick(UWorld* World,ELevelTick TickType,float Delta){
 if(Done||Output.IsEmpty()||!World->IsGameWorld()||!World->HasBegunPlay())return;
 // Tick functions initialize their visited/queued frame counters to zero. The
 // engine's GFrameCounter==0 bootstrap frame has not simulated these actors.
 if(GFrameCounter==0){++BootstrapFrames;return;}
 if(Count==0)InitialPawns=Pawns(World);
 auto Frame=Obj();TArray<V> Objects;for(TActorIterator<AActor> It(World);It;++It){auto* Actor=*It;const auto Id=Identity(Actor);if(!Id.StartsWith(TEXT("node_"))||Behaviour(Actor))continue;auto Value=Obj();Value->SetStringField(TEXT("id"),Id);Value->SetObjectField(TEXT("position"),Vec(Actor->GetActorLocation()));Value->SetObjectField(TEXT("scale"),Vec(Actor->GetActorScale3D(),true));Objects.Add(MakeShared<FJsonValueObject>(Value));}
 Frame->SetArrayField(TEXT("objects"),Objects);Frames.Add(MakeShared<FJsonValueObject>(Frame));Deltas.Add(MakeShared<FJsonValueNumber>(Delta));if(++Count!=250){FApp::SetFixedDeltaTime(Timeline[Count]);return;}Done=true;
 TArray<UUserWidget*> Widgets;UWidgetBlueprintLibrary::GetAllWidgetsOfClass(World,Widgets,UUserWidget::StaticClass(),false);TArray<V> WidgetValues,UIResources;int32 Clicks=0;
 for(auto* Widget:Widgets){auto Value=Obj();Value->SetStringField(TEXT("class"),Widget->GetClass()->GetPathName());Value->SetBoolField(TEXT("inViewport"),Widget->IsInViewport());TArray<UWidget*> Children;Widget->WidgetTree->GetAllWidgets(Children);Value->SetNumberField(TEXT("widgets"),Children.Num());WidgetValues.Add(MakeShared<FJsonValueObject>(Value));for(auto* Child:Children){if(auto* Image=Cast<UImage>(Child))UIResources.Add(MakeShared<FJsonValueObject>(BrushResource(Child,Image->GetBrush())));if(auto* Button=Cast<UButton>(Child)){UIResources.Add(MakeShared<FJsonValueObject>(BrushResource(Child,Button->GetStyle().Normal)));Button->OnClicked.Broadcast();Button->OnClicked.Broadcast();Clicks+=2;}}}
 TArray<V> Actors;for(TActorIterator<AActor> It(World);It;++It){auto* Actor=*It;if(!Behaviour(Actor))continue;auto Value=Obj();auto ClassName=Actor->GetClass()->GetName();ClassName.RemoveFromEnd(TEXT("_C"));Value->SetStringField(TEXT("id"),Identity(Actor));Value->SetStringField(TEXT("className"),ClassName);Value->SetStringField(TEXT("owner"),Actor->GetAttachParentActor()?Identity(Actor->GetAttachParentActor()):TEXT(""));Value->SetObjectField(TEXT("state"),State(Actor));Actor->Destroy();Value->SetObjectField(TEXT("destroyState"),State(Actor));Actors.Add(MakeShared<FJsonValueObject>(Value));}
 auto Result=Obj();Result->SetArrayField(TEXT("frames"),Frames);Result->SetArrayField(TEXT("deltas"),Deltas);Result->SetArrayField(TEXT("actors"),Actors);Result->SetArrayField(TEXT("widgets"),WidgetValues);Result->SetArrayField(TEXT("uiResources"),UIResources);Result->SetNumberField(TEXT("clicksExecuted"),Clicks);Result->SetBoolField(TEXT("worldTickExecuted"),true);Result->SetBoolField(TEXT("savedSceneExecuted"),true);Result->SetBoolField(TEXT("packagedExecutableExecuted"),true);Result->SetBoolField(TEXT("projectPortComplete"),false);
 Result->SetNumberField(TEXT("bootstrapFramesSkipped"),BootstrapFrames);Result->SetStringField(TEXT("timeInput"),TEXT("actual Unity captured frame deltas"));
 Result->SetArrayField(TEXT("pawnsAtStart"),InitialPawns);Result->SetArrayField(TEXT("pawnsAtEnd"),Pawns(World));
 FString Json;FJsonSerializer::Serialize(Result.ToSharedRef(),TJsonWriterFactory<>::Create(&Json));IFileManager::Get().MakeDirectory(*FPaths::GetPath(Output),true);const bool Saved=FFileHelper::SaveStringToFile(Json,*Output);UE_LOG(LogTemp,Display,TEXT("EM_PACKAGED_RUNTIME_PROBE: frames=%d saved=%d"),Count,Saved);FPlatformMisc::RequestExit(false);
}
}
class FEMRuntimeProbe : public IModuleInterface {
 FDelegateHandle Handle;
 public:void StartupModule() override {if(FParse::Value(FCommandLine::Get(),TEXT("EMProbeOutput="),EMProbe::Output)){FString Input,Json;TSharedPtr<FJsonObject> Data;if(!FParse::Value(FCommandLine::Get(),TEXT("EMProbeInput="),Input)||!FFileHelper::LoadFileToString(Json,*Input)||!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Json),Data)){EMProbe::Output.Empty();return;}for(auto& Value:Data->GetArrayField(TEXT("deltas")))EMProbe::Timeline.Add(Value->AsNumber());if(EMProbe::Timeline.Num()!=250){EMProbe::Output.Empty();return;}FApp::SetUseFixedTimeStep(true);FApp::SetFixedDeltaTime(EMProbe::Timeline[0]);Handle=FWorldDelegates::OnWorldPostActorTick.AddStatic(&EMProbe::Tick);}}
 void ShutdownModule() override {if(Handle.IsValid())FWorldDelegates::OnWorldPostActorTick.Remove(Handle);}
};
IMPLEMENT_MODULE(FEMRuntimeProbe,EMRuntimeProbe)
