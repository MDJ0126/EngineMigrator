param([string]$Project='artifacts/runtime-world-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project)
if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeWorld';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
using UnityEngine.SceneManagement;
public class RuntimeSpawner:MonoBehaviour {
 public GameObject Source;public GameObject Spawned;public int Frame;public bool FirstCreated;public bool Removed;public bool SecondCreated;public bool AliveImmediatelyAfterDestroy;
 void Update(){Frame++;if(Frame==10){Spawned=Instantiate(Source);FirstCreated=Spawned!=null;}if(Frame==20){Destroy(Spawned);AliveImmediatelyAfterDestroy=Spawned!=null;Removed=true;}if(Frame==30){Spawned=Instantiate(Source);SecondCreated=Spawned!=null;}if(Frame==40){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/RuntimeSpawner.cs')
@'
using UnityEngine;
public class RuntimeTemplateCounter:MonoBehaviour {
 public GameObject Target;public GameObject External;public int Awakes;public int Starts;public int Updates;
 void Awake(){Awakes++;}void Start(){Starts++;}void Update(){Updates++;}
}
'@|Set-Content ($folder+'/RuntimeTemplateCounter.cs')
@'
using UnityEngine;
using UnityEngine.SceneManagement;
public class RuntimeMiddle:MonoBehaviour {public int Frame;void Update(){Frame++;if(Frame==10){SceneManager.LoadScene("RuntimeFinal");}}}
'@|Set-Content ($folder+'/RuntimeMiddle.cs')
@'
using UnityEngine;
public class RuntimeFinal:MonoBehaviour {public int Frame;void Update(){Frame++;}}
'@|Set-Content ($folder+'/RuntimeFinal.cs')
@'
using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareRuntimeWorld {
 public static void Run(){
  const string folder="Assets/RuntimeWorld/";
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var driver=new GameObject("Runtime Driver").AddComponent<RuntimeSpawner>();var source=new GameObject("Runtime Template");source.transform.position=new Vector3(2,3,4);var child=new GameObject("Template Child");child.transform.SetParent(source.transform,false);child.transform.localPosition=new Vector3(1,2,3);var counter=source.AddComponent<RuntimeTemplateCounter>();counter.Awakes=0;counter.Target=child;counter.External=driver.gameObject;driver.Source=source;
  EditorSceneManager.SaveScene(scene,folder+"RuntimeStart.unity");
  scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Middle Driver").AddComponent<RuntimeMiddle>();EditorSceneManager.SaveScene(scene,folder+"RuntimeMiddle.unity");
  scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Final Driver").AddComponent<RuntimeFinal>();EditorSceneManager.SaveScene(scene,folder+"RuntimeFinal.unity");
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(folder+"RuntimeStart.unity",true),new EditorBuildSettingsScene(folder+"RuntimeMiddle.unity",true),new EditorBuildSettingsScene(folder+"RuntimeFinal.unity",true)};
  AssetDatabase.SaveAssets();Debug.Log("RUNTIME_WORLD_FIXTURE_READY");
 }
}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareRuntimeWorld.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true
foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareRuntimeWorld.Run','-logFile',($Project+'/prepare-runtime-world.log'))){$s.ArgumentList.Add($a)}
$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Runtime world fixture failed'}
Write-Output 'RUNTIME_WORLD_FIXTURE_READY'
