param([Parameter(Mandatory=$true)][string]$Project,[string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent));$Project=[IO.Path]::GetFullPath($Project)
if(-not $Project.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture preparation is limited to private artifacts project copies.'}
@'
using UnityEngine;
public class PrivateActor : MonoBehaviour {
 [SerializeField] private int health=7;
 [SerializeField] private GameObject camera;
 public int Visible;
 public bool HasCamera;
 protected virtual void Awake(){health++;}
 protected virtual void Start(){health+=3;}
 protected virtual void Update(){health++;Visible=health;HasCamera=camera!=null;}
 protected virtual void OnDestroy(){health--;Visible=health;}
}
'@|Set-Content -LiteralPath "$Project/Assets/SceneBehaviourFixture/PrivateActor.cs"
Copy-Item "$workspace/adapters/unity/*.cs" "$Project/Assets/Editor" -Force
@'
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PrivateBehaviourFixture {
 public static void Run(){var scene=EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);var instance=new GameObject("Private Serialized State").AddComponent<PrivateActor>();var flags=BindingFlags.NonPublic|BindingFlags.Instance;typeof(PrivateActor).GetField("health",flags).SetValue(instance,2147483646);typeof(PrivateActor).GetField("camera",flags).SetValue(instance,GameObject.Find("Main Camera"));EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("ENGINE_MIGRATOR_PRIVATE_FIXTURE_READY");}
}
'@|Set-Content -LiteralPath "$Project/Assets/Editor/PrivateBehaviourFixture.cs"
$start=[Diagnostics.ProcessStartInfo]::new($UnityEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','PrivateBehaviourFixture.Run','-logFile',"$Project/private-fixture-prepare.log")){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode -ne 0){throw "Private fixture failed: $Project/private-fixture-prepare.log"};Write-Output "PRIVATE_FIXTURE_READY: $Project"
