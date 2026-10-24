param([Parameter(Mandatory=$true)][string]$Project,[string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent));$Project=[IO.Path]::GetFullPath($Project)
if(-not $Project.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture preparation is limited to private artifacts project copies.'}
New-Item -ItemType Directory -Path "$Project/Assets/SceneBehaviourFixture","$Project/Assets/Editor" -Force | Out-Null
foreach($name in @('PortBase','PortDerived')){Copy-Item -LiteralPath "$workspace/artifacts/actor-check/Original/$name.cs" -Destination "$Project/Assets/SceneBehaviourFixture/$name.cs"}
Copy-Item "$workspace/adapters/unity/*.cs" "$Project/Assets/Editor" -Force
@'
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class SceneBehaviourFixture {
 public static void Run(){
  var scene=EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
  var root=new GameObject("Behaviour Owner");root.transform.position=new Vector3(2,1,3);
  var first=root.AddComponent<PortDerived>();first.Health=123.0;first.Bonus=7.0;
  var child=new GameObject("Behaviour Child");child.transform.SetParent(root.transform,false);child.transform.localPosition=new Vector3(1,2,0);
  var second=child.AddComponent<PortDerived>();second.Health=57.0;second.Bonus=2.0;
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("ENGINE_MIGRATOR_SCENE_BEHAVIOUR_FIXTURE_READY");
 }
}
'@ | Set-Content -LiteralPath "$Project/Assets/Editor/SceneBehaviourFixture.cs"
$start=[Diagnostics.ProcessStartInfo]::new($UnityEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','SceneBehaviourFixture.Run','-logFile',"$Project/fixture-prepare.log")){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode -ne 0){throw "Unity scene fixture preparation failed: $Project/fixture-prepare.log"}
Write-Output "SCENE_BEHAVIOUR_FIXTURE_READY: $Project"
