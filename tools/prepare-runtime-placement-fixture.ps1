param([string]$Project='artifacts/runtime-placement-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'};& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimePlacement';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class PlacementCounter:MonoBehaviour {
 public GameObject Target;public Transform TargetTransform;public Vector3 AwakeWorld;public Vector3 AwakeLocal;public Quaternion AwakeRotation;public Quaternion AwakeLocalRotation;public Transform ParentAtAwake;public int Awakes;public int Starts;public int Updates;
 void Awake(){Awakes++;AwakeWorld=transform.position;AwakeLocal=transform.localPosition;AwakeRotation=transform.rotation;AwakeLocalRotation=transform.localRotation;ParentAtAwake=transform.parent;}
 void Start(){Starts++;}void Update(){Updates++;}
}
'@|Set-Content ($folder+'/PlacementCounter.cs')
@'
using UnityEngine;
using UnityEngine.SceneManagement;
public class PlacementDriver:MonoBehaviour {
 public GameObject Prefab;public GameObject SceneSource;public Transform Parent;public Quaternion Rotation=new Quaternion(0f,0.70710677f,0f,0.70710677f);public GameObject LastPrefab;public GameObject LastScene;public int Frame;
 void Update(){Frame++;if(Frame==3){LastPrefab=Instantiate(Prefab);LastScene=Instantiate(SceneSource);}if(Frame==6){LastPrefab=Instantiate(Prefab,new Vector3(8f,3f,2f),Rotation);LastScene=Instantiate(SceneSource,new Vector3(8f,3f,2f),Rotation);}if(Frame==9){LastPrefab=Instantiate(Prefab,Parent);LastScene=Instantiate(SceneSource,Parent);}if(Frame==12){LastPrefab=Instantiate(Prefab,Parent,true);LastScene=Instantiate(SceneSource,Parent,true);}if(Frame==15){LastPrefab=Instantiate(Prefab,Parent,false);LastScene=Instantiate(SceneSource,Parent,false);}if(Frame==18){LastPrefab=Instantiate(Prefab,new Vector3(-3f,7f,4f),Rotation,Parent);LastScene=Instantiate(SceneSource,new Vector3(-3f,7f,4f),Rotation,Parent);}if(Frame==21){LastPrefab=Instantiate(Prefab,new Vector3(1f,5f,9f),Quaternion.identity,null);LastScene=Instantiate(SceneSource,new Vector3(1f,5f,9f),Quaternion.identity,null);}if(Frame==24){LastPrefab=Instantiate(Prefab,(Transform)null);LastScene=Instantiate(SceneSource,(Transform)null);}if(Frame==27){LastPrefab=Instantiate(Prefab,null,true);LastScene=Instantiate(SceneSource,null,true);}if(Frame==30){LastPrefab=Instantiate(Prefab,null,false);LastScene=Instantiate(SceneSource,null,false);}if(Frame==40){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/PlacementDriver.cs')
@'
using System;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPreparePlacement {
 public static void Run(){const string folder="Assets/RuntimePlacement/";if(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Placement.prefab"))throw new Exception("Fixture already exists");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Placement Root");root.transform.position=new Vector3(2,3,4);root.transform.rotation=Quaternion.Euler(10,20,30);root.transform.localScale=new Vector3(1.2f,1.2f,1.2f);var child=new GameObject("Placement Child");child.transform.SetParent(root.transform,false);child.transform.localPosition=new Vector3(1,2,3);child.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectScopeFixture/Shared.asset");child.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectScopeFixture/Shared.mat");var counter=root.AddComponent<PlacementCounter>();counter.Target=child;counter.TargetTransform=child.transform;counter.Awakes=0;var asset=PrefabUtility.SaveAsPrefabAsset(root,folder+"Placement.prefab");UnityEngine.Object.DestroyImmediate(root);
 var sourceParent=new GameObject("Source Parent");sourceParent.transform.position=new Vector3(20,4,10);sourceParent.transform.rotation=Quaternion.Euler(0,35,0);sourceParent.transform.localScale=Vector3.one*1.5f;var source=(GameObject)PrefabUtility.InstantiatePrefab(asset);source.transform.SetParent(sourceParent.transform,false);source.transform.localPosition=new Vector3(2,3,4);PrefabUtility.RecordPrefabInstancePropertyModifications(source.transform);
 var destination=new GameObject("Destination Parent");destination.transform.position=new Vector3(-10,2,5);destination.transform.rotation=Quaternion.Euler(0,-25,0);destination.transform.localScale=Vector3.one*2f;var driver=new GameObject("Placement Driver").AddComponent<PlacementDriver>();driver.Prefab=asset;driver.SceneSource=source;driver.Parent=destination.transform;
 EditorSceneManager.SaveScene(scene,folder+"Placement.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(folder+"Placement.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_PLACEMENT_FIXTURE_READY");}
}
'@|Set-Content ($Project+'/Assets/Editor/EMPreparePlacement.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPreparePlacement.Run','-logFile',($Project+'/prepare-placement.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Placement fixture failed'};Write-Output 'RUNTIME_PLACEMENT_FIXTURE_READY'
