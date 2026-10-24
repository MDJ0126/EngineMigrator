using System.Text.Json.Nodes;
namespace EngineMigrator.Core;

/// <summary>Cookable UMG callback binding. Editor bridge code is not needed at runtime.</summary>
public static class UiRuntimePorting
{
    internal static void VerifySource(string project)
    {
        var root=Path.Combine(project,"Plugins","EMUIRuntime","Source","EMUIRuntime");if(!Directory.Exists(root))return;
        static string Lines(string text)=>text.Replace("\r\n","\n");
        var header=Lines(File.ReadAllText(Path.Combine(root,"Public","EMPortUserWidget.h")));var implementation=Lines(File.ReadAllText(Path.Combine(root,"Private","EMPortUserWidget.cpp")));
        if((header!=Lines(Header)||implementation!=Lines(Implementation))&&(header!=Lines(LegacyUiRuntimePorting.Header)||implementation!=Lines(LegacyUiRuntimePorting.Implementation))&&(header!=Lines(PrefabUiRuntimeV1.Header)||implementation!=Lines(PrefabUiRuntimeV1.Implementation)))throw new NotSupportedException("수정된 런타임 UI 바인더는 자동 역변환할 수 없습니다.");
    }
    internal static async Task CompileAsync(string project,string editor,string log,CancellationToken token)
    {
        UnrealRuntimeHost.Ensure(project);
        var plugin=Path.Combine(project,"Plugins","EMUIRuntime");if(Directory.Exists(plugin))throw new InvalidOperationException("런타임 UI 플러그인 이름 충돌: EMUIRuntime");
        var module=Path.Combine(plugin,"Source","EMUIRuntime");Directory.CreateDirectory(Path.Combine(module,"Public"));Directory.CreateDirectory(Path.Combine(module,"Private"));
        File.WriteAllText(Path.Combine(plugin,"EMUIRuntime.uplugin"),"{\"FileVersion\":3,\"Version\":1,\"FriendlyName\":\"EngineMigrator Runtime UI\",\"CanContainContent\":false,\"Modules\":[{\"Name\":\"EMUIRuntime\",\"Type\":\"Runtime\",\"LoadingPhase\":\"Default\"}]}");
        File.WriteAllText(Path.Combine(module,"EMUIRuntime.Build.cs"),"using UnrealBuildTool; public class EMUIRuntime : ModuleRules { public EMUIRuntime(ReadOnlyTargetRules Target) : base(Target) { PCHUsage=PCHUsageMode.UseExplicitOrSharedPCHs; PublicDependencyModuleNames.AddRange(new[]{\"Core\",\"CoreUObject\",\"Engine\",\"UMG\"}); PrivateDependencyModuleNames.AddRange(new[]{\"Json\"}); } }");
        File.WriteAllText(Path.Combine(module,"Public","EMPortUserWidget.h"),Header);
        File.WriteAllText(Path.Combine(module,"Private","EMPortUserWidget.cpp"),Implementation);
        var path=Directory.GetFiles(project,"*.uproject").Single();var json=JsonNode.Parse(File.ReadAllText(path))!;var plugins=(JsonArray)json["Plugins"]!;plugins.Add(new JsonObject{["Name"]="EMUIRuntime",["Enabled"]=true});File.WriteAllText(path,json.ToJsonString());
        var engine=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(editor)!,"..",".."));var dotnet=Directory.GetFiles(Path.Combine(engine,"Binaries","ThirdParty","DotNet"),"dotnet.exe",SearchOption.AllDirectories).Single();
        await ResourcePorting.RunEditor(dotnet,[Path.Combine(engine,"Binaries","DotNET","UnrealBuildTool","UnrealBuildTool.dll"),UnrealRuntimeHost.EditorTarget(project),"Win64","Development","-Project="+path,"-NoHotReload","-MaxParallelActions=1","-log="+log],project,token);
    }
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
         void EM_InitializeScope();
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
         for(int32 I=0;I<Originals.Num()&&I<Copies.Num();++I)if(Copies[I]==this)if(auto* Source=Cast<AEMPortUIPresenter>(Originals[I])){Source->EM_InitializeScope();EM_Scope=Source->EM_Scope;}
         for(int32 I=0;I<Originals.Num()&&I<Copies.Num();++I){if(Originals[I]==OriginalOwner)SetOwner(Copies[I]);if(Originals[I]->Tags.Num()&&Originals[I]->Tags[0].ToString().StartsWith(TEXT("node_")))EM_Scope.Add(Originals[I]->Tags[0].ToString(),Copies[I]);}
        }
        void AEMPortUIPresenter::EM_InitializeScope(){
         if(EM_Scope.Num()||Tags.Contains(TEXT("EMRuntimeTemplate")))return;TArray<AActor*> Actors;UGameplayStatics::GetAllActorsOfClass(this,AActor::StaticClass(),Actors);
         for(auto* Actor:Actors)if(!Actor->Tags.Contains(TEXT("EMRuntimeCreated"))&&!Actor->Tags.Contains(TEXT("EMRuntimeTemplate"))&&Actor->Tags.Num()&&Actor->Tags[0].ToString().StartsWith(TEXT("node_")))EM_Scope.Add(Actor->Tags[0].ToString(),Actor);
        }
        static bool EM_UIActive(AActor* Actor){for(AActor* It=Actor;It;It=It->GetAttachParentActor())if(It->Tags.Contains(TEXT("EMInactiveSelf")))return false;return IsValid(Actor);}
        void AEMPortUIPresenter::BeginPlay(){Super::BeginPlay();EM_RefreshRuntimeUI();}
        void AEMPortUIPresenter::Tick(float Delta){Super::Tick(Delta);EM_RefreshRuntimeUI();}
        void AEMPortUIPresenter::EM_RefreshRuntimeUI(){
         if(Tags.Contains(TEXT("EMRuntimeTemplate"))||!EM_WidgetClass)return;
         EM_InitializeScope();AActor* ScopeOwner=GetOwner();const bool Active=EM_UIActive(ScopeOwner)&&!ScopeOwner->Tags.Contains(TEXT("EMUICanvasDisabled"));
         if(!EM_Widget&&Active){
          EM_Widget=NewObject<UUserWidget>(GetWorld(),EM_WidgetClass);if(auto* Port=Cast<UEMPortUserWidget>(EM_Widget)){Port->EM_Contextual=true;Port->EM_Scope=EM_Scope;}
          EM_Widget->Initialize();EM_Widget->AddToViewport();
         }
         if(!EM_Widget)return;EM_Widget->SetVisibility(Active?ESlateVisibility::SelfHitTestInvisible:ESlateVisibility::Collapsed);
         for(const auto& Entry:EM_Scope){const FString& Id=Entry.Key;if(Id.Contains(TEXT("_behaviour_"))||!IsValid(Entry.Value))continue;
          if(auto* Panel=EM_Widget->WidgetTree->FindWidget(*(Id+TEXT("_Panel"))))Panel->SetVisibility(EM_UIActive(Entry.Value)?ESlateVisibility::SelfHitTestInvisible:ESlateVisibility::Collapsed);
          if(auto* Button=Cast<UButton>(EM_Widget->WidgetTree->FindWidget(*(Id+TEXT("_Visual")))))Button->SetIsEnabled(!Entry.Value->Tags.Contains(TEXT("EMUIButtonDisabled"))&&!Entry.Value->Tags.Contains(TEXT("EMUIButtonNonInteractable")));
         }
        }
        void AEMPortUIPresenter::EndPlay(const EEndPlayReason::Type Reason){if(EM_Widget){EM_Widget->RemoveFromParent();EM_Widget=nullptr;}EM_Scope.Empty();Super::EndPlay(Reason);}
        """;
}
