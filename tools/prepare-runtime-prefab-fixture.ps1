param([string]$Project='artifacts/runtime-prefab-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
@'
using System;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareRuntimePrefab {
 public static void Run(){const string folder="Assets/RuntimeWorld/";if(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"RuntimeSpawnable.prefab"))throw new Exception("Fixture already exists");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Runtime Template");root.transform.position=new Vector3(2,3,4);var child=new GameObject("Template Child");child.transform.SetParent(root.transform,false);child.transform.localPosition=new Vector3(1,2,3);child.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectScopeFixture/Shared.asset");child.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectScopeFixture/Shared.mat");var counter=root.AddComponent<RuntimeTemplateCounter>();counter.Target=child;counter.External=null;counter.Awakes=0;var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"RuntimeSpawnable.prefab");UnityEngine.Object.DestroyImmediate(root);var driver=new GameObject("Runtime Driver").AddComponent<RuntimeSpawner>();driver.Source=prefab;EditorSceneManager.SaveScene(scene,folder+"RuntimeStart.unity");AssetDatabase.SaveAssets();Debug.Log("RUNTIME_PREFAB_FIXTURE_READY");}
}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareRuntimePrefab.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareRuntimePrefab.Run','-logFile',($Project+'/prepare-runtime-prefab.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Runtime prefab fixture failed'};Write-Output 'RUNTIME_PREFAB_FIXTURE_READY'
