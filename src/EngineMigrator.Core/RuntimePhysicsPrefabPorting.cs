namespace EngineMigrator.Core;
internal static class RuntimePhysicsPrefabPorting
{
    internal const string Includes="#include \"Components/ShapeComponent.h\"\n";
    internal static string RuntimeHelpers=>WholeObjectDestructionPorting.RuntimeHelpers.Replace(
        " for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning(EM_New->GetActorTransform(),true);",
        " for (AActor* EM_New:EM_Copies) EM_New->FinishSpawning(EM_New->GetActorTransform(),true);\n for(AActor* EM_New:EM_Copies)EM_RefreshRuntimePhysics(EM_New,EM_ObjectActiveInHierarchy(EM_New));")+PhysicsHelpers;
    internal static string Helpers(string name)=>WholeObjectDestructionPorting.Helpers(name).Replace(
        "EM_Actor->SetActorEnableCollision(EM_Active);",
        "EM_Actor->SetActorEnableCollision(EM_Active);EM_RefreshRuntimePhysics(EM_Actor,EM_Active);");
    private const string PhysicsHelpers="""
static void EM_RefreshRuntimePhysics(AActor* EM_Actor,bool EM_Active){
 if(!EM_Actor->Tags.Contains(TEXT("EMRuntimePhysicsDynamic")))return;
 TArray<UShapeComponent*> EM_Shapes;EM_Actor->GetComponents(EM_Shapes);
 for(UShapeComponent* EM_Shape:EM_Shapes)if(EM_Shape->GetFName()==TEXT("EM_Physics")){
  if(!EM_Active&&EM_Shape->IsSimulatingPhysics()){
   const FVector EM_Linear=EM_Shape->GetPhysicsLinearVelocity(),EM_Angular=EM_Shape->GetPhysicsAngularVelocityInRadians();
   EM_Actor->Tags.RemoveAll([](const FName& T){return T.ToString().StartsWith(TEXT("EMPhysicsLinear:"))||T.ToString().StartsWith(TEXT("EMPhysicsAngular:"));});
   EM_Actor->Tags.Add(FName(*(FString(TEXT("EMPhysicsLinear:"))+EM_Linear.ToString())));EM_Actor->Tags.Add(FName(*(FString(TEXT("EMPhysicsAngular:"))+EM_Angular.ToString())));EM_Shape->SetSimulatePhysics(false);
  }else if(EM_Active&&!EM_Shape->IsSimulatingPhysics()){
   EM_Shape->SetSimulatePhysics(true);
   for(const FName& T:EM_Actor->Tags){const FString EM_Tag=T.ToString();FVector EM_Velocity;
    if(EM_Tag.StartsWith(TEXT("EMPhysicsLinear:"))&&EM_Velocity.InitFromString(EM_Tag.RightChop(16)))EM_Shape->SetPhysicsLinearVelocity(EM_Velocity);
    if(EM_Tag.StartsWith(TEXT("EMPhysicsAngular:"))&&EM_Velocity.InitFromString(EM_Tag.RightChop(17)))EM_Shape->SetPhysicsAngularVelocityInRadians(EM_Velocity);
   }
   EM_Actor->Tags.RemoveAll([](const FName& T){return T.ToString().StartsWith(TEXT("EMPhysicsLinear:"))||T.ToString().StartsWith(TEXT("EMPhysicsAngular:"));});
  }
 }
}

""";
}
