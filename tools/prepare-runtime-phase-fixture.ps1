param([string]$Project='artifacts/runtime-phase-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimePhase';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimePhaseCounter:MonoBehaviour {
 public RuntimeActiveRecorder Recorder;public bool KillAwake;public bool KillStart;
 void Awake(){Recorder.Code=Recorder.Code*10+1;Recorder.Awakes=Recorder.Awakes+1;if(KillAwake){Destroy(this);}}
 void OnEnable(){Recorder.Code=Recorder.Code*10+2;Recorder.Enables=Recorder.Enables+1;Recorder.Enabled=enabled;}
 void Start(){Recorder.Code=Recorder.Code*10+3;Recorder.Starts=Recorder.Starts+1;if(KillStart){Destroy(this);}}
 void Update(){Recorder.Updates=Recorder.Updates+1;}
 void OnDisable(){Recorder.Code=Recorder.Code*10+4;Recorder.Disables=Recorder.Disables+1;Recorder.Enabled=enabled;}
 void OnDestroy(){Recorder.Code=Recorder.Code*10+5;Recorder.Destroys=Recorder.Destroys+1;}
}
'@|Set-Content ($folder+'/RuntimePhaseCounter.cs')
@'
using UnityEngine;
public class RuntimePhaseProducer:MonoBehaviour {
 public RuntimeActiveRecorder AwakeRecorder;public RuntimeActiveRecorder StartRecorder;public RuntimeActiveRecorder KillAwakeRecorder;public RuntimeActiveRecorder KillStartRecorder;
 public GameObject AwakeObject;public GameObject StartObject;public GameObject KillAwakeObject;public GameObject KillStartObject;
 public RuntimePhaseCounter AwakeCounter;public RuntimePhaseCounter StartCounter;public RuntimePhaseCounter KillAwakeCounter;public RuntimePhaseCounter KillStartCounter;
 public int AwakeImmediate;public int AwakeReturnStart;public int StartImmediate;public int StartReturnStart;public int KillAwakeImmediate;public int KillStartImmediate;
 void Awake(){AwakeObject=new GameObject("Created inside Awake");AwakeObject.SetActive(false);AwakeCounter=AwakeObject.AddComponent<RuntimePhaseCounter>();AwakeCounter.Recorder=AwakeRecorder;AwakeObject.SetActive(true);AwakeImmediate=AwakeRecorder.Code;AwakeReturnStart=AwakeRecorder.Starts;
 KillAwakeObject=new GameObject("Destroyed inside Awake");KillAwakeObject.SetActive(false);KillAwakeCounter=KillAwakeObject.AddComponent<RuntimePhaseCounter>();KillAwakeCounter.Recorder=KillAwakeRecorder;KillAwakeCounter.KillAwake=true;KillAwakeObject.SetActive(true);KillAwakeImmediate=KillAwakeRecorder.Code;}
 void Start(){StartObject=new GameObject("Created inside Start");StartObject.SetActive(false);StartCounter=StartObject.AddComponent<RuntimePhaseCounter>();StartCounter.Recorder=StartRecorder;StartObject.SetActive(true);StartImmediate=StartRecorder.Code;StartReturnStart=StartRecorder.Starts;
 KillStartObject=new GameObject("Destroyed inside Start");KillStartObject.SetActive(false);KillStartCounter=KillStartObject.AddComponent<RuntimePhaseCounter>();KillStartCounter.Recorder=KillStartRecorder;KillStartCounter.KillStart=true;KillStartObject.SetActive(true);KillStartImmediate=KillStartRecorder.Code;}
}
'@|Set-Content ($folder+'/RuntimePhaseProducer.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeParentDriver:MonoBehaviour {
 public GameObject ActiveParent;public GameObject InactiveParent;public GameObject Child;public RuntimeActiveRecorder Recorder;public RuntimeActiveCounter Counter;public RuntimePhaseProducer Producer;public int Frame;public int Immediate;public bool Self;public bool Hierarchy;public bool ParentExists;public Vector3 World;public Vector3 Local;public Quaternion Rotation;
 void Update(){Frame++;Recorder.Code=0;
 if(Frame==2){Child.transform.SetParent(InactiveParent.transform);}
 if(Frame==3){Child.transform.SetParent(InactiveParent.transform);}
 if(Frame==4){Child.transform.SetParent(null);}
 if(Frame==5){Child.transform.SetParent(ActiveParent.transform,false);}
 if(Frame==6){ActiveParent.SetActive(false);}
 if(Frame==7){Child.transform.SetParent(null,false);}
 if(Frame==8){Child.SetActive(false);}
 if(Frame==9){Child.transform.SetParent(InactiveParent.transform,true);}
 if(Frame==10){Child.SetActive(true);}
 if(Frame==11){InactiveParent.SetActive(true);}
 if(Frame==12){Child.transform.SetParent(ActiveParent.transform,true);}
 if(Frame==13){ActiveParent.SetActive(true);}
 if(Frame==14){Child.transform.parent=null;}
 if(Frame==15){Child.SetActive(false);Child.transform.SetParent(InactiveParent.transform);}
 if(Frame==16){Child.SetActive(true);}
 if(Frame==17){Child.transform.SetParent(null);}
 Immediate=Recorder.Code;Self=Child.activeSelf;Hierarchy=Child.activeInHierarchy;ParentExists=Child.transform.parent!=null;World=Child.transform.position;Local=Child.transform.localPosition;Rotation=Child.transform.rotation;
 if(Frame==20){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/RuntimeParentDriver.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPreparePhase {public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var driver=new GameObject("Parent Driver").AddComponent<RuntimeParentDriver>();driver.Recorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();driver.ActiveParent=new GameObject("Active Parent");driver.ActiveParent.transform.position=new Vector3(3,2,-1);driver.ActiveParent.transform.rotation=Quaternion.Euler(0,35,0);driver.InactiveParent=new GameObject("Inactive Parent");driver.InactiveParent.transform.position=new Vector3(-2,1,4);driver.InactiveParent.SetActive(false);driver.Child=new GameObject("Moving Child");driver.Child.transform.position=new Vector3(1,2,3);driver.Counter=driver.Child.AddComponent<RuntimeActiveCounter>();driver.Counter.Recorder=driver.Recorder;var producer=driver.gameObject.AddComponent<RuntimePhaseProducer>();driver.Producer=producer;producer.AwakeRecorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();producer.StartRecorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();producer.KillAwakeRecorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();producer.KillStartRecorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(driver),-100);MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(producer),-90);EditorSceneManager.SaveScene(scene,"Assets/RuntimePhase/Phase.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimePhase/Phase.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_PHASE_FIXTURE_READY");}}
'@|Set-Content ($Project+'/Assets/Editor/EMPreparePhase.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPreparePhase.Run','-logFile',($Project+'/prepare-phase.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Phase fixture failed'};Write-Output 'RUNTIME_PHASE_FIXTURE_READY'
