#include "EMBlueprintLibrary.h"
#include "Modules/ModuleManager.h"
#include "Engine/Blueprint.h"
#include "Engine/BlueprintGeneratedClass.h"
#include "EdGraph/EdGraph.h"
#include "EdGraphSchema_K2.h"
#include "K2Node_FunctionEntry.h"
#include "K2Node_FunctionResult.h"
#include "K2Node_IfThenElse.h"
#include "K2Node_CallFunction.h"
#include "Kismet/KismetMathLibrary.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "Serialization/JsonSerializer.h"
#include "UObject/SavePackage.h"
#include "UObject/StructOnScope.h"
#include "UObject/UnrealType.h"
#include "Misc/PackageName.h"
#include "Engine/World.h"
#include "Engine/Engine.h"
#include "GameFramework/Actor.h"
#include "GameFramework/WorldSettings.h"
#include "Editor.h"
#include "EngineUtils.h"
#include "Components/SceneComponent.h"
#include "Components/PrimitiveComponent.h"
#include "Blueprint/WidgetBlueprintLibrary.h"
#include "Blueprint/UserWidget.h"
#include "Blueprint/WidgetTree.h"
#include "Components/Button.h"
#include "Materials/Material.h"
IMPLEMENT_MODULE(FDefaultModuleImpl, EMBlueprintBridge)
bool UEMBlueprintLibrary::HasWorldPositionOffset(UMaterial* Material){auto* Input=Material?Material->GetExpressionInputForProperty(MP_WorldPositionOffset):nullptr;return Input&&Input->Expression!=nullptr;}

namespace EM {
using J=TSharedPtr<FJsonObject>;
J Obj(){return MakeShared<FJsonObject>();}
TMap<FString,FName> Ops() {return {{TEXT("+"),TEXT("Add_DoubleDouble")},{TEXT("-"),TEXT("Subtract_DoubleDouble")},{TEXT("*"),TEXT("Multiply_DoubleDouble")},{TEXT(">"),TEXT("Greater_DoubleDouble")},{TEXT("<"),TEXT("Less_DoubleDouble")},{TEXT("=="),TEXT("EqualEqual_DoubleDouble")}};}
FEdGraphPinType DoubleType(){FEdGraphPinType T;T.PinCategory=UEdGraphSchema_K2::PC_Real;T.PinSubCategory=UEdGraphSchema_K2::PC_Double;return T;}
void Link(UEdGraphPin* A,UEdGraphPin* B){check(A&&B);check(A->Direction!=B->Direction);A->MakeLinkTo(B);}
template<class T> T* Node(UEdGraph* G){auto* N=NewObject<T>(G);G->AddNode(N,true,false);N->CreateNewGuid();N->PostPlacedNewNode();N->AllocateDefaultPins();return N;}
UEdGraphPin* Expression(const J& E,UEdGraph* G,UK2Node_FunctionEntry* Entry,UEdGraphPin* Destination,int Depth=0){
 check(Depth<128);const FString Kind=E->GetStringField(TEXT("kind"));
 if(Kind==TEXT("param")){auto* P=Entry->FindPinChecked(FName(*E->GetStringField(TEXT("name"))),EGPD_Output);Link(P,Destination);return P;}
 if(Kind==TEXT("const")){Destination->DefaultValue=FString::Printf(TEXT("%.17g"),E->GetNumberField(TEXT("value")));return nullptr;}
 check(Kind==TEXT("binary"));const FName Function=Ops().FindChecked(E->GetStringField(TEXT("op")));
 auto* N=NewObject<UK2Node_CallFunction>(G);N->SetFromFunction(UKismetMathLibrary::StaticClass()->FindFunctionByName(Function));G->AddNode(N,true,false);N->CreateNewGuid();N->PostPlacedNewNode();N->AllocateDefaultPins();
 Expression(E->GetObjectField(TEXT("left")),G,Entry,N->FindPinChecked(TEXT("A")),Depth+1);
 Expression(E->GetObjectField(TEXT("right")),G,Entry,N->FindPinChecked(TEXT("B")),Depth+1);
 Link(N->GetReturnValuePin(),Destination);return N->GetReturnValuePin();
}
void Statement(const J& B,UEdGraph* G,UK2Node_FunctionEntry* Entry,UEdGraphPin* Exec,int Depth=0){
 check(Depth<128);
 if(B->GetStringField(TEXT("kind"))==TEXT("return")){
  auto* N=NewObject<UK2Node_FunctionResult>(G);G->AddNode(N,true,false);N->CreateNewGuid();N->FunctionReference=Entry->FunctionReference;N->AllocateDefaultPins();
  auto* Ret=N->FindPin(TEXT("ReturnValue"));if(!Ret)Ret=N->CreateUserDefinedPin(TEXT("ReturnValue"),DoubleType(),EGPD_Input);
  Expression(B->GetObjectField(TEXT("value")),G,Entry,Ret);Link(Exec,N->FindPinChecked(UEdGraphSchema_K2::PN_Execute));return;
 }
 check(B->GetStringField(TEXT("kind"))==TEXT("if"));auto* N=Node<UK2Node_IfThenElse>(G);Link(Exec,N->GetExecPin());
 Expression(B->GetObjectField(TEXT("condition")),G,Entry,N->GetConditionPin());
 Statement(B->GetObjectField(TEXT("then")),G,Entry,N->GetThenPin(),Depth+1);Statement(B->GetObjectField(TEXT("else")),G,Entry,N->GetElsePin(),Depth+1);
}
J ReadExpression(UEdGraphPin* Pin,UK2Node_FunctionEntry* Entry,TSet<UEdGraphNode*>& Visited,int Depth=0){
 check(Depth<128);auto E=Obj();
 if(Pin->LinkedTo.Num()==0){check(Pin->PinType.PinCategory==UEdGraphSchema_K2::PC_Real);E->SetStringField(TEXT("kind"),TEXT("const"));E->SetNumberField(TEXT("value"),FCString::Atod(*Pin->DefaultValue));return E;}
 check(Pin->LinkedTo.Num()==1);auto* Source=Pin->LinkedTo[0];auto* Owner=Source->GetOwningNode();Visited.Add(Owner);
 if(Owner==Entry){E->SetStringField(TEXT("kind"),TEXT("param"));E->SetStringField(TEXT("name"),Source->PinName.ToString());return E;}
 auto* Call=CastChecked<UK2Node_CallFunction>(Owner);auto* Function=Call->GetTargetFunction();check(Function&&Function->GetOuterUClass()==UKismetMathLibrary::StaticClass());
 FString Op;for(const auto& Pair:Ops())if(Pair.Value==Function->GetFName())Op=Pair.Key;check(!Op.IsEmpty());
 E->SetStringField(TEXT("kind"),TEXT("binary"));E->SetStringField(TEXT("op"),Op);
 E->SetObjectField(TEXT("left"),ReadExpression(Call->FindPinChecked(TEXT("A")),Entry,Visited,Depth+1));E->SetObjectField(TEXT("right"),ReadExpression(Call->FindPinChecked(TEXT("B")),Entry,Visited,Depth+1));return E;
}
J ReadStatement(UEdGraphPin* Exec,UK2Node_FunctionEntry* Entry,TSet<UEdGraphNode*>& Visited,int Depth=0){
 check(Depth<128&&Exec->LinkedTo.Num()==1);auto* N=Exec->LinkedTo[0]->GetOwningNode();check(!Visited.Contains(N));Visited.Add(N);auto B=Obj();
 if(auto* Ret=Cast<UK2Node_FunctionResult>(N)){B->SetStringField(TEXT("kind"),TEXT("return"));B->SetObjectField(TEXT("value"),ReadExpression(Ret->FindPinChecked(TEXT("ReturnValue")),Entry,Visited));return B;}
 auto* Branch=CastChecked<UK2Node_IfThenElse>(N);B->SetStringField(TEXT("kind"),TEXT("if"));B->SetObjectField(TEXT("condition"),ReadExpression(Branch->GetConditionPin(),Entry,Visited));
 B->SetObjectField(TEXT("then"),ReadStatement(Branch->GetThenPin(),Entry,Visited,Depth+1));B->SetObjectField(TEXT("else"),ReadStatement(Branch->GetElsePin(),Entry,Visited,Depth+1));return B;
}
void SaveJson(const J& Value,const FString& Path){FString Text;auto Writer=TJsonWriterFactory<>::Create(&Text);check(FJsonSerializer::Serialize(Value.ToSharedRef(),Writer));check(FFileHelper::SaveStringToFile(Text,*Path));}
}

FString UEMBlueprintLibrary::Run(const FString& InputFile,const FString& OutputDirectory){
 using namespace EM;FString Text;check(FFileHelper::LoadFileToString(Text,*InputFile));J Input;auto Reader=TJsonReaderFactory<>::Create(Text);check(FJsonSerializer::Deserialize(Reader,Input));
 check(Input->GetIntegerField(TEXT("schema"))==1&&Input->GetStringField(TEXT("parent"))==TEXT("BlueprintFunctionLibrary"));
 FString ClassName=Input->GetStringField(TEXT("className"));check(FName::IsValidXName(ClassName,INVALID_OBJECTNAME_CHARACTERS));
 UBlueprint* BP=nullptr;FString ExistingAsset;
 if(Input->TryGetStringField(TEXT("existingAsset"),ExistingAsset)){BP=LoadObject<UBlueprint>(nullptr,*ExistingAsset);check(BP);ClassName=BP->GetName();}
 else {
 const FString PackageName=TEXT("/Game/EngineMigratorBlueprint/")+ClassName;check(!FPackageName::DoesPackageExist(PackageName));
 auto* Package=CreatePackage(*PackageName);
 BP=FKismetEditorUtilities::CreateBlueprint(UBlueprintFunctionLibrary::StaticClass(),Package,FName(*ClassName),BPTYPE_FunctionLibrary,UBlueprint::StaticClass(),UBlueprintGeneratedClass::StaticClass());check(BP);
 for(auto& V:Input->GetArrayField(TEXT("functions"))){auto F=V->AsObject();const FName Name(*F->GetStringField(TEXT("name")));
  auto* G=FBlueprintEditorUtils::CreateNewGraph(BP,Name,UEdGraph::StaticClass(),UEdGraphSchema_K2::StaticClass());FBlueprintEditorUtils::AddFunctionGraph<UClass>(BP,G,true,nullptr);
  TArray<UK2Node_FunctionEntry*> Entries;G->GetNodesOfClass(Entries);check(Entries.Num()==1);auto* Entry=Entries[0];Entry->SetExtraFlags(FUNC_Public|FUNC_Static|FUNC_BlueprintCallable);
  for(auto& A:F->GetArrayField(TEXT("args")))Entry->CreateUserDefinedPin(FName(*A->AsString()),DoubleType(),EGPD_Output);
  // CreateFunctionGraph may create a default result; replace it with explicit control flow.
  TArray<UK2Node_FunctionResult*> Results;G->GetNodesOfClass(Results);for(auto* R:Results)R->DestroyNode();
  Statement(F->GetObjectField(TEXT("body")),G,Entry,Entry->FindPinChecked(UEdGraphSchema_K2::PN_Then));
 }
 FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(BP);FKismetEditorUtilities::CompileBlueprint(BP);check(BP->Status!=BS_Error);
 FSavePackageArgs Save;Save.TopLevelFlags=RF_Public|RF_Standalone;const FString File=FPackageName::LongPackageNameToFilename(PackageName,FPackageName::GetAssetPackageExtension());check(UPackage::SavePackage(Package,BP,*File,Save));
 }
 check(BP->GeneratedClass&&BP->Status!=BS_Error);
 if(BP->ParentClass!=UBlueprintFunctionLibrary::StaticClass()||BP->NewVariables.Num()||BP->UbergraphPages.Num()||BP->MacroGraphs.Num()||BP->ImplementedInterfaces.Num())return TEXT("ERROR: only stateless BlueprintFunctionLibrary graphs are supported; inheritance/events/interfaces/state require additional conversion");
 for(UEdGraph* G:BP->FunctionGraphs)for(UEdGraphNode* N:G->Nodes){
  if(auto* Entry=Cast<UK2Node_FunctionEntry>(N)){
   if(Entry->LocalVariables.Num())return TEXT("ERROR: Blueprint local state is unsupported");
   for(auto* P:Entry->Pins){if(P->PinType.PinCategory==UEdGraphSchema_K2::PC_Exec)continue;if(P==Entry->GetAutoWorldContextPin()){if(P->LinkedTo.Num())return TEXT("ERROR: world context usage is unsupported");continue;}if(P->PinType.PinCategory!=UEdGraphSchema_K2::PC_Real||P->PinType.PinSubCategory!=UEdGraphSchema_K2::PC_Double||P->PinType.ContainerType!=EPinContainerType::None||P->PinType.bIsReference)return TEXT("ERROR: only scalar double Blueprint inputs are supported");}continue;
  }
  if(auto* Result=Cast<UK2Node_FunctionResult>(N)){int32 Values=0;for(auto* P:Result->Pins)if(P->PinType.PinCategory!=UEdGraphSchema_K2::PC_Exec){++Values;if(P->PinName!=TEXT("ReturnValue")||P->PinType.PinCategory!=UEdGraphSchema_K2::PC_Real||P->PinType.PinSubCategory!=UEdGraphSchema_K2::PC_Double||P->PinType.ContainerType!=EPinContainerType::None||P->PinType.bIsReference)return TEXT("ERROR: only one scalar double return is supported");}if(Values!=1)return TEXT("ERROR: missing or additional Blueprint outputs");continue;}
  if(Cast<UK2Node_IfThenElse>(N))continue;
  auto* Call=Cast<UK2Node_CallFunction>(N);if(!Call)return TEXT("ERROR: unsupported Blueprint node ")+N->GetClass()->GetName();
  auto* Function=Call->GetTargetFunction();bool Allowed=false;if(Function&&Function->GetOuterUClass()==UKismetMathLibrary::StaticClass())for(const auto& Pair:Ops())if(Pair.Value==Function->GetFName())Allowed=true;
  if(!Allowed)return TEXT("ERROR: unsupported Blueprint call target ")+(Function?Function->GetPathName():TEXT("unresolved"));
 }
 // Read actual node connections, not the input JSON, back into equivalent executable functions.
 auto Output=Obj();Output->SetNumberField(TEXT("schema"),1);Output->SetStringField(TEXT("className"),ClassName);Output->SetStringField(TEXT("parent"),TEXT("BlueprintFunctionLibrary"));TArray<TSharedPtr<FJsonValue>> Functions;
 check(BP->ParentClass==UBlueprintFunctionLibrary::StaticClass()&&BP->NewVariables.Num()==0&&BP->UbergraphPages.Num()==0&&BP->MacroGraphs.Num()==0&&BP->ImplementedInterfaces.Num()==0);
 for(UEdGraph* G:BP->FunctionGraphs){TArray<UK2Node_FunctionEntry*> Entries;G->GetNodesOfClass(Entries);check(Entries.Num()==1);auto* Entry=Entries[0];TSet<UEdGraphNode*> Visited;Visited.Add(Entry);auto F=Obj();F->SetStringField(TEXT("name"),G->GetName());TArray<TSharedPtr<FJsonValue>> Args;
  for(auto* P:Entry->Pins)if(P->Direction==EGPD_Output&&P->PinType.PinCategory!=UEdGraphSchema_K2::PC_Exec){if(P==Entry->GetAutoWorldContextPin()){check(P->LinkedTo.Num()==0);continue;}check(P->PinType.PinCategory==UEdGraphSchema_K2::PC_Real&&P->PinType.PinSubCategory==UEdGraphSchema_K2::PC_Double);Args.Add(MakeShared<FJsonValueString>(P->PinName.ToString()));}
  F->SetArrayField(TEXT("args"),Args);F->SetObjectField(TEXT("body"),ReadStatement(Entry->FindPinChecked(UEdGraphSchema_K2::PN_Then),Entry,Visited));check(Visited.Num()==G->Nodes.Num());Functions.Add(MakeShared<FJsonValueObject>(F));
 }
 Output->SetArrayField(TEXT("functions"),Functions);IFileManager::Get().MakeDirectory(*OutputDirectory,true);SaveJson(Output,FPaths::Combine(OutputDirectory,TEXT("blueprint-graph.json")));
 auto Verification=Obj();Verification->SetBoolField(TEXT("compiled"),true);Verification->SetStringField(TEXT("asset"),BP->GetPathName());Verification->SetBoolField(TEXT("projectPortComplete"),false);TArray<TSharedPtr<FJsonValue>> Cases;
 const TArray<TSharedPtr<FJsonValue>>* TestCases=nullptr;Input->TryGetArrayField(TEXT("cases"),TestCases);
 if(TestCases)for(auto& V:*TestCases){auto C=V->AsObject();auto* Function=BP->GeneratedClass->FindFunctionByName(FName(*C->GetStringField(TEXT("function"))));check(Function);FStructOnScope Params(Function);int32 Index=0;FDoubleProperty* Return=nullptr;
  for(TFieldIterator<FProperty> It(Function);It;++It)if(It->HasAnyPropertyFlags(CPF_Parm)){if(It->GetFName()==TEXT("__WorldContext"))continue;auto* Property=CastFieldChecked<FDoubleProperty>(*It);if(Property->HasAnyPropertyFlags(CPF_ReturnParm))Return=Property;else Property->SetPropertyValue_InContainer(Params.GetStructMemory(),C->GetArrayField(TEXT("args"))[Index++]->AsNumber());}
  BP->GeneratedClass->GetDefaultObject()->ProcessEvent(Function,Params.GetStructMemory());check(Return);auto R=Obj();R->SetStringField(TEXT("function"),C->GetStringField(TEXT("function")));R->SetNumberField(TEXT("result"),Return->GetPropertyValue_InContainer(Params.GetStructMemory()));Cases.Add(MakeShared<FJsonValueObject>(R));
 }
 Verification->SetArrayField(TEXT("cases"),Cases);SaveJson(Verification,FPaths::Combine(OutputDirectory,TEXT("blueprint-execution.json")));return BP->GetPathName();
}

FString UEMBlueprintLibrary::RunActors(const FString& InputFile,const FString& OutputDirectory){
 using namespace EM;FString Text;check(FFileHelper::LoadFileToString(Text,*InputFile));J Input;check(FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Text),Input));
 auto* Class=LoadClass<AActor>(nullptr,*Input->GetStringField(TEXT("classPath")));if(!Class)return TEXT("ERROR: generated Actor class not found");
 const auto Initialization=UWorld::InitializationValues().CreatePhysicsScene(false).CreateNavigation(false).CreateAISystem(false);
 UWorld* World=UWorld::CreateWorld(EWorldType::Game,false,NAME_None,nullptr,true,ERHIFeatureLevel::Num,&Initialization);GEngine->CreateNewWorldContext(EWorldType::Game).SetCurrentWorld(World);
 World->InitializeActorsForPlay(FURL());World->BeginPlay();World->GetWorldSettings()->NotifyBeginPlay();auto* Actor=World->SpawnActor<AActor>(Class);check(Actor);
 for(auto& Delta:Input->GetArrayField(TEXT("deltas")))Actor->Tick(static_cast<float>(Delta->AsNumber()));
 auto* Damage=Actor->FindFunction(TEXT("ApplyDamage"));check(Damage);FStructOnScope Params(Damage);
 for(TFieldIterator<FProperty> It(Damage);It;++It)if(It->HasAnyPropertyFlags(CPF_Parm)&&!It->HasAnyPropertyFlags(CPF_ReturnParm))CastFieldChecked<FDoubleProperty>(*It)->SetPropertyValue_InContainer(Params.GetStructMemory(),20.0);
 Actor->ProcessEvent(Damage,Params.GetStructMemory());Actor->Destroy();
 auto Result=Obj();for(const FString Name:{TEXT("Health"),TEXT("Position"),TEXT("AwakeCount"),TEXT("StartCount"),TEXT("TickCount"),TEXT("DestroyCount"),TEXT("Bonus")}){auto* P=FindFProperty<FDoubleProperty>(Class,FName(*Name));check(P);Result->SetNumberField(Name,P->GetPropertyValue_InContainer(Actor));}
 IFileManager::Get().MakeDirectory(*OutputDirectory,true);Result->SetBoolField(TEXT("projectPortComplete"),false);SaveJson(Result,FPaths::Combine(OutputDirectory,TEXT("actor-execution.json")));
 World->DestroyWorld(false);GEngine->DestroyWorldContext(World);return TEXT("ACTOR_EVENTS_EXECUTED");
}

FString UEMBlueprintLibrary::RunScene(const FString& InputFile,const FString& OutputDirectory){
 using namespace EM;FString Text;if(!FFileHelper::LoadFileToString(Text,*InputFile))return TEXT("ERROR: scene input missing");J Input;if(!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Text),Input))return TEXT("ERROR: scene input invalid");
 UWorld* EditorWorld=GEditor->GetEditorWorldContext().World();if(!EditorWorld)return TEXT("ERROR: no saved scene loaded");
 auto& Context=GEngine->CreateNewWorldContext(EWorldType::PIE);Context.PIEInstance=0;FString MapName;UWorld* World=GEditor->CreatePIEWorldByDuplication(Context,EditorWorld,MapName);Context.SetCurrentWorld(World);GEditor->PostCreatePIEWorld(World);
 UWorld* PreviousWorld=EditorWorld;GWorld=World;World->InitializeActorsForPlay(FURL());World->BeginPlay();World->GetWorldSettings()->NotifyBeginPlay();
 TArray<AActor*> Actors;for(TActorIterator<AActor> It(World);It;++It)if(It->GetClass()->GetPathName().StartsWith(TEXT("/Script/EMActorPort."))||(It->GetClass()->ClassGeneratedBy&&It->Tags.Num()&&It->Tags[0].ToString().StartsWith(TEXT("node_"))))Actors.Add(*It);
 TArray<TSharedPtr<FJsonValue>> Frames;
 for(auto& Delta:Input->GetArrayField(TEXT("deltas"))){++GFrameCounter;World->Tick(LEVELTICK_All,static_cast<float>(Delta->AsNumber()));
  auto Frame=Obj();TArray<TSharedPtr<FJsonValue>> Objects;for(TActorIterator<AActor> It(World);It;++It){auto* A=*It;if(A->Tags.IsEmpty()||!A->Tags[0].ToString().StartsWith(TEXT("node_"))||A->Tags[0].ToString().Contains(TEXT("_behaviour_")))continue;auto Value=Obj();Value->SetStringField(TEXT("id"),A->Tags[0].ToString());auto P=A->GetActorLocation();auto Position=Obj();Position->SetNumberField(TEXT("x"),P.Y/100);Position->SetNumberField(TEXT("y"),P.Z/100);Position->SetNumberField(TEXT("z"),P.X/100);Value->SetObjectField(TEXT("position"),Position);auto S=A->GetActorScale3D();auto Scale=Obj();Scale->SetNumberField(TEXT("x"),S.Y);Scale->SetNumberField(TEXT("y"),S.Z);Scale->SetNumberField(TEXT("z"),S.X);Value->SetObjectField(TEXT("scale"),Scale);Objects.Add(MakeShared<FJsonValueObject>(Value));}Frame->SetArrayField(TEXT("objects"),Objects);Frames.Add(MakeShared<FJsonValueObject>(Frame));
 }
 int32 ClicksExecuted=0;const TArray<TSharedPtr<FJsonValue>>* Clicks=nullptr;if(Input->TryGetArrayField(TEXT("clicks"),Clicks)){TArray<UUserWidget*> ClickWidgets;UWidgetBlueprintLibrary::GetAllWidgetsOfClass(World,ClickWidgets,UUserWidget::StaticClass(),false);for(auto& Request:*Clicks){const auto Data=Request->AsObject();for(auto* Widget:ClickWidgets)if(auto* Button=Widget->WidgetTree->FindWidget<UButton>(*Data->GetStringField(TEXT("button"))))for(int32 I=0;I<Data->GetIntegerField(TEXT("count"));++I){Button->OnClicked.Broadcast();++ClicksExecuted;}}}
 auto ReadState=[](AActor* Actor){auto State=Obj();for(TFieldIterator<FProperty> It(Actor->GetClass());It;++It){if(!It->GetOwnerClass()->GetPathName().StartsWith(TEXT("/Script/EMActorPort."))&&!It->GetOwnerClass()->GetPathName().StartsWith(TEXT("/Game/")))continue;
  if(auto* Number=CastField<FNumericProperty>(*It)){if(Number->IsFloatingPoint())State->SetNumberField(It->GetName(),Number->GetFloatingPointPropertyValue(It->ContainerPtrToValuePtr<void>(Actor)));else if(Number->IsInteger())State->SetNumberField(It->GetName(),Number->GetSignedIntPropertyValue(It->ContainerPtrToValuePtr<void>(Actor)));}
  else if(auto* Flag=CastField<FBoolProperty>(*It))State->SetBoolField(It->GetName(),Flag->GetPropertyValue_InContainer(Actor));
  else if(auto* Struct=CastField<FStructProperty>(*It)){if(Struct->Struct->GetName()==TEXT("Vector3f")){auto V=*Struct->ContainerPtrToValuePtr<FVector3f>(Actor);auto Value=Obj();Value->SetNumberField(TEXT("x"),V.X);Value->SetNumberField(TEXT("y"),V.Y);Value->SetNumberField(TEXT("z"),V.Z);State->SetObjectField(It->GetName(),Value);}}
 }return State;};
 auto Position=[](const FVector& V){auto Value=Obj();Value->SetNumberField(TEXT("x"),V.Y/100.0);Value->SetNumberField(TEXT("y"),V.Z/100.0);Value->SetNumberField(TEXT("z"),V.X/100.0);return Value;};
 bool EndPlayOnly=false;Input->TryGetBoolField(TEXT("endPlayOnly"),EndPlayOnly);
 TArray<TSharedPtr<FJsonValue>> Results;for(auto* Actor:Actors){auto Result=Obj();FString Identity=Actor->Tags.Num()?Actor->Tags[0].ToString():TEXT("");Result->SetStringField(TEXT("id"),Identity.Contains(TEXT("_behaviour_"))?Identity:Identity+TEXT("_behaviour_0"));Result->SetStringField(TEXT("className"),Actor->GetClass()->ClassGeneratedBy?Actor->GetClass()->GetName().LeftChop(2):Actor->GetClass()->GetName());auto* Parent=Actor->GetAttachParentActor();Result->SetStringField(TEXT("owner"),Parent&&Parent->Tags.Num()?Parent->Tags[0].ToString():Identity);auto* Owner=Parent?Parent:Actor;Result->SetObjectField(TEXT("position"),Position(Owner->GetActorLocation()));Result->SetObjectField(TEXT("localPosition"),Position(Owner->GetRootComponent()->GetRelativeLocation()));Result->SetObjectField(TEXT("state"),ReadState(Actor));if(!EndPlayOnly){Actor->Destroy();Result->SetObjectField(TEXT("destroyState"),ReadState(Actor));}Results.Add(MakeShared<FJsonValueObject>(Result));}
 if(EndPlayOnly){World->EndPlay(EEndPlayReason::EndPlayInEditor);for(int32 Index=0;Index<Actors.Num();++Index)Results[Index]->AsObject()->SetObjectField(TEXT("destroyState"),ReadState(Actors[Index]));}
 auto Result=Obj();Result->SetArrayField(TEXT("actors"),Results);Result->SetArrayField(TEXT("frames"),Frames);TArray<UUserWidget*> Widgets;UWidgetBlueprintLibrary::GetAllWidgetsOfClass(World,Widgets,UUserWidget::StaticClass(),false);TArray<TSharedPtr<FJsonValue>> WidgetValues;for(auto* Widget:Widgets){auto Value=Obj();Value->SetStringField(TEXT("class"),Widget->GetClass()->GetPathName());Value->SetBoolField(TEXT("inViewport"),Widget->IsInViewport());TArray<UWidget*> Children;Widget->WidgetTree->GetAllWidgets(Children);Value->SetNumberField(TEXT("widgets"),Children.Num());WidgetValues.Add(MakeShared<FJsonValueObject>(Value));}Result->SetArrayField(TEXT("widgets"),WidgetValues);Result->SetNumberField(TEXT("clicksExecuted"),ClicksExecuted);Result->SetBoolField(TEXT("endPlayOnly"),EndPlayOnly);Result->SetBoolField(TEXT("savedSceneExecuted"),true);Result->SetBoolField(TEXT("worldTickExecuted"),true);Result->SetBoolField(TEXT("projectPortComplete"),false);IFileManager::Get().MakeDirectory(*OutputDirectory,true);SaveJson(Result,FPaths::Combine(OutputDirectory,TEXT("scene-execution.json")));
 World->EndPlay(EEndPlayReason::EndPlayInEditor);World->DestroyWorld(false,EditorWorld);GEngine->DestroyWorldContext(World);GWorld=PreviousWorld;return TEXT("SCENE_EVENTS_EXECUTED");
}
