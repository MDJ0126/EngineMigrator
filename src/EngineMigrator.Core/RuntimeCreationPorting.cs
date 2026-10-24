namespace EngineMigrator.Core;
internal static class RuntimeCreationPorting
{
    internal const string Helpers="""
protected:
static void EM_SetObjectName(AActor* EM_Object,const TCHAR* EM_Name){
 for(int32 EM_i=EM_Object->Tags.Num()-1;EM_i>=0;--EM_i)if(EM_Object->Tags[EM_i].ToString().StartsWith(TEXT("EMRuntimeName:")))EM_Object->Tags.RemoveAt(EM_i);
 const FString EM_Text=EM_Name;FString EM_Hex;int32 EM_Chunk=0;
 for(int32 EM_i=0;EM_i<EM_Text.Len();++EM_i){EM_Hex+=FString::Printf(TEXT("%08X"),uint32(EM_Text[EM_i]));if(EM_Hex.Len()==800){EM_Object->Tags.Add(FName(*(FString::Printf(TEXT("EMRuntimeName:%d:"),EM_Chunk++)+EM_Hex)));EM_Hex.Empty();}}
 if(!EM_Hex.IsEmpty()||EM_Chunk==0)EM_Object->Tags.Add(FName(*(FString::Printf(TEXT("EMRuntimeName:%d:"),EM_Chunk)+EM_Hex)));
}
static FString EM_GetObjectName(AActor* EM_Object){
 FString EM_Name;for(int32 EM_Chunk=0;;++EM_Chunk){const FString EM_Prefix=FString::Printf(TEXT("EMRuntimeName:%d:"),EM_Chunk);FString EM_Hex;bool EM_Found=false;
  for(const FName& EM_Tag:EM_Object->Tags){const FString EM_Value=EM_Tag.ToString();if(EM_Value.StartsWith(EM_Prefix)){EM_Hex=EM_Value.RightChop(EM_Prefix.Len());EM_Found=true;break;}}
  if(!EM_Found)break;for(int32 EM_i=0;EM_i+8<=EM_Hex.Len();EM_i+=8){const FString EM_Unit=EM_Hex.Mid(EM_i,8);EM_Name.AppendChar(TCHAR(FCString::Strtoui64(*EM_Unit,nullptr,16)));}
 }return EM_Name;
}
AActor* EM_NewGameObject(const TCHAR* EM_Name){
 FActorSpawnParameters EM_Params;EM_Params.bDeferConstruction=true;EM_Params.SpawnCollisionHandlingOverride=ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
 AActor* EM_New=GetWorld()->SpawnActor<AActor>(AActor::StaticClass(),FTransform::Identity,EM_Params);
 if(!EM_New)return nullptr;
 USceneComponent* EM_Root=NewObject<USceneComponent>(EM_New,TEXT("EM_Root"));EM_New->SetRootComponent(EM_Root);EM_New->AddInstanceComponent(EM_Root);EM_Root->RegisterComponent();
 EM_New->Tags.Add(TEXT("node_runtime_created"));EM_New->Tags.Add(TEXT("EMRuntimeCreated"));EM_SetObjectName(EM_New,EM_Name);EM_New->FinishSpawning(FTransform::Identity,true);return EM_New;
}
static FVector3f EM_ReadPosition(AActor* EM_Object){const FVector EM_V=EM_Object->GetActorLocation();return FVector3f(EM_V.Y/100,EM_V.Z/100,EM_V.X/100);}
static FVector3f EM_ReadLocalPosition(AActor* EM_Object){const FVector EM_V=EM_Object->GetRootComponent()->GetRelativeLocation();return FVector3f(EM_V.Y/100,EM_V.Z/100,EM_V.X/100);}
static FVector3f EM_ReadLocalScale(AActor* EM_Object){const FVector EM_V=EM_Object->GetRootComponent()->GetRelativeScale3D();return FVector3f(EM_V.Y,EM_V.Z,EM_V.X);}
static FVector4f EM_ReadRotation(AActor* EM_Object){const FQuat EM_Q=EM_Object->GetActorQuat();return FVector4f(EM_Q.Y,EM_Q.Z,EM_Q.X,EM_Q.W);}
static FVector4f EM_ReadLocalRotation(AActor* EM_Object){const FQuat EM_Q=EM_Object->GetRootComponent()->GetRelativeRotation().Quaternion();return FVector4f(EM_Q.Y,EM_Q.Z,EM_Q.X,EM_Q.W);}
static AActor* EM_ReadParent(AActor* EM_Object){return EM_Object->GetAttachParentActor();}
static void EM_WritePosition(AActor* EM_Object,FVector3f EM_V){EM_Object->SetActorLocation(FVector(EM_V.Z*100,EM_V.X*100,EM_V.Y*100));}
static void EM_WriteLocalPosition(AActor* EM_Object,FVector3f EM_V){EM_Object->SetActorRelativeLocation(FVector(EM_V.Z*100,EM_V.X*100,EM_V.Y*100));}
static void EM_WriteLocalScale(AActor* EM_Object,FVector3f EM_V){EM_Object->SetActorRelativeScale3D(FVector(EM_V.Z,EM_V.X,EM_V.Y));}
static void EM_WriteRotation(AActor* EM_Object,FVector4f EM_Q){EM_Object->SetActorRotation(FQuat(EM_Q.Z,EM_Q.X,EM_Q.Y,EM_Q.W));}
static void EM_WriteLocalRotation(AActor* EM_Object,FVector4f EM_Q){EM_Object->SetActorRelativeRotation(FQuat(EM_Q.Z,EM_Q.X,EM_Q.Y,EM_Q.W));}
static void EM_SetParent(AActor* EM_Object,AActor* EM_Parent,bool EM_World){
 if(EM_Parent)EM_Object->AttachToActor(EM_Parent,EM_World?FAttachmentTransformRules::KeepWorldTransform:FAttachmentTransformRules::KeepRelativeTransform);
 else EM_Object->DetachFromActor(EM_World?FDetachmentTransformRules::KeepWorldTransform:FDetachmentTransformRules::KeepRelativeTransform);
}

""";
}
