// Editor adapter. Install into a working copy's Assets/Editor, not the original project.
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
public partial class EMNode {public bool hasRenderer,rendererEnabled=true,castShadows=true;}
public static partial class EngineMigratorBridge
{
    static EMMesh ReadMesh(Mesh mesh,string id)
    {
        var result=new EMMesh{id=id,name=mesh.name,file="meshes/"+id+".obj"};
        // Editor MeshUtility permits inspection of imported assets whose runtime
        // Read/Write option is off, without changing the source importer.
        using(var snapshots=MeshUtility.AcquireReadOnlyMeshData(mesh))
        {
            var data=snapshots[0];
            using(var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp)){data.GetVertices(vertices);result.vertices=vertices.ToArray();}
            result.normals=new Vector3[0];result.uv=new Vector2[0];
            if(data.HasVertexAttribute(VertexAttribute.Normal))using(var normals=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp)){data.GetNormals(normals);result.normals=normals.ToArray();}
            if(data.HasVertexAttribute(VertexAttribute.TexCoord0))using(var uv=new NativeArray<Vector2>(data.vertexCount,Allocator.Temp)){data.GetUVs(0,uv);result.uv=uv.ToArray();}
            using(var indices=new NativeArray<int>(data.GetSubMesh(0).indexCount,Allocator.Temp)){data.GetIndices(indices,0,true);result.triangles=indices.ToArray();}
        }
        return result;
    }
}
