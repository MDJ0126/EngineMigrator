param([Parameter(Mandatory=$true)][string]$ReverseRun)
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$ReverseRun=[IO.Path]::GetFullPath($ReverseRun);$Project=Join-Path $ReverseRun 'Target';& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
@'
using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMVerifyProjectStructure {
 static Component Behaviour(GameObject go)=>go.GetComponent("ProjectReference");
 static double Counter(GameObject go)=>Convert.ToDouble(Behaviour(go).GetType().GetField("Counter").GetValue(Behaviour(go)));
 static GameObject Target(GameObject go)=>(GameObject)Behaviour(go).GetType().GetField("Target").GetValue(Behaviour(go));
 public static void Run(){
  var project=JsonUtility.FromJson<EMProjectPackage>(File.ReadAllText(Path.Combine(Application.dataPath,"../.EngineMigratorProject.json")));var scenes=project.units.Where(u=>u.kind=="scene").ToArray();var prefab=project.units.Single(u=>u.kind=="prefab");if(scenes.Length!=3||EditorBuildSettings.scenes.Length!=3||EditorBuildSettings.scenes.Count(s=>s.enabled)!=2)throw new Exception("Scene count/enabled build flags differ");
  var meshes=new HashSet<string>();var materials=new HashSet<string>();var textures=new HashSet<string>();int instances=0,references=0;
  foreach(var unit in scenes){EditorSceneManager.OpenScene(unit.actualPath);if(unit.name!="World")continue;var a=GameObject.Find("Instance One");var b=GameObject.Find("Instance Two");foreach(var go in new[]{a,b}){if(!PrefabUtility.IsPartOfPrefabInstance(go)||PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)!=prefab.actualPath||go.transform.childCount!=1)throw new Exception("Actual prefab connection/topology differs");instances++;var visual=go.transform.GetChild(0).gameObject;meshes.Add(AssetDatabase.GetAssetPath(visual.GetComponent<MeshFilter>().sharedMesh));var material=visual.GetComponent<MeshRenderer>().sharedMaterial;materials.Add(AssetDatabase.GetAssetPath(material));textures.Add(AssetDatabase.GetAssetPath(material.mainTexture));}if(Target(a)!=a.transform.GetChild(0).gameObject||Target(b)!=a.transform.GetChild(0).gameObject)throw new Exception("Internal/cross-instance GameObject references differ");references++;if(Math.Abs(Counter(a)-5)>1e-9||Math.Abs(Counter(b)-(unit.path.Contains("/A/")?17:29))>1e-9)throw new Exception("Prefab property overrides differ");}
  if(instances!=4||references!=2||meshes.Count!=1||materials.Count!=1||textures.Count!=1)throw new Exception("Shared resources were duplicated or merged incorrectly");
  var baseline=File.ReadAllBytes(prefab.actualPath);try{var root=PrefabUtility.LoadPrefabContents(prefab.actualPath);Behaviour(root).GetType().GetField("Counter").SetValue(Behaviour(root),9d);PrefabUtility.SaveAsPrefabAsset(root,prefab.actualPath);PrefabUtility.UnloadPrefabContents(root);AssetDatabase.ImportAsset(prefab.actualPath,ImportAssetOptions.ForceUpdate);var unit=scenes.First(u=>u.name=="World");EditorSceneManager.OpenScene(unit.actualPath);if(Counter(GameObject.Find("Instance One"))!=9||Counter(GameObject.Find("Instance Two"))!=(unit.path.Contains("/A/")?17:29))throw new Exception("Template edit propagation or instance override preservation failed");}finally{File.WriteAllBytes(prefab.actualPath,baseline);AssetDatabase.ImportAsset(prefab.actualPath,ImportAssetOptions.ForceUpdate);}
  File.WriteAllText(Path.Combine(Application.dataPath,"../../unity-project-structure-verification.json"),"{\"status\":\"PASS\",\"scenes\":3,\"enabledScenes\":2,\"prefabAssets\":1,\"prefabInstances\":4,\"crossInstanceReferences\":2,\"sharedMeshes\":1,\"sharedMaterials\":1,\"sharedTextures\":1,\"templateEditPropagation\":true,\"propertyOverridesPreserved\":true}");Debug.Log("ACTUAL_UNITY_PROJECT_STRUCTURE_PASS");
 }
}
'@|Set-Content ($Project+'/Assets/Editor/EMVerifyProjectStructure.cs')
$start=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMVerifyProjectStructure.Run','-logFile',($ReverseRun+'/structure-verification.log'))){$start.ArgumentList.Add($a)}
$p=[Diagnostics.Process]::Start($start);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Actual Unity project structure verification failed'};Write-Output 'ACTUAL_UNITY_PROJECT_STRUCTURE_PASS'
