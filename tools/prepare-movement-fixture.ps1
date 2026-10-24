param([Parameter(Mandatory=$true)][string]$Project,[string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent));$Project=[IO.Path]::GetFullPath($Project)
if(-not $Project.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture preparation is limited to private artifacts project copies.'}
@'
using UnityEngine;
public class MoveActor : MonoBehaviour {
 public Vector3 Velocity=new Vector3(1.25f,0.25f,0.5f);
 public Vector3 Last;
 public float SampleX;
 public double TickCount;
 protected virtual void Awake(){Velocity=Velocity+Vector3.right;}
 protected virtual void Update(){transform.position+=Velocity*Time.deltaTime;Last=transform.position;SampleX=Last.x;TickCount+=1.0;}
}
'@|Set-Content -LiteralPath "$Project/Assets/SceneBehaviourFixture/MoveActor.cs"
@'
using UnityEngine;
public class LocalMoveActor : MoveActor {
 protected override void Update(){transform.localPosition+=Velocity*Time.deltaTime;Last=transform.localPosition;SampleX=Last.x;TickCount+=1.0;}
}
'@|Set-Content -LiteralPath "$Project/Assets/SceneBehaviourFixture/LocalMoveActor.cs"
Copy-Item "$workspace/adapters/unity/*.cs" "$Project/Assets/Editor" -Force
@'
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class MovementBehaviourFixture {
 public static void Run(){
  var scene=EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);var root=GameObject.CreatePrimitive(PrimitiveType.Cube);root.name="World Moving Cube";root.transform.position=new Vector3(-1,0,0);Object.DestroyImmediate(root.GetComponent<Collider>());
  var mesh=Object.Instantiate(root.GetComponent<MeshFilter>().sharedMesh);mesh.name="MovementCube";AssetDatabase.CreateAsset(mesh,"Assets/SceneBehaviourFixture/MovementCube.asset");root.GetComponent<MeshFilter>().sharedMesh=mesh;var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/UrpResourceFixture/Material_png.mat");root.GetComponent<MeshRenderer>().sharedMaterial=material;root.AddComponent<MoveActor>();
  var parent=new GameObject("Movement Parent");parent.transform.position=new Vector3(-3,1,2);parent.transform.rotation=Quaternion.Euler(0,30,0);parent.transform.localScale=new Vector3(1.25f,2,.75f);
  var child=new GameObject("Local Moving Cube");child.transform.SetParent(parent.transform,false);child.transform.localPosition=new Vector3(.5f,1,2);child.transform.localScale=Vector3.one*.5f;child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;var instance=child.AddComponent<LocalMoveActor>();instance.Velocity=new Vector3(.75f,0,-.5f);
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("ENGINE_MIGRATOR_MOVEMENT_FIXTURE_READY");
 }
}
'@|Set-Content -LiteralPath "$Project/Assets/Editor/MovementBehaviourFixture.cs"
$start=[Diagnostics.ProcessStartInfo]::new($UnityEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','MovementBehaviourFixture.Run','-logFile',"$Project/movement-fixture-prepare.log")){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode -ne 0){throw "Movement fixture failed: $Project/movement-fixture-prepare.log"};Write-Output "MOVEMENT_FIXTURE_READY: $Project"
