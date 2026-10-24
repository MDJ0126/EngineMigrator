param([Parameter(Mandatory=$true)][string]$Project,[string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent));$Project=[IO.Path]::GetFullPath($Project)
if(-not $Project.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture preparation is limited to private artifacts project copies.'}
Copy-Item -LiteralPath "$workspace/artifacts/int-actor-check/original.cs" -Destination "$Project/Assets/SceneBehaviourFixture/IntActor.cs" -Force
Copy-Item "$workspace/adapters/unity/*.cs" "$Project/Assets/Editor" -Force
@'
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class IntegerBehaviourFixture {
 public static void Run(){var scene=EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);var instance=new GameObject("Integer State").AddComponent<IntActor>();instance.Product=150000001;EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("ENGINE_MIGRATOR_INTEGER_FIXTURE_READY");}
}
'@ | Set-Content -LiteralPath "$Project/Assets/Editor/IntegerBehaviourFixture.cs"
$start=[Diagnostics.ProcessStartInfo]::new($UnityEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','IntegerBehaviourFixture.Run','-logFile',"$Project/int-fixture-prepare.log")){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode -ne 0){throw "Integer fixture failed: $Project/int-fixture-prepare.log"};Write-Output "INTEGER_FIXTURE_READY: $Project"
