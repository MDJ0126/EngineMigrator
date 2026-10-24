param([Parameter(Mandatory=$true)][string]$Project,[string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent));$Project=[IO.Path]::GetFullPath($Project)
if(-not $Project.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture preparation is limited to private artifacts project copies.'}
@'
using UnityEngine;
public class ReferenceActor : MonoBehaviour {
 public GameObject CameraTarget;
 public GameObject ChildTarget;
 public GameObject SelfTarget;
 public GameObject NullTarget;
 public bool HasCamera,HasChild,HasSelf,MissingIsNull;
 public double Count;
 protected virtual void Awake(){Count+=1.0;}
 protected virtual void Start(){Count+=10.0;}
 protected virtual void Update(){HasCamera=Check(CameraTarget);HasChild=ChildTarget!=null;HasSelf=SelfTarget!=null;MissingIsNull=NullTarget==null;Count+=1.0;}
 protected virtual void OnDestroy(){Count+=100.0;}
 public bool Check(GameObject value){return value!=null;}
}
'@|Set-Content -LiteralPath "$Project/Assets/SceneBehaviourFixture/ReferenceActor.cs"
Copy-Item "$workspace/adapters/unity/*.cs" "$Project/Assets/Editor" -Force
@'
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class ReferenceBehaviourFixture {
 public static void Run(){var scene=EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);var go=new GameObject("Reference State");var instance=go.AddComponent<ReferenceActor>();instance.CameraTarget=GameObject.Find("Main Camera");instance.ChildTarget=GameObject.Find("Behaviour Child");instance.SelfTarget=go;EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("ENGINE_MIGRATOR_REFERENCE_FIXTURE_READY");}
}
'@|Set-Content -LiteralPath "$Project/Assets/Editor/ReferenceBehaviourFixture.cs"
$start=[Diagnostics.ProcessStartInfo]::new($UnityEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','ReferenceBehaviourFixture.Run','-logFile',"$Project/reference-fixture-prepare.log")){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode -ne 0){throw "Reference fixture failed: $Project/reference-fixture-prepare.log"};Write-Output "REFERENCE_FIXTURE_READY: $Project"
