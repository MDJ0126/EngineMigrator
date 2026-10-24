#include "EMBlueprintLibrary.h"
#include "GameFramework/Actor.h"
#include "Components/BoxComponent.h"
#include "Components/SphereComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "PhysicalMaterials/PhysicalMaterial.h"
#include "Serialization/JsonSerializer.h"
#include "Misc/PackageName.h"
#include "UObject/SavePackage.h"

namespace EMP {
using J=TSharedPtr<FJsonObject>;
FString Json(const J& O){FString S;FJsonSerializer::Serialize(O.ToSharedRef(),TJsonWriterFactory<>::Create(&S));return S;}
FVector Vector(const J& O){return FVector(O->GetNumberField(TEXT("z")),O->GetNumberField(TEXT("x")),O->GetNumberField(TEXT("y")))*100.;}
J Vec(FVector V){auto O=MakeShared<FJsonObject>();O->SetNumberField(TEXT("x"),V.Y/100.);O->SetNumberField(TEXT("y"),V.Z/100.);O->SetNumberField(TEXT("z"),V.X/100.);return O;}
UShapeComponent* Shape(AActor* A){if(!A)return nullptr;for(auto* C:A->GetComponents())if(C->GetFName()==TEXT("EM_Physics"))return Cast<UShapeComponent>(C);return nullptr;}
}
FString UEMBlueprintLibrary::ApplyPhysics(AActor* Actor,const FString& Json){
 using namespace EMP;J V;if(!Actor||!FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Json),V))return TEXT("ERROR: invalid physics input");
 if(Shape(Actor))return TEXT("ERROR: duplicate physics component");
 FString Kind=V->GetStringField(TEXT("shape"));UShapeComponent* C=nullptr;
 if(Kind==TEXT("box")){auto* Box=NewObject<UBoxComponent>(Actor,TEXT("EM_Physics"),RF_Transactional);Box->SetBoxExtent(Vector(V->GetObjectField(TEXT("size")))*0.5);C=Box;}
 else if(Kind==TEXT("sphere")){auto* Sphere=NewObject<USphereComponent>(Actor,TEXT("EM_Physics"),RF_Transactional);Sphere->SetSphereRadius(V->GetNumberField(TEXT("radius"))*100.);C=Sphere;}
 else if(Kind==TEXT("capsule")){auto* Capsule=NewObject<UCapsuleComponent>(Actor,TEXT("EM_Physics"),RF_Transactional);Capsule->SetCapsuleSize(V->GetNumberField(TEXT("radius"))*100.,V->GetNumberField(TEXT("height"))*50.);C=Capsule;}
 if(!C)return TEXT("ERROR: unsupported physics shape");
 const bool HasBody=V->GetBoolField(TEXT("hasBody")),Kinematic=V->GetBoolField(TEXT("kinematic"));
 const bool Dynamic=HasBody&&!Kinematic;auto* OldRoot=Actor->GetRootComponent();
 if(!OldRoot){OldRoot=NewObject<USceneComponent>(Actor,TEXT("EM_Root"),RF_Transactional);Actor->AddInstanceComponent(OldRoot);Actor->SetRootComponent(OldRoot);OldRoot->SetMobility(EComponentMobility::Movable);OldRoot->RegisterComponent();}
 if(!Actor->FindComponentByClass<UStaticMeshComponent>()){auto* Mesh=NewObject<UStaticMeshComponent>(Actor,TEXT("EM_Mesh"),RF_Transactional);Actor->AddInstanceComponent(Mesh);Mesh->SetupAttachment(OldRoot);Mesh->SetMobility(EComponentMobility::Movable);Mesh->SetCollisionProfileName(TEXT("NoCollision"));Mesh->RegisterComponent();}
 Actor->AddInstanceComponent(C);C->SetMobility(EComponentMobility::Movable);
 if(Dynamic){FTransform T=Actor->GetActorTransform();OldRoot->DetachFromComponent(FDetachmentTransformRules::KeepWorldTransform);Actor->SetRootComponent(C);C->SetWorldTransform(T);OldRoot->AttachToComponent(C,FAttachmentTransformRules::KeepWorldTransform);}
 else {C->SetupAttachment(OldRoot);C->SetRelativeLocation(Vector(V->GetObjectField(TEXT("center"))));}
 const int32 Direction=V->GetIntegerField(TEXT("direction"));
 if(Kind==TEXT("capsule")&&Direction!=1){if(Dynamic)return TEXT("ERROR: dynamic capsule X/Z axes unsupported");C->SetRelativeRotation(Direction==0?FRotator(0,0,90):FRotator(90,0,0));}
 const bool Enabled=V->GetBoolField(TEXT("enabled")),Trigger=V->GetBoolField(TEXT("trigger"));
 C->SetCollisionProfileName(Trigger?TEXT("OverlapAllDynamic"):TEXT("BlockAllDynamic"));C->SetCollisionEnabled(!Enabled?ECollisionEnabled::NoCollision:Trigger?ECollisionEnabled::QueryOnly:ECollisionEnabled::QueryAndPhysics);C->SetGenerateOverlapEvents(Trigger);
 double Friction=.6,Static=.6,Bounce=0;V->TryGetNumberField(TEXT("dynamicFriction"),Friction);V->TryGetNumberField(TEXT("staticFriction"),Static);V->TryGetNumberField(TEXT("bounciness"),Bounce);FString MaterialPath;V->TryGetStringField(TEXT("materialAsset"),MaterialPath);UPhysicalMaterial* Material=nullptr;
 if(!MaterialPath.IsEmpty()){if(!MaterialPath.StartsWith(TEXT("/Game/"))||MaterialPath.Contains(TEXT("..")))return TEXT("ERROR: unsafe physical material path");Material=LoadObject<UPhysicalMaterial>(nullptr,*(MaterialPath+TEXT(".")+FPackageName::GetLongPackageAssetName(MaterialPath)));if(Material&&(FMath::Abs(Material->Friction-Friction)>1e-5||FMath::Abs(Material->StaticFriction-Static)>1e-5||FMath::Abs(Material->Restitution-Bounce)>1e-5))return TEXT("ERROR: conflicting shared physics material");if(!Material)Material=NewObject<UPhysicalMaterial>(CreatePackage(*MaterialPath),*FPackageName::GetLongPackageAssetName(MaterialPath),RF_Public|RF_Standalone);}
 else Material=NewObject<UPhysicalMaterial>(Actor,TEXT("EM_UnityDefaultMaterial"),RF_Transactional);
 Material->Friction=Friction;Material->StaticFriction=Static;Material->Restitution=Bounce;Material->bOverrideFrictionCombineMode=true;Material->bOverrideRestitutionCombineMode=true;Material->FrictionCombineMode=EFrictionCombineMode::Average;Material->RestitutionCombineMode=EFrictionCombineMode::Average;
 if(!MaterialPath.IsEmpty()){FSavePackageArgs Args;Args.TopLevelFlags=RF_Public|RF_Standalone;if(!UPackage::SavePackage(Material->GetOutermost(),Material,*FPackageName::LongPackageNameToFilename(MaterialPath,FPackageName::GetAssetPackageExtension()),Args))return TEXT("ERROR: physical material save failed");}C->SetPhysMaterialOverride(Material);
 C->BodyInstance.bOverrideMass=true;C->BodyInstance.SetMassOverride(V->GetNumberField(TEXT("mass")),true);
 C->SetEnableGravity(V->GetBoolField(TEXT("gravity")));C->SetLinearDamping(V->GetNumberField(TEXT("linearDamping")));C->SetAngularDamping(V->GetNumberField(TEXT("angularDamping")));
 const int32 Locks=V->GetIntegerField(TEXT("constraints"));auto& B=C->BodyInstance;
 B.bLockXTranslation=(Locks&8)!=0;B.bLockYTranslation=(Locks&2)!=0;B.bLockZTranslation=(Locks&4)!=0;
 B.bLockXRotation=(Locks&64)!=0;B.bLockYRotation=(Locks&16)!=0;B.bLockZRotation=(Locks&32)!=0;B.DOFMode=EDOFMode::SixDOF;
 B.bUseCCD=V->GetStringField(TEXT("detection"))==TEXT("ContinuousDynamic");C->RegisterComponent();C->SetSimulatePhysics(Dynamic);Actor->Modify();return TEXT("PHYSICS_APPLIED");
}
FString UEMBlueprintLibrary::ReadPhysics(AActor* Actor){
 using namespace EMP;auto O=MakeShared<FJsonObject>();auto* C=Shape(Actor);if(!C)return TEXT("{}");
 O->SetBoolField(TEXT("enabled"),C->GetCollisionEnabled()!=ECollisionEnabled::NoCollision);O->SetBoolField(TEXT("trigger"),C->GetGenerateOverlapEvents());
 O->SetObjectField(TEXT("center"),Vec(C==Actor->GetRootComponent()?FVector::ZeroVector:C->GetRelativeLocation()));
 auto Zero=MakeShared<FJsonObject>();Zero->SetNumberField(TEXT("x"),0);Zero->SetNumberField(TEXT("y"),0);Zero->SetNumberField(TEXT("z"),0);O->SetObjectField(TEXT("size"),Zero);O->SetNumberField(TEXT("radius"),0);O->SetNumberField(TEXT("height"),0);O->SetNumberField(TEXT("direction"),1);
 if(auto* Box=Cast<UBoxComponent>(C)){O->SetStringField(TEXT("shape"),TEXT("box"));O->SetObjectField(TEXT("size"),Vec(Box->GetUnscaledBoxExtent()*2));}
 if(auto* Sphere=Cast<USphereComponent>(C)){O->SetStringField(TEXT("shape"),TEXT("sphere"));O->SetNumberField(TEXT("radius"),Sphere->GetUnscaledSphereRadius()/100.);}
 if(auto* Capsule=Cast<UCapsuleComponent>(C)){O->SetStringField(TEXT("shape"),TEXT("capsule"));O->SetNumberField(TEXT("radius"),Capsule->GetUnscaledCapsuleRadius()/100.);O->SetNumberField(TEXT("height"),Capsule->GetUnscaledCapsuleHalfHeight()/50.);auto Up=C->GetRelativeRotation().RotateVector(FVector::UpVector).GetAbs();O->SetNumberField(TEXT("direction"),Up.Z>.9?1:Up.Y>.9?0:2);}
 auto& B=C->BodyInstance;O->SetBoolField(TEXT("kinematic"),!B.bSimulatePhysics);O->SetBoolField(TEXT("gravity"),C->IsGravityEnabled());O->SetNumberField(TEXT("mass"),B.GetMassOverride());O->SetNumberField(TEXT("linearDamping"),C->GetLinearDamping());O->SetNumberField(TEXT("angularDamping"),C->GetAngularDamping());
 int32 Locks=(B.bLockXTranslation?8:0)|(B.bLockYTranslation?2:0)|(B.bLockZTranslation?4:0)|(B.bLockXRotation?64:0)|(B.bLockYRotation?16:0)|(B.bLockZRotation?32:0);O->SetNumberField(TEXT("constraints"),Locks);O->SetStringField(TEXT("detection"),B.bUseCCD?TEXT("ContinuousDynamic"):TEXT("Discrete"));if(auto* Material=B.GetSimplePhysicalMaterial()){O->SetNumberField(TEXT("dynamicFriction"),Material->Friction);O->SetNumberField(TEXT("staticFriction"),Material->StaticFriction);O->SetNumberField(TEXT("bounciness"),Material->Restitution);O->SetStringField(TEXT("nativeMaterial"),Material->GetPathName());auto Mode=[](int Value){return Value==EFrictionCombineMode::Min?TEXT("Minimum"):Value==EFrictionCombineMode::Multiply?TEXT("Multiply"):Value==EFrictionCombineMode::Max?TEXT("Maximum"):TEXT("Average");};O->SetStringField(TEXT("frictionCombine"),Mode(Material->FrictionCombineMode));O->SetStringField(TEXT("bounceCombine"),Mode(Material->RestitutionCombineMode));}return Json(O);
}
