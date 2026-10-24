#include "EMBlueprintLibrary.h"
#include "Engine/StaticMesh.h"
#include "MeshDescription.h"
#include "StaticMeshAttributes.h"
#include "Serialization/JsonSerializer.h"

FString UEMBlueprintLibrary::ReadMeshSource(UStaticMesh* Mesh){
 if(!Mesh||Mesh->GetNumLODs()!=1)return TEXT("ERROR: exactly one mesh LOD is required");
 const auto& Model=Mesh->GetSourceModel(0);
 if(Model.BuildSettings.bRecomputeNormals||Model.BuildSettings.BuildScale3D!=FVector(1))return TEXT("ERROR: recomputed normals or build scale require explicit mesh conversion");
 const FMeshDescription* Description=Mesh->GetMeshDescription(0);if(!Description)return TEXT("ERROR: editable source MeshDescription is unavailable");
 const FStaticMeshConstAttributes Attributes(*Description);const auto Positions=Attributes.GetVertexPositions();const auto Normals=Attributes.GetVertexInstanceNormals();const auto UVs=Attributes.GetVertexInstanceUVs();
 using V=TSharedPtr<FJsonValue>;TArray<V> Vertices,OutputNormals,OutputUVs,Triangles;TMap<FVertexInstanceID,int32> Indices;
 auto Vec=[](const FVector3f& P,double Divisor){auto O=MakeShared<FJsonObject>();O->SetNumberField(TEXT("x"),P.Y/Divisor);O->SetNumberField(TEXT("y"),P.Z/Divisor);O->SetNumberField(TEXT("z"),P.X/Divisor);return O;};
 for(const FTriangleID Triangle:Description->Triangles().GetElementIDs())for(const FVertexInstanceID Instance:Description->GetTriangleVertexInstances(Triangle)){
  int32* Existing=Indices.Find(Instance);int32 Index;
  if(Existing)Index=*Existing;else{Index=Vertices.Num();Indices.Add(Instance,Index);Vertices.Add(MakeShared<FJsonValueObject>(Vec(Positions[Description->GetVertexInstanceVertex(Instance)],100)));OutputNormals.Add(MakeShared<FJsonValueObject>(Vec(Normals[Instance],1)));if(UVs.GetNumChannels()>0){const auto UV=UVs.Get(Instance,0);auto O=MakeShared<FJsonObject>();O->SetNumberField(TEXT("x"),UV.X);O->SetNumberField(TEXT("y"),1-UV.Y);OutputUVs.Add(MakeShared<FJsonValueObject>(O));}}
  Triangles.Add(MakeShared<FJsonValueNumber>(Index));
 }
 auto Result=MakeShared<FJsonObject>();Result->SetArrayField(TEXT("vertices"),Vertices);Result->SetArrayField(TEXT("normals"),OutputNormals);Result->SetArrayField(TEXT("uv"),OutputUVs);Result->SetArrayField(TEXT("triangles"),Triangles);FString Json;FJsonSerializer::Serialize(Result,TJsonWriterFactory<>::Create(&Json));return Json;
}
