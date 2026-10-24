#pragma once
#include "Kismet/BlueprintFunctionLibrary.h"
#include "EMBlueprintLibrary.generated.h"
class UMaterial;
class AActor;
class ULevelSequence;
class UWorld;
class UStaticMesh;
UCLASS()
class EMBLUEPRINTBRIDGE_API UEMBlueprintLibrary : public UBlueprintFunctionLibrary {
 GENERATED_BODY()
public:
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString Run(const FString& InputFile,const FString& OutputDirectory);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString RunActors(const FString& InputFile,const FString& OutputDirectory);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString RunScene(const FString& InputFile,const FString& OutputDirectory);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static bool HasWorldPositionOffset(UMaterial* Material);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString ApplyPhysics(AActor* Actor,const FString& Json);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString ReadPhysics(AActor* Actor);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString ActorGraph(const FString& Json);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString MapUI(const FString& Json);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static bool BindAnimation(ULevelSequence* Sequence,FGuid BindingId,AActor* Actor);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString CreateAnimationPresenter(const FString& AssetPath);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString RuntimePolicy(UWorld* World,const FString& Json);
 UFUNCTION(BlueprintCallable,Category="EngineMigrator")
 static FString ReadMeshSource(UStaticMesh* Mesh);
};
