#include "EMBlueprintLibrary.h"
#include "Engine/Blueprint.h"
#include "Engine/BlueprintGeneratedClass.h"
#include "GameFramework/Actor.h"
#include "EdGraph/EdGraph.h"
#include "EdGraphSchema_K2.h"
#include "K2Node_Event.h"
#include "K2Node_CustomEvent.h"
#include "K2Node_VariableGet.h"
#include "K2Node_VariableSet.h"
#include "K2Node_IfThenElse.h"
#include "K2Node_CallFunction.h"
#include "K2Node_CallParentFunction.h"
#include "K2Node_FunctionEntry.h"
#include "K2Node_FunctionResult.h"
#include "Kismet/KismetMathLibrary.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "Serialization/JsonSerializer.h"
#include "Misc/PackageName.h"
#include "UObject/SavePackage.h"
#include "UObject/UnrealType.h"
#include "UObject/MetaData.h"
#include "Engine/SimpleConstructionScript.h"
#include "Engine/SCS_Node.h"
#include "Components/SceneComponent.h"
#include "K2Node_Knot.h"
#include "EdGraphNode_Comment.h"

namespace EMAG {
using J=TSharedPtr<FJsonObject>;using V=TSharedPtr<FJsonValue>;int ReadBudget=8192;
J Obj(){return MakeShared<FJsonObject>();}
FString Text(const J& O){FString S;FJsonSerializer::Serialize(O.ToSharedRef(),TJsonWriterFactory<>::Create(&S));return S;}
TMap<FString,FName> Ops(){return {{TEXT("+"),TEXT("Add_DoubleDouble")},{TEXT("-"),TEXT("Subtract_DoubleDouble")},{TEXT("*"),TEXT("Multiply_DoubleDouble")},{TEXT(">"),TEXT("Greater_DoubleDouble")},{TEXT("<"),TEXT("Less_DoubleDouble")},{TEXT("=="),TEXT("EqualEqual_DoubleDouble")}};}
FName Event(const FString& Name){return Name==TEXT("Start")?TEXT("ReceiveBeginPlay"):Name==TEXT("Update")?TEXT("ReceiveTick"):Name==TEXT("OnDestroy")?FName(TEXT("ReceiveEndPlay")):FName(NAME_None);}
FString EventName(FName Name){return Name==TEXT("ReceiveBeginPlay")?TEXT("Start"):Name==TEXT("ReceiveTick")?TEXT("Update"):Name==TEXT("ReceiveEndPlay")?TEXT("OnDestroy"):TEXT("");}
void Link(UEdGraphPin* A,UEdGraphPin* B){check(A&&B);GetDefault<UEdGraphSchema_K2>()->TryCreateConnection(A,B);}
template<class T>T* New(UEdGraph* G){auto* N=NewObject<T>(G);G->AddNode(N);N->CreateNewGuid();N->PostPlacedNewNode();return N;}
void Expr(const J& E,UEdGraph* G,UK2Node_Event* Entry,UEdGraphPin* To,int Depth=0){
 check(Depth<128);auto Kind=E->GetStringField(TEXT("kind"));
 if(Kind==TEXT("const")){const auto Value=E->Values[TEXT("value")];To->DefaultValue=Value->Type==EJson::Boolean?(Value->AsBool()?TEXT("true"):TEXT("false")):FString::Printf(TEXT("%.17g"),Value->AsNumber());return;}
 if(Kind==TEXT("param")){Link(Entry->FindPinChecked(*E->GetStringField(TEXT("name"))),To);return;}
 if(Kind==TEXT("field")){auto* N=New<UK2Node_VariableGet>(G);N->VariableReference.SetSelfMember(*E->GetStringField(TEXT("name")));N->AllocateDefaultPins();Link(N->GetValuePin(),To);return;}
 check(Kind==TEXT("binary"));auto IsBool=[G](const J& X){const auto K=X->GetStringField(TEXT("kind"));if(K==TEXT("const"))return X->Values[TEXT("value")]->Type==EJson::Boolean;if(K==TEXT("binary"))return X->GetStringField(TEXT("op"))==TEXT(">")||X->GetStringField(TEXT("op"))==TEXT("<")||X->GetStringField(TEXT("op"))==TEXT("==");if(K==TEXT("field")){auto* Blueprint=G->GetTypedOuter<UBlueprint>();return FindFProperty<FBoolProperty>(Blueprint->GeneratedClass,*X->GetStringField(TEXT("name")))!=nullptr;}return false;};FName Target=E->GetStringField(TEXT("op"))==TEXT("==")&&IsBool(E->GetObjectField(TEXT("left")))&&IsBool(E->GetObjectField(TEXT("right")))?FName(TEXT("EqualEqual_BoolBool")):Ops().FindChecked(E->GetStringField(TEXT("op")));auto* N=New<UK2Node_CallFunction>(G);N->SetFromFunction(UKismetMathLibrary::StaticClass()->FindFunctionByName(Target));N->AllocateDefaultPins();Expr(E->GetObjectField(TEXT("left")),G,Entry,N->FindPinChecked(TEXT("A")),Depth+1);Expr(E->GetObjectField(TEXT("right")),G,Entry,N->FindPinChecked(TEXT("B")),Depth+1);Link(N->GetReturnValuePin(),To);
}
void Body(TArray<V> Statements,UEdGraph* G,UBlueprint* BP,UK2Node_Event* Entry,UEdGraphPin* From,int Depth=0){
 check(Depth<128);if(Statements.IsEmpty())return;auto S=Statements[0]->AsObject();Statements.RemoveAt(0);const auto Kind=S->GetStringField(TEXT("kind"));
 if(Kind==TEXT("if")){auto* N=New<UK2Node_IfThenElse>(G);N->AllocateDefaultPins();Link(From,N->GetExecPin());Expr(S->GetObjectField(TEXT("condition")),G,Entry,N->GetConditionPin());auto Then=S->GetArrayField(TEXT("then"));Then.Append(Statements);auto Else=S->GetArrayField(TEXT("else"));Else.Append(Statements);Body(Then,G,BP,Entry,N->GetThenPin(),Depth+1);Body(Else,G,BP,Entry,N->GetElsePin(),Depth+1);return;}
 if(Kind==TEXT("set")){auto* N=New<UK2Node_VariableSet>(G);N->VariableReference.SetSelfMember(*S->GetStringField(TEXT("name")));N->AllocateDefaultPins();Link(From,N->GetExecPin());Expr(S->GetObjectField(TEXT("value")),G,Entry,N->FindPinChecked(N->VariableReference.GetMemberName(),EGPD_Input));Body(Statements,G,BP,Entry,N->GetThenPin(),Depth+1);return;}
 check(Kind==TEXT("parent"));auto* N=New<UK2Node_CallParentFunction>(G);N->SetFromFunction(BP->ParentClass->FindFunctionByName(Event(S->GetStringField(TEXT("event")))));N->AllocateDefaultPins();Link(From,N->GetExecPin());if(auto* Input=N->FindPin(TEXT("DeltaSeconds")))Link(Entry->FindPinChecked(TEXT("DeltaSeconds")),Input);if(auto* Reason=N->FindPin(TEXT("EndPlayReason")))Link(Entry->FindPinChecked(TEXT("EndPlayReason")),Reason);Body(Statements,G,BP,Entry,N->GetThenPin(),Depth+1);
}
J ReadExpr(UEdGraphPin* P,UK2Node_Event* Entry,TSet<UEdGraphNode*>& Used,FString& Error,int Depth=0){
 if(!P||Depth>128||--ReadBudget<0){Error=TEXT("invalid data pin/depth/budget");return nullptr;}auto O=Obj();
 if(P->LinkedTo.IsEmpty()){O->SetStringField(TEXT("kind"),TEXT("const"));if(P->PinType.PinCategory==UEdGraphSchema_K2::PC_Boolean)O->SetBoolField(TEXT("value"),P->DefaultValue==TEXT("true"));else if(P->PinType.PinCategory==UEdGraphSchema_K2::PC_Real)O->SetNumberField(TEXT("value"),FCString::Atod(*P->DefaultValue));else{Error=TEXT("unsupported unlinked value type");return nullptr;}return O;}
 if(P->LinkedTo.Num()!=1){Error=TEXT("multiple data links");return nullptr;}auto* Source=P->LinkedTo[0];auto* N=Source->GetOwningNode();Used.Add(N);
 if(auto* Knot=Cast<UK2Node_Knot>(N))return ReadExpr(Knot->GetInputPin(),Entry,Used,Error,Depth+1);
 if(N==Entry&&Source->PinName==TEXT("DeltaSeconds")){O->SetStringField(TEXT("kind"),TEXT("param"));O->SetStringField(TEXT("name"),TEXT("DeltaSeconds"));return O;}
 if(auto* Get=Cast<UK2Node_VariableGet>(N)){if(Get->VariableReference.IsSelfContext()){O->SetStringField(TEXT("kind"),TEXT("field"));O->SetStringField(TEXT("name"),Get->VariableReference.GetMemberName().ToString());return O;}}
 if(auto* Call=Cast<UK2Node_CallFunction>(N)){auto* F=Call->GetTargetFunction();if(F&&F->GetOuterUClass()==UKismetMathLibrary::StaticClass()){
  if(F->GetName()==TEXT("Conv_DoubleToFloat")&&!P->GetOwningNode()->IsA<UK2Node_VariableSet>()){Error=TEXT("intermediate float narrowing requires a typed graph conversion");return nullptr;}if(F->GetName()==TEXT("Conv_FloatToDouble")||F->GetName()==TEXT("Conv_DoubleToFloat")){for(auto* Pin:Call->Pins)if(Pin->Direction==EGPD_Input&&Pin->PinType.PinCategory==UEdGraphSchema_K2::PC_Real)return ReadExpr(Pin,Entry,Used,Error,Depth+1);}
  auto Operations=Ops();Operations.Add(TEXT("bool=="),TEXT("EqualEqual_BoolBool"));for(auto& Pair:Operations)if(Pair.Value==F->GetFName()){auto A=ReadExpr(Call->FindPin(TEXT("A")),Entry,Used,Error,Depth+1);auto B=ReadExpr(Call->FindPin(TEXT("B")),Entry,Used,Error,Depth+1);if(!A||!B)return nullptr;O->SetStringField(TEXT("kind"),TEXT("binary"));O->SetStringField(TEXT("op"),Pair.Key==TEXT("bool==")?TEXT("=="):Pair.Key);O->SetObjectField(TEXT("left"),A);O->SetObjectField(TEXT("right"),B);return O;}
 }}Error=TEXT("unsupported data node/call target: ")+N->GetClass()->GetName();return nullptr;
}
bool ReadBody(UEdGraphPin* P,UK2Node_Event* Entry,TSet<UEdGraphNode*>& Used,TSet<UEdGraphNode*> Path,TArray<V>& Result,FString& Error,int Depth=0){
 if(!P||Depth>128||--ReadBudget<0){Error=TEXT("invalid execution pin/depth/budget");return false;}if(P->LinkedTo.IsEmpty())return true;if(P->LinkedTo.Num()!=1){Error=TEXT("execution fanout unsupported");return false;}auto* N=P->LinkedTo[0]->GetOwningNode();if(Path.Contains(N)){Error=TEXT("execution cycle unsupported");return false;}Path.Add(N);Used.Add(N);auto O=Obj();
 if(auto* Knot=Cast<UK2Node_Knot>(N))return ReadBody(Knot->GetOutputPin(),Entry,Used,Path,Result,Error,Depth+1);
 if(auto* Set=Cast<UK2Node_VariableSet>(N)){if(!Set->VariableReference.IsSelfContext()){Error=TEXT("external object mutation unsupported");return false;}auto E=ReadExpr(Set->FindPin(Set->VariableReference.GetMemberName(),EGPD_Input),Entry,Used,Error);if(!E)return false;O->SetStringField(TEXT("kind"),TEXT("set"));O->SetStringField(TEXT("name"),Set->VariableReference.GetMemberName().ToString());O->SetObjectField(TEXT("value"),E);Result.Add(MakeShared<FJsonValueObject>(O));return ReadBody(Set->GetThenPin(),Entry,Used,Path,Result,Error,Depth+1);}
 if(auto* Branch=Cast<UK2Node_IfThenElse>(N)){auto E=ReadExpr(Branch->GetConditionPin(),Entry,Used,Error);if(!E)return false;TArray<V> A,B;if(!ReadBody(Branch->GetThenPin(),Entry,Used,Path,A,Error,Depth+1)||!ReadBody(Branch->GetElsePin(),Entry,Used,Path,B,Error,Depth+1))return false;O->SetStringField(TEXT("kind"),TEXT("if"));O->SetObjectField(TEXT("condition"),E);O->SetArrayField(TEXT("then"),A);O->SetArrayField(TEXT("else"),B);Result.Add(MakeShared<FJsonValueObject>(O));return true;}
 if(auto* Parent=Cast<UK2Node_CallParentFunction>(N)){auto* F=Parent->GetTargetFunction();auto Name=F?EventName(F->GetFName()):TEXT("");if(Name.IsEmpty()||F->GetFName()!=Entry->EventReference.GetMemberName()){Error=TEXT("unmatched parent call target");return false;}if(auto* Delta=Parent->FindPin(TEXT("DeltaSeconds"))){auto E=ReadExpr(Delta,Entry,Used,Error);if(!E||E->GetStringField(TEXT("kind"))!=TEXT("param")){Error=TEXT("modified parent Tick input");return false;}}O->SetStringField(TEXT("kind"),TEXT("parent"));O->SetStringField(TEXT("event"),Name);Result.Add(MakeShared<FJsonValueObject>(O));return ReadBody(Parent->GetThenPin(),Entry,Used,Path,Result,Error,Depth+1);}
 Error=TEXT("unsupported execution node: ")+N->GetClass()->GetName();return false;
}
J Read(UBlueprint* BP,FString& Error){ReadBudget=8192;
 if(!BP||!BP->GeneratedClass||BP->MacroGraphs.Num()||BP->ImplementedInterfaces.Num()){Error=TEXT("unsupported Actor macros/interfaces");return nullptr;}
 if(BP->SimpleConstructionScript){auto Nodes=BP->SimpleConstructionScript->GetAllNodes();if(Nodes.Num()>1){Error=TEXT("additional Actor Blueprint components require separate conversion");return nullptr;}for(USCS_Node* N:Nodes)if(N->ComponentClass!=USceneComponent::StaticClass()){Error=TEXT("unported Actor Blueprint component: ")+N->GetVariableName().ToString();return nullptr;}}
 for(UEdGraph* G:BP->FunctionGraphs){if(G->GetFName()!=TEXT("UserConstructionScript")){Error=TEXT("unsupported Actor function: ")+G->GetName();return nullptr;}for(UEdGraphNode* N:G->Nodes){if(Cast<UK2Node_FunctionEntry>(N)||Cast<UK2Node_FunctionResult>(N))continue;auto* Call=Cast<UK2Node_CallParentFunction>(N);if(!Call||Call->FunctionReference.GetMemberName()!=TEXT("UserConstructionScript")){Error=TEXT("nonempty Actor construction graph");return nullptr;}}}
 auto O=Obj();O->SetStringField(TEXT("name"),BP->GetName());O->SetStringField(TEXT("asset"),BP->GetPathName());
 auto* Parent=Cast<UBlueprint>(BP->ParentClass->ClassGeneratedBy);if(!Parent&&BP->ParentClass!=AActor::StaticClass()){Error=TEXT("unsupported native Actor inheritance");return nullptr;}O->SetStringField(TEXT("parent"),Parent?Parent->GetName():TEXT("MonoBehaviour"));
 TArray<V> Fields,Events;auto* CDO=BP->GeneratedClass->GetDefaultObject();for(auto& Var:BP->NewVariables){auto Field=Obj();Field->SetStringField(TEXT("name"),Var.VarName.ToString());FString Access,Serialized;FBlueprintEditorUtils::GetBlueprintVariableMetaData(BP,Var.VarName,nullptr,TEXT("EMSourceAccess"),Access);FBlueprintEditorUtils::GetBlueprintVariableMetaData(BP,Var.VarName,nullptr,TEXT("EMSourceSerializeField"),Serialized);Field->SetStringField(TEXT("access"),Access.IsEmpty()?TEXT("public"):Access);Field->SetBoolField(TEXT("serialized"),Serialized==TEXT("true"));auto* Property=FindFProperty<FProperty>(BP->GeneratedClass,Var.VarName);
  if(auto* Number=CastField<FNumericProperty>(Property)){if(!Number->IsFloatingPoint()){Error=TEXT("non-floating Actor state unsupported");return nullptr;}Field->SetStringField(TEXT("type"),CastField<FFloatProperty>(Property)?TEXT("float"):TEXT("double"));Field->SetNumberField(TEXT("value"),Number->GetFloatingPointPropertyValue(Number->ContainerPtrToValuePtr<void>(CDO)));}
  else if(auto* Flag=CastField<FBoolProperty>(Property)){Field->SetStringField(TEXT("type"),TEXT("bool"));Field->SetBoolField(TEXT("value"),Flag->GetPropertyValue_InContainer(CDO));}else {Error=TEXT("unsupported Actor field type");return nullptr;}Fields.Add(MakeShared<FJsonValueObject>(Field));}
 for(UEdGraph* G:BP->UbergraphPages){TSet<UEdGraphNode*> Used;for(UEdGraphNode* N:G->Nodes)if(auto* Entry=Cast<UK2Node_Event>(N)){auto* Custom=Cast<UK2Node_CustomEvent>(Entry);auto Name=Custom?Custom->CustomFunctionName.ToString():EventName(Entry->EventReference.GetMemberName());if(Custom){auto* Function=BP->GeneratedClass->FindFunctionByName(Custom->CustomFunctionName);if(!Function||Function->ParmsSize!=0){Error=TEXT("only zero-argument void custom events are supported");return nullptr;}}else if(Name.IsEmpty()||!Entry->bOverrideFunction){Error=TEXT("unsupported Actor event signature");return nullptr;}Used.Add(Entry);auto E=Obj();E->SetStringField(TEXT("name"),Name);E->SetBoolField(TEXT("custom"),Custom!=nullptr);TArray<V> Statements;if(!ReadBody(Entry->FindPin(UEdGraphSchema_K2::PN_Then),Entry,Used,{},Statements,Error))return nullptr;E->SetArrayField(TEXT("body"),Statements);Events.Add(MakeShared<FJsonValueObject>(E));}for(UEdGraphNode* N:G->Nodes)if(!Used.Contains(N)&&!N->IsA<UEdGraphNode_Comment>()){Error=TEXT("unported/unreachable graph node: ")+N->GetClass()->GetName();return nullptr;}}
 O->SetArrayField(TEXT("fields"),Fields);O->SetArrayField(TEXT("events"),Events);return O;
}
}
FString UEMBlueprintLibrary::ActorGraph(const FString& Json){
 using namespace EMAG;J Input;if(!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Json),Input))return TEXT("ERROR: invalid Actor graph JSON");
 TArray<UBlueprint*> Blueprints;const TArray<V>* Classes=nullptr;FString Error;
 if(Input->TryGetArrayField(TEXT("classes"),Classes)){
  TMap<FString,UClass*> Types;FString Root=TEXT("/Game/EngineMigratorActors");Input->TryGetStringField(TEXT("assetRoot"),Root);if(!Root.StartsWith(TEXT("/Game/"))||Root.Contains(TEXT("..")))return TEXT("ERROR: invalid asset root");
  for(auto& Class:*Classes){auto C=Class->AsObject();auto Name=C->GetStringField(TEXT("name")),Parent=C->GetStringField(TEXT("parent"));if(!FName::IsValidXName(Name,INVALID_OBJECTNAME_CHARACTERS))return TEXT("ERROR: invalid class name");auto Path=Root+TEXT("/")+Name;if(FPackageName::DoesPackageExist(Path))return TEXT("ERROR: Actor Blueprint already exists");auto* Base=Parent==TEXT("MonoBehaviour")?AActor::StaticClass():Types.FindRef(Parent);if(!Base)return TEXT("ERROR: missing parent class");auto* Package=CreatePackage(*Path);auto* BP=FKismetEditorUtilities::CreateBlueprint(Base,Package,*Name,BPTYPE_Normal,UBlueprint::StaticClass(),UBlueprintGeneratedClass::StaticClass());
   BP->UbergraphPages.Empty();for(auto& Field:C->GetArrayField(TEXT("fields"))){auto F=Field->AsObject();FEdGraphPinType T;const auto Type=F->GetStringField(TEXT("type"));T.PinCategory=Type==TEXT("bool")?UEdGraphSchema_K2::PC_Boolean:UEdGraphSchema_K2::PC_Real;T.PinSubCategory=Type==TEXT("bool")?NAME_None:Type==TEXT("float")?UEdGraphSchema_K2::PC_Float:UEdGraphSchema_K2::PC_Double;const auto Value=F->Values[TEXT("value")];FString Default=Type==TEXT("bool")?(Value->AsBool()?TEXT("true"):TEXT("false")):FString::Printf(TEXT("%.17g"),Value->AsNumber());if(!FBlueprintEditorUtils::AddMemberVariable(BP,*F->GetStringField(TEXT("name")),T,Default))return TEXT("ERROR: failed Actor field");FBlueprintEditorUtils::SetBlueprintOnlyEditableFlag(BP,*F->GetStringField(TEXT("name")),false);FString Access;if(F->TryGetStringField(TEXT("access"),Access))FBlueprintEditorUtils::SetBlueprintVariableMetaData(BP,*F->GetStringField(TEXT("name")),nullptr,TEXT("EMSourceAccess"),Access);bool Serialized=false;if(F->TryGetBoolField(TEXT("serialized"),Serialized))FBlueprintEditorUtils::SetBlueprintVariableMetaData(BP,*F->GetStringField(TEXT("name")),nullptr,TEXT("EMSourceSerializeField"),Serialized?TEXT("true"):TEXT("false"));}
   FKismetEditorUtilities::CompileBlueprint(BP);if(BP->Status==BS_Error)return TEXT("ERROR: field compilation failed");auto* G=FBlueprintEditorUtils::CreateNewGraph(BP,TEXT("EventGraph"),UEdGraph::StaticClass(),UEdGraphSchema_K2::StaticClass());FBlueprintEditorUtils::AddUbergraphPage(BP,G);
   for(auto& Event:C->GetArrayField(TEXT("events"))){auto E=Event->AsObject();FName Function=EMAG::Event(E->GetStringField(TEXT("name")));UK2Node_Event* Entry=nullptr;bool Custom=false;E->TryGetBoolField(TEXT("custom"),Custom);if(Custom){const FName CallbackName=*E->GetStringField(TEXT("name"));if(BP->ParentClass->FindFunctionByName(CallbackName))return TEXT("ERROR: inherited custom callback override unsupported");auto* Node=New<UK2Node_CustomEvent>(G);Node->CustomFunctionName=CallbackName;Node->FunctionFlags=FUNC_Public|FUNC_BlueprintCallable|FUNC_BlueprintEvent;Entry=Node;}else {if(Function.IsNone())return TEXT("ERROR: unsupported event");Entry=New<UK2Node_Event>(G);Entry->EventReference.SetExternalMember(Function,AActor::StaticClass());Entry->bOverrideFunction=true;}Entry->AllocateDefaultPins();Body(E->GetArrayField(TEXT("body")),G,BP,Entry,Entry->FindPinChecked(UEdGraphSchema_K2::PN_Then));}
   FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(BP);FKismetEditorUtilities::CompileBlueprint(BP);if(BP->Status==BS_Error)return TEXT("ERROR: Actor graph compilation failed");CastChecked<AActor>(BP->GeneratedClass->GetDefaultObject())->PrimaryActorTick.bCanEverTick=true;FSavePackageArgs Args;Args.TopLevelFlags=RF_Public|RF_Standalone;BP->SetFlags(RF_Public|RF_Standalone);if(!UPackage::SavePackage(Package,BP,*FPackageName::LongPackageNameToFilename(Path,FPackageName::GetAssetPackageExtension()),Args))return TEXT("ERROR: Actor graph save failed");Types.Add(Name,BP->GeneratedClass);Blueprints.Add(BP);
  }
 }else {const TArray<V>* Assets=nullptr;if(!Input->TryGetArrayField(TEXT("assets"),Assets))return TEXT("ERROR: missing Actor assets");TSet<UBlueprint*> Added;TFunction<bool(UBlueprint*,int)> Collect=[&](UBlueprint* BP,int Depth){if(!BP||Depth>64)return false;if(Added.Contains(BP))return true;if(auto* Parent=Cast<UBlueprint>(BP->ParentClass->ClassGeneratedBy))if(!Collect(Parent,Depth+1))return false;Added.Add(BP);Blueprints.Add(BP);return true;};for(auto& Asset:*Assets){auto* BP=LoadObject<UBlueprint>(nullptr,*Asset->AsString());if(!Collect(BP,0))return TEXT("ERROR: Actor asset/parent missing or too deep");}}
 TArray<V> Output;for(auto* BP:Blueprints){auto C=Read(BP,Error);if(!C)return TEXT("ERROR: ")+Error;Output.Add(MakeShared<FJsonValueObject>(C));}auto O=Obj();O->SetNumberField(TEXT("schema"),1);O->SetStringField(TEXT("parent"),TEXT("Actor"));O->SetArrayField(TEXT("classes"),Output);return Text(O);
}
