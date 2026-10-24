param([string]$Project='artifacts/project-scope-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
@'
using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static partial class EngineMigratorBridge {
 public static void VerifyProjectPreflight(){
  const string scenePath="Assets/ProjectScopeFixture/A/World.unity",prefabPath="Assets/ProjectScopeFixture/SharedActor.prefab";var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../project-preflight-tests"));Directory.CreateDirectory(output);var scene=File.ReadAllBytes(scenePath);var prefab=File.ReadAllBytes(prefabPath);
  try{
   EditorSceneManager.OpenScene(scenePath);new GameObject("Unsupported Sound").AddComponent<AudioSource>();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());PackageRoot=output+"/unsupported";
   bool rejected=false;try{ExportProject();}catch(Exception){var data=JsonUtility.FromJson<EMProjectPackage>(File.ReadAllText(PackageRoot+"/preflight.json"));rejected=data.blockers.Any(b=>b.Contains("AudioSource")&&b.Contains("World.unity"));}if(!rejected)throw new Exception("Unsupported component was not located and rejected");
  }finally{File.WriteAllBytes(scenePath,scene);AssetDatabase.ImportAsset(scenePath,ImportAssetOptions.ForceUpdate);}
  try{
   var text=File.ReadAllText(prefabPath);var broken=new Regex(@"^([ \t]*Target:)\s*\{fileID:.*\}",RegexOptions.Multiline).Replace(text,"$1 {fileID: 1234567, guid: fedcba9876543210fedcba9876543210, type: 3}",1);if(text==broken)throw new Exception("Missing GUID fixture did not modify an actual reference");File.WriteAllText(prefabPath,broken);AssetDatabase.ImportAsset(prefabPath,ImportAssetOptions.ForceUpdate);PackageRoot=output+"/missing-guid";
   bool rejected=false;try{ExportProject();}catch(Exception){var data=JsonUtility.FromJson<EMProjectPackage>(File.ReadAllText(PackageRoot+"/preflight.json"));rejected=data.blockers.Any(b=>b.Contains("fedcba9876543210fedcba9876543210")&&b.Contains("SharedActor.prefab:"));}if(!rejected)throw new Exception("Actual missing GUID was not located and rejected");
  }finally{File.WriteAllBytes(prefabPath,prefab);AssetDatabase.ImportAsset(prefabPath,ImportAssetOptions.ForceUpdate);}
  if(!scene.SequenceEqual(File.ReadAllBytes(scenePath))||!prefab.SequenceEqual(File.ReadAllBytes(prefabPath)))throw new Exception("Negative fixtures were not restored");File.WriteAllText(output+"/verification.json","{\"status\":\"PASS\",\"actualUnityMissingGuidRejected\":true,\"unsupportedComponentRejected\":true,\"fileLineOrObjectLocated\":true,\"fixturesRestored\":true,\"targetEngineNeverExecuted\":true}");Debug.Log("PROJECT_PREFLIGHT_NEGATIVE_PASS");
 }
}
'@|Set-Content ($Project+'/Assets/Editor/EMProjectPreflightTest.cs')
$start=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($argument in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EngineMigratorBridge.VerifyProjectPreflight','-logFile',($Project+'/preflight-negative.log'))){$start.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0){throw 'Actual Unity project preflight negative tests failed'};Write-Output 'PROJECT_PREFLIGHT_NEGATIVE_PASS'
