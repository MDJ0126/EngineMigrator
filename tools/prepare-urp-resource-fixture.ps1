param([Parameter(Mandatory=$true)][string]$Project,[string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent));$Project=[IO.Path]::GetFullPath($Project)
if(-not $Project.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture preparation is limited to private artifacts project copies.'}
Copy-Item "$workspace/adapters/unity/*.cs" "$Project/Assets/Editor" -Force
@'
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
public static class UrpResourceFixture {
 public static void Run(){
  var scene=EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);const string folder="Assets/UrpResourceFixture";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
  var mesh=new Mesh{name="SharedResourceMesh"};mesh.vertices=new[]{Vector3.zero,new Vector3(2,0,0),new Vector3(0,3,0)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up};mesh.triangles=new[]{0,1,2};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/SharedMesh.asset");
  var image=new Texture2D(4,4,TextureFormat.RGB24,false);var pixels=new Color[16];for(var i=0;i<16;i++)pixels[i]=i%2==0?new Color(.9f,.2f,.1f):new Color(.1f,.7f,.4f);image.SetPixels(pixels);image.Apply();
  var extensions=new[]{"png","jpg","tga"};for(var i=0;i<3;i++){
   var extension=extensions[i];var path=folder+"/Pattern."+extension;File.WriteAllBytes(path,extension=="png"?image.EncodeToPNG():extension=="jpg"?image.EncodeToJPG(90):image.EncodeToTGA());AssetDatabase.Refresh();
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.wrapModeU=TextureWrapMode.Clamp;importer.wrapModeV=TextureWrapMode.Mirror;importer.SaveAndReimport();
   var material=new Material(Shader.Find(i==2?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));material.SetColor("_BaseColor",new Color(.6f,.35f,.2f,1));if(i!=2){material.SetFloat("_Metallic",.35f);material.SetFloat("_Smoothness",.72f);}AssetDatabase.CreateAsset(material,folder+"/Material_"+extension+".mat");
   var go=new GameObject("URP "+extension);go.transform.position=new Vector3(i*3,1,0);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
  }
  UnityEngine.Object.DestroyImmediate(image);
  var volume=UnityEngine.Object.FindFirstObjectByType<Volume>();if(volume){var shared=new GameObject("Shared Global Volume").AddComponent<Volume>();shared.isGlobal=true;shared.priority=1;shared.sharedProfile=volume.sharedProfile;}
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("ENGINE_MIGRATOR_URP_RESOURCE_FIXTURE_READY");
 }
}
'@ | Set-Content -LiteralPath "$Project/Assets/Editor/UrpResourceFixture.cs"
$start=[Diagnostics.ProcessStartInfo]::new($UnityEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','UrpResourceFixture.Run','-logFile',"$Project/urp-fixture-prepare.log")){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode -ne 0){throw "URP fixture failed: $Project/urp-fixture-prepare.log"};Write-Output "URP_RESOURCE_FIXTURE_READY: $Project"
