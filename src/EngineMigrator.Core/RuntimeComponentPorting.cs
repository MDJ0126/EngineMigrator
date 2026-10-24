namespace EngineMigrator.Core;
internal static class RuntimeComponentPorting
{
    internal const string BeginPlayBody="Super::BeginPlay(); if(Tags.Contains(TEXT(\"EMRuntimeTemplate\")))SetActorTickEnabled(false);else if(Tags.Contains(TEXT(\"EMRuntimeAdded\"))||Tags.Contains(TEXT(\"EMRuntimeClone\")))EM_DeferStart();else EM_StartOnce();";
    internal const string TickBody="Super::Tick(DeltaSeconds); if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))&&EM_Started)EM_Update(DeltaSeconds);";
    internal static string Lifecycle(string name)=>"""
bool EM_Started=false;
void EM_StartOnce(){if(!EM_Started){EM_Started=true;EM_Start();}}
void EM_DeferStart(){
 TWeakObjectPtr<EM_CLASS> EM_Self=this;TWeakObjectPtr<UWorld> EM_World=GetWorld();auto EM_Handle=MakeShared<FDelegateHandle>();
 *EM_Handle=FWorldDelegates::OnWorldPostActorTick.AddLambda([EM_Self,EM_World,EM_Handle](UWorld* EM_Ticking,ELevelTick,float){
  if(EM_Ticking!=EM_World.Get())return;EM_CLASS* EM_Actor=EM_Self.Get();FDelegateHandle EM_Remove=*EM_Handle;
  FWorldDelegates::OnWorldPostActorTick.Remove(EM_Remove);if(IsValid(EM_Actor))EM_Actor->EM_StartOnce();
 });
}

""".Replace("EM_CLASS","A"+name);
    internal const string Includes="#include <type_traits>\n#include \"UObject/StructOnScope.h\"\n";
    internal const string Helpers="""
protected:
static bool EM_ComponentEqual(const AActor* EM_A,const AActor* EM_B){if(!EM_A)return !IsValid(EM_B);if(!EM_B)return !IsValid(EM_A);return EM_A==EM_B;}
static AActor* EM_ComponentOwner(AActor* EM_Component){return EM_Component->GetAttachParentActor()?EM_Component->GetAttachParentActor():EM_Component;}
static int32 EM_ComponentOrder(AActor* EM_Component){for(const FName& EM_Tag:EM_Component->Tags){const FString EM_Text=EM_Tag.ToString();if(EM_Text.StartsWith(TEXT("EMComponentOrder:")))return FCString::Atoi(*EM_Text.RightChop(17));}return MAX_int32;}
static AActor* EM_GetComponent(AActor* EM_Owner,const TCHAR* EM_ClassName){
 UClass* EM_Class=LoadClass<AActor>(nullptr,EM_ClassName);if(!EM_Class)return nullptr;
 TArray<AActor*> EM_Children;EM_Owner->GetAttachedActors(EM_Children);AActor* EM_Result=nullptr;int32 EM_Order=MAX_int32;
 for(AActor* EM_Child:EM_Children)if(IsValid(EM_Child)&&EM_Child->IsA(EM_Class)){int32 EM_Current=EM_ComponentOrder(EM_Child);if(!EM_Result||EM_Current<EM_Order){EM_Result=EM_Child;EM_Order=EM_Current;}}
 return EM_Result;
}
AActor* EM_AddComponent(AActor* EM_Owner,const TCHAR* EM_ClassName){
 UClass* EM_Class=LoadClass<AActor>(nullptr,EM_ClassName);if(!EM_Class)return nullptr;
 TArray<AActor*> EM_Children;EM_Owner->GetAttachedActors(EM_Children);int32 EM_Order=0;for(AActor* EM_Child:EM_Children){int32 EM_Current=EM_ComponentOrder(EM_Child);if(EM_Current!=MAX_int32)EM_Order=FMath::Max(EM_Order,EM_Current+1);}
 FActorSpawnParameters EM_Params;EM_Params.bDeferConstruction=true;EM_Params.SpawnCollisionHandlingOverride=ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
 AActor* EM_New=GetWorld()->SpawnActor<AActor>(EM_Class,EM_Owner->GetActorTransform(),EM_Params);if(!EM_New)return nullptr;
 EM_New->Tags.Add(TEXT("EMRuntimeAdded"));EM_New->Tags.Add(FName(*FString::Printf(TEXT("EMComponentOrder:%d"),EM_Order)));EM_New->AttachToActor(EM_Owner,FAttachmentTransformRules::KeepWorldTransform);EM_New->AddTickPrerequisiteActor(this);EM_New->FinishSpawning(EM_Owner->GetActorTransform(),true);return EM_New;
}
template<typename EM_T>static EM_T EM_ComponentRead(AActor* EM_Object,const TCHAR* EM_Name){
 EM_T EM_Value{};FProperty* EM_P=EM_Object->GetClass()->FindPropertyByName(FName(EM_Name));check(EM_P);
 if constexpr(std::is_same_v<EM_T,bool>){return CastFieldChecked<FBoolProperty>(EM_P)->GetPropertyValue_InContainer(EM_Object);}
 else if constexpr(std::is_arithmetic_v<EM_T>){auto* EM_N=CastFieldChecked<FNumericProperty>(EM_P);const void* EM_Data=EM_P->ContainerPtrToValuePtr<void>(EM_Object);return EM_N->IsFloatingPoint()?EM_T(EM_N->GetFloatingPointPropertyValue(EM_Data)):EM_T(EM_N->GetSignedIntPropertyValue(EM_Data));}
 else if constexpr(std::is_same_v<EM_T,AActor*>){return Cast<AActor>(CastFieldChecked<FObjectPropertyBase>(EM_P)->GetObjectPropertyValue_InContainer(EM_Object));}
 else{EM_P->CopyCompleteValue(&EM_Value,EM_P->ContainerPtrToValuePtr<void>(EM_Object));return EM_Value;}
}
template<typename EM_T>static void EM_ComponentWrite(AActor* EM_Object,const TCHAR* EM_Name,EM_T EM_Value){
 FProperty* EM_P=EM_Object->GetClass()->FindPropertyByName(FName(EM_Name));check(EM_P);
 if constexpr(std::is_same_v<EM_T,bool>){CastFieldChecked<FBoolProperty>(EM_P)->SetPropertyValue_InContainer(EM_Object,EM_Value);}
 else if constexpr(std::is_arithmetic_v<EM_T>){auto* EM_N=CastFieldChecked<FNumericProperty>(EM_P);void* EM_Data=EM_P->ContainerPtrToValuePtr<void>(EM_Object);if(EM_N->IsFloatingPoint())EM_N->SetFloatingPointPropertyValue(EM_Data,double(EM_Value));else EM_N->SetIntPropertyValue(EM_Data,uint64(EM_Value));}
 else if constexpr(std::is_same_v<EM_T,AActor*>){CastFieldChecked<FObjectPropertyBase>(EM_P)->SetObjectPropertyValue_InContainer(EM_Object,EM_Value);}
 else EM_P->CopyCompleteValue(EM_P->ContainerPtrToValuePtr<void>(EM_Object),&EM_Value);
}
template<typename EM_T>static void EM_CallWrite(FProperty* EM_P,uint8* EM_Memory,EM_T EM_Value){
 void* EM_Data=EM_P->ContainerPtrToValuePtr<void>(EM_Memory);
 if constexpr(std::is_same_v<EM_T,bool>)CastFieldChecked<FBoolProperty>(EM_P)->SetPropertyValue(EM_Data,EM_Value);
 else if constexpr(std::is_arithmetic_v<EM_T>){auto* EM_N=CastFieldChecked<FNumericProperty>(EM_P);if(EM_N->IsFloatingPoint())EM_N->SetFloatingPointPropertyValue(EM_Data,double(EM_Value));else EM_N->SetIntPropertyValue(EM_Data,uint64(EM_Value));}
 else CastFieldChecked<FObjectPropertyBase>(EM_P)->SetObjectPropertyValue(EM_Data,EM_Value);
}
template<typename EM_Return,typename... EM_Args>static EM_Return EM_ComponentCall(AActor* EM_Object,const TCHAR* EM_Name,EM_Args... EM_Values){
 UFunction* EM_Function=EM_Object->FindFunction(FName(EM_Name));check(EM_Function);FStructOnScope EM_Scope(EM_Function);uint8* EM_Memory=EM_Scope.GetStructMemory();TArray<FProperty*> EM_Parameters;FProperty* EM_Result=nullptr;
 for(TFieldIterator<FProperty> EM_P(EM_Function);EM_P;++EM_P)if(EM_P->HasAnyPropertyFlags(CPF_ReturnParm))EM_Result=*EM_P;else if(EM_P->HasAnyPropertyFlags(CPF_Parm))EM_Parameters.Add(*EM_P);
 check(EM_Parameters.Num()==sizeof...(EM_Args));int32 EM_Index=0;(EM_CallWrite(EM_Parameters[EM_Index++],EM_Memory,EM_Values),...);EM_Object->ProcessEvent(EM_Function,EM_Memory);
 if constexpr(!std::is_void_v<EM_Return>){check(EM_Result);const void* EM_Data=EM_Result->ContainerPtrToValuePtr<void>(EM_Memory);
  if constexpr(std::is_same_v<EM_Return,bool>)return CastFieldChecked<FBoolProperty>(EM_Result)->GetPropertyValue(EM_Data);
  else if constexpr(std::is_arithmetic_v<EM_Return>){auto* EM_N=CastFieldChecked<FNumericProperty>(EM_Result);return EM_N->IsFloatingPoint()?EM_Return(EM_N->GetFloatingPointPropertyValue(EM_Data)):EM_Return(EM_N->GetSignedIntPropertyValue(EM_Data));}
  else return Cast<AActor>(CastFieldChecked<FObjectPropertyBase>(EM_Result)->GetObjectPropertyValue(EM_Data));
 }
}

""";
}
