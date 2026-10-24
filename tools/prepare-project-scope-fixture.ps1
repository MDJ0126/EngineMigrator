param([string]$Project='artifacts/project-scope-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
New-Item -ItemType Directory -Force ($Project+'/Assets/ProjectScopeFixture')|Out-Null
@'
using UnityEngine;
public class ProjectReference:MonoBehaviour {public GameObject Target;public double Counter=5;void Update(){Counter+=Time.deltaTime;}}
'@|Set-Content ($Project+'/Assets/ProjectScopeFixture/ProjectReference.cs')
@'
using System;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareProjectScope {
 public static void Run(){
  const string folder="Assets/ProjectScopeFixture";if(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/SharedActor.prefab"))throw new Exception("Project fixture already exists");
  Directory.CreateDirectory(folder+"/A");Directory.CreateDirectory(folder+"/B");AssetDatabase.Refresh();
  var image=new Texture2D(2,2,TextureFormat.RGBA32,false);image.SetPixels(new[]{Color.red,Color.green,Color.yellow,Color.white});image.Apply();File.WriteAllBytes(folder+"/Shared.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);AssetDatabase.Refresh();var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/Shared.png");importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
  var mesh=new Mesh{name="SharedTetra"};mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.forward};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};mesh.triangles=new[]{0,2,1,0,1,3,0,3,2,1,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,folder+"/Shared.asset");
  var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="SharedAmber"};material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Shared.png");AssetDatabase.CreateAsset(material,folder+"/Shared.mat");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("SharedActor");var child=new GameObject("Visual");child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;root.AddComponent<ProjectReference>().Target=child;PrefabUtility.SaveAsPrefabAsset(root,folder+"/SharedActor.prefab");UnityEngine.Object.DestroyImmediate(root);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/SharedActor.prefab");
  foreach(var variant in new[]{"A","B"}){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var first=(GameObject)PrefabUtility.InstantiatePrefab(asset);first.name="Instance One";first.transform.position=new Vector3(2,1,3);var second=(GameObject)PrefabUtility.InstantiatePrefab(asset);second.name="Instance Two";second.transform.position=new Vector3(8,2,4);second.GetComponent<ProjectReference>().Counter=variant=="A"?17:29;second.GetComponent<ProjectReference>().Target=first.transform.GetChild(0).gameObject;PrefabUtility.RecordPrefabInstancePropertyModifications(second.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(second.GetComponent<ProjectReference>());EditorSceneManager.SaveScene(scene,folder+"/"+variant+"/World.unity");}
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(folder+"/A/World.unity",true),new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",true),new EditorBuildSettingsScene(folder+"/B/World.unity",false)};AssetDatabase.SaveAssets();Debug.Log("PROJECT_SCOPE_FIXTURE_READY");
 }
}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareProjectScope.cs')
$start=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareProjectScope.Run','-logFile',($Project+'/prepare-project-scope.log'))){$start.ArgumentList.Add($a)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0){throw 'Project scope fixture preparation failed'};Write-Output 'PROJECT_SCOPE_FIXTURE_READY'
