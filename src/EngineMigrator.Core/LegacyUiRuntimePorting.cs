namespace EngineMigrator.Core;
// Exact previous release sources remain readable during reverse porting.
internal static class LegacyUiRuntimePorting
{
    public const string Header="""
        #pragma once
        #include "Blueprint/UserWidget.h"
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
         virtual void NativeOnInitialized() override;
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
          UGameplayStatics::GetAllActorsWithTag(GetWorld(),*Value->GetStringField(TEXT("behaviour")),Actors);
          auto* Expected=LoadClass<AActor>(nullptr,*Value->GetStringField(TEXT("targetClass")));
          if(!Button||Actors.Num()!=1||!Expected||!Actors[0]->IsA(Expected)){UE_LOG(LogTemp,Error,TEXT("EngineMigrator UI target/widget missing, duplicated or class changed"));continue;}
          auto* Listener=Bound.FindRef(Button);if(!Listener){Listener=NewObject<UEMPortClickListener>(this);Bound.Add(Button,Listener);EM_Listeners.Add(Listener);Button->OnClicked.AddDynamic(Listener,&UEMPortClickListener::Clicked);}Listener->Targets.Add(Actors[0]);Listener->Methods.Add(*Value->GetStringField(TEXT("method")));
         }
        }
        """;
}
