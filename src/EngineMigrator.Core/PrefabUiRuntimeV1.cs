namespace EngineMigrator.Core;
internal static class PrefabUiRuntimeV1
{
    public const string Header="""
        #pragma once
        #include "Blueprint/UserWidget.h"
        #include "GameFramework/Actor.h"
        #include "EMPortUserWidget.generated.h"
        class AActor;
        UCLASS()
        class EMUIRUNTIME_API UEMPortClickListener : public UObject {
         GENERATED_BODY()
        public:
         UPROPERTY() TArray<TObjectPtr<AActor>> Targets;
         UPROPERTY() TArray<FName> Methods;
         UFUNCTION() void Clicked();
        };
        UCLASS(Blueprintable)
        class EMUIRUNTIME_API UEMPortUserWidget : public UUserWidget {
         GENERATED_BODY()
        public:
         UPROPERTY(EditDefaultsOnly,Category="EngineMigrator") FString EM_Bindings;
         UPROPERTY(Transient) TArray<TObjectPtr<UEMPortClickListener>> EM_Listeners;
         UPROPERTY(Transient) TMap<FString,TObjectPtr<AActor>> EM_Scope;
         bool EM_Contextual=false;
         virtual void NativeOnInitialized() override;
        };
        UCLASS()
        class EMUIRUNTIME_API AEMPortUIPresenter : public AActor {
         GENERATED_BODY()
        public:
         AEMPortUIPresenter();
         UPROPERTY(EditAnywhere) TSubclassOf<UUserWidget> EM_WidgetClass;
         UPROPERTY(Transient,DuplicateTransient) TObjectPtr<UUserWidget> EM_Widget;
         UPROPERTY(Transient,DuplicateTransient) TMap<FString,TObjectPtr<AActor>> EM_Scope;
         UFUNCTION() void EM_ConfigureRuntimeUI(AActor* OriginalOwner,const TArray<AActor*>& Originals,const TArray<AActor*>& Copies);
         UFUNCTION() void EM_RefreshRuntimeUI();
         virtual void BeginPlay() override;
         virtual void Tick(float Delta) override;
         virtual void EndPlay(const EEndPlayReason::Type Reason) override;
        };
        """;
    public const string Implementation="""
        #include "EMPortUserWidget.h"
        #include "Blueprint/WidgetTree.h"
        #include "Components/Button.h"
        #include "GameFramework/Actor.h"
        #include "Kismet/GameplayStatics.h"
        #include "Serialization/JsonSerializer.h"
        #include "Modules/ModuleManager.h"
        IMPLEMENT_MODULE(FDefaultModuleImpl, EMUIRuntime)
        void UEMPortClickListener::Clicked(){
         for(int32 Index=0;Index<Targets.Num();++Index){auto* Target=Targets[Index].Get();if(!IsValid(Target))continue;auto* Function=Target->FindFunction(Methods[Index]);
          if(!Function||Function->ParmsSize!=0){UE_LOG(LogTemp,Error,TEXT("EngineMigrator callback signature changed: %s"),*Methods[Index].ToString());continue;}
          Target->ProcessEvent(Function,nullptr);
         }
        }
        void UEMPortUserWidget::NativeOnInitialized(){
         Super::NativeOnInitialized();TSharedPtr<FJsonObject> Data;
         if(!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(EM_Bindings),Data)){UE_LOG(LogTemp,Error,TEXT("EngineMigrator UI bindings invalid"));return;}
         TMap<UButton*,UEMPortClickListener*> Bound;
         for(auto& Entry:Data->GetArrayField(TEXT("callbacks"))){auto Value=Entry->AsObject();if(!Value->GetBoolField(TEXT("enabled")))continue;
          auto* Button=WidgetTree->FindWidget<UButton>(*Value->GetStringField(TEXT("widget")));TArray<AActor*> Actors;
          const FString Identity=Value->GetStringField(TEXT("behaviour"));
          if(EM_Contextual){if(auto Target=EM_Scope.FindRef(Identity))Actors.Add(Target);}else UGameplayStatics::GetAllActorsWithTag(GetWorld(),*Identity,Actors);
          auto* Expected=LoadClass<AActor>(nullptr,*Value->GetStringField(TEXT("targetClass")));
          if(!Button||Actors.Num()!=1||!Expected||!Actors[0]->IsA(Expected)){UE_LOG(LogTemp,Error,TEXT("EngineMigrator UI target/widget missing, duplicated or class changed"));continue;}
          auto* Listener=Bound.FindRef(Button);if(!Listener){Listener=NewObject<UEMPortClickListener>(this);Bound.Add(Button,Listener);EM_Listeners.Add(Listener);Button->OnClicked.AddDynamic(Listener,&UEMPortClickListener::Clicked);}Listener->Targets.Add(Actors[0]);Listener->Methods.Add(*Value->GetStringField(TEXT("method")));
         }
        }
        AEMPortUIPresenter::AEMPortUIPresenter(){PrimaryActorTick.bCanEverTick=true;PrimaryActorTick.TickGroup=TG_PostPhysics;}
        void AEMPortUIPresenter::EM_ConfigureRuntimeUI(AActor* OriginalOwner,const TArray<AActor*>& Originals,const TArray<AActor*>& Copies){
         EM_Widget=nullptr;EM_Scope.Empty();
         for(int32 I=0;I<Originals.Num()&&I<Copies.Num();++I){if(Originals[I]==OriginalOwner)SetOwner(Copies[I]);if(Originals[I]->Tags.Num()&&Originals[I]->Tags[0].ToString().StartsWith(TEXT("node_")))EM_Scope.Add(Originals[I]->Tags[0].ToString(),Copies[I]);}
        }
        static bool EM_UIActive(AActor* Actor){for(AActor* It=Actor;It;It=It->GetAttachParentActor())if(It->Tags.Contains(TEXT("EMInactiveSelf")))return false;return IsValid(Actor);}
        void AEMPortUIPresenter::BeginPlay(){Super::BeginPlay();EM_RefreshRuntimeUI();}
        void AEMPortUIPresenter::Tick(float Delta){Super::Tick(Delta);EM_RefreshRuntimeUI();}
        void AEMPortUIPresenter::EM_RefreshRuntimeUI(){
         if(Tags.Contains(TEXT("EMRuntimeTemplate"))||!EM_WidgetClass)return;
         AActor* ScopeOwner=GetOwner();const bool Active=EM_UIActive(ScopeOwner)&&!Tags.Contains(TEXT("EMUICanvasDisabled"));
         if(!EM_Widget&&Active){
          EM_Widget=NewObject<UUserWidget>(GetWorld(),EM_WidgetClass);if(auto* Port=Cast<UEMPortUserWidget>(EM_Widget)){Port->EM_Contextual=true;Port->EM_Scope=EM_Scope;}
          EM_Widget->Initialize();EM_Widget->AddToViewport();
         }
         if(!EM_Widget)return;EM_Widget->SetVisibility(Active?ESlateVisibility::SelfHitTestInvisible:ESlateVisibility::Collapsed);
         for(const auto& Entry:EM_Scope){const FString& Id=Entry.Key;if(Id.Contains(TEXT("_behaviour_")))continue;
          if(auto* Panel=EM_Widget->WidgetTree->FindWidget(*(Id+TEXT("_Panel"))))Panel->SetVisibility(EM_UIActive(Entry.Value)?ESlateVisibility::SelfHitTestInvisible:ESlateVisibility::Collapsed);
         }
        }
        void AEMPortUIPresenter::EndPlay(const EEndPlayReason::Type Reason){if(EM_Widget){EM_Widget->RemoveFromParent();EM_Widget=nullptr;}EM_Scope.Empty();Super::EndPlay(Reason);}
        """;
}
