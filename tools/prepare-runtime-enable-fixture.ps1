param([string]$Project='artifacts/runtime-enable-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeEnable';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimeEnableRecorder:MonoBehaviour{public int Code;public int Awakes;public int Enables;public int Disables;public int Starts;public int Destroys;public bool LastCallbackEnabled;}
'@|Set-Content ($folder+'/RuntimeEnableRecorder.cs')
@'
using UnityEngine;
public class RuntimeEnableCounter:MonoBehaviour {
 public RuntimeEnableRecorder Recorder;public int Awakes;public int Enables;public int Disables;public int Starts;public int Updates;
 void Awake(){Recorder=GetComponent<RuntimeEnableRecorder>();Awakes++;if(Recorder!=null){Recorder.Code=Recorder.Code*10+1;Recorder.Awakes=Recorder.Awakes+1;}}
 void OnEnable(){Enables++;if(Recorder!=null){Recorder.Code=Recorder.Code*10+2;Recorder.Enables=Recorder.Enables+1;Recorder.LastCallbackEnabled=enabled;}}
 void Start(){Starts++;if(Recorder!=null){Recorder.Code=Recorder.Code*10+3;Recorder.Starts=Recorder.Starts+1;}}
 void Update(){Updates++;}
 void OnDisable(){Disables++;if(Recorder!=null){Recorder.Code=Recorder.Code*10+4;Recorder.Disables=Recorder.Disables+1;Recorder.LastCallbackEnabled=enabled;}}
 void OnDestroy(){if(Recorder!=null){Recorder.Code=Recorder.Code*10+5;Recorder.Destroys=Recorder.Destroys+1;}}
}
'@|Set-Content ($folder+'/RuntimeEnableCounter.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeEnableDriver:MonoBehaviour {
 public GameObject Created;public RuntimeEnableCounter Counter;public RuntimeEnableRecorder Recorder;public RuntimeEnableCounter Authored;public RuntimeEnableRecorder AuthoredRecorder;public int Frame;public int Immediate;public int AtReturnStart;public bool ReadEnabled;
 void Update(){Frame++;Immediate=0;if(Recorder!=null){Recorder.Code=0;}AuthoredRecorder.Code=0;
 if(Frame==2){Created=new GameObject("활성 토글");Recorder=Created.AddComponent<RuntimeEnableRecorder>();Counter=Created.AddComponent<RuntimeEnableCounter>();Immediate=Recorder.Code;AtReturnStart=Counter.Starts;}
 if(Frame==3){Counter.enabled=false;Immediate=Recorder.Code;}
 if(Frame==4){Counter.enabled=true;Immediate=Recorder.Code;}
 if(Frame==5){Counter.enabled=false;Counter.enabled=false;Immediate=Recorder.Code;}
 if(Frame==6){Counter.enabled=true;Counter.enabled=true;Immediate=Recorder.Code;}
 if(Frame==7){Counter.enabled=false;Destroy(Counter);Immediate=Recorder.Code;}
 if(Frame==8){Counter=Created.AddComponent<RuntimeEnableCounter>();Counter.enabled=false;Immediate=Recorder.Code;AtReturnStart=Counter.Starts;}
 if(Frame==9){Counter.enabled=true;Immediate=Recorder.Code;AtReturnStart=Counter.Starts;}
 if(Frame==10){Counter.enabled=false;Counter.enabled=true;Immediate=Recorder.Code;}
 if(Frame==11){Destroy(Counter);Immediate=Recorder.Code;}
 if(Frame==12){Authored.enabled=true;Immediate=AuthoredRecorder.Code;AtReturnStart=Authored.Starts;}
 if(Frame==13){Authored.enabled=false;Immediate=AuthoredRecorder.Code;}
 if(Frame==14){Authored.enabled=true;Immediate=AuthoredRecorder.Code;}
 if(Frame==16){Authored.enabled=false;Authored.enabled=true;Immediate=AuthoredRecorder.Code;}
 if(Counter!=null){ReadEnabled=Counter.enabled;}if(Frame==20){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/RuntimeEnableDriver.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareEnable {public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var driver=new GameObject("Enable Driver").AddComponent<RuntimeEnableDriver>();var owner=new GameObject("Initially Disabled");var recorder=owner.AddComponent<RuntimeEnableRecorder>();var authored=owner.AddComponent<RuntimeEnableCounter>();authored.Recorder=recorder;authored.enabled=false;driver.Authored=authored;driver.AuthoredRecorder=recorder;EditorSceneManager.SaveScene(scene,"Assets/RuntimeEnable/Enable.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimeEnable/Enable.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_ENABLE_FIXTURE_READY");}}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareEnable.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareEnable.Run','-logFile',($Project+'/prepare-enable.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Enable fixture failed'};Write-Output 'RUNTIME_ENABLE_FIXTURE_READY'
