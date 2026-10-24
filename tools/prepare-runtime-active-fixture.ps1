param([string]$Project='artifacts/runtime-active-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project)
if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeActive';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimeActiveRecorder:MonoBehaviour {public int Code;public int Awakes;public int Enables;public int Starts;public int Disables;public int Destroys;public int Updates;public bool Self;public bool Hierarchy;public bool Enabled;}
'@|Set-Content ($folder+'/RuntimeActiveRecorder.cs')
@'
using UnityEngine;
public class RuntimeActiveCounter:MonoBehaviour {
 public RuntimeActiveRecorder Recorder;
 void Awake(){if(Recorder!=null){Recorder.Code=Recorder.Code*10+1;Recorder.Awakes=Recorder.Awakes+1;}}
 void OnEnable(){if(Recorder!=null){Recorder.Code=Recorder.Code*10+2;Recorder.Enables=Recorder.Enables+1;Recorder.Self=gameObject.activeSelf;Recorder.Hierarchy=gameObject.activeInHierarchy;Recorder.Enabled=enabled;}}
 void Start(){if(Recorder!=null){Recorder.Code=Recorder.Code*10+3;Recorder.Starts=Recorder.Starts+1;}}
 void Update(){if(Recorder!=null){Recorder.Updates=Recorder.Updates+1;}}
 void OnDisable(){if(Recorder!=null){Recorder.Code=Recorder.Code*10+4;Recorder.Disables=Recorder.Disables+1;Recorder.Self=gameObject.activeSelf;Recorder.Hierarchy=gameObject.activeInHierarchy;Recorder.Enabled=enabled;}}
 void OnDestroy(){if(Recorder!=null){Recorder.Code=Recorder.Code*10+5;Recorder.Destroys=Recorder.Destroys+1;}}
}
'@|Set-Content ($folder+'/RuntimeActiveCounter.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeActiveDriver:MonoBehaviour {
 public GameObject Parent;public GameObject Child;public GameObject Dormant;public GameObject Created;public RuntimeActiveCounter Counter;public RuntimeActiveCounter Sleeping;public RuntimeActiveRecorder Recorder;public RuntimeActiveRecorder DormantRecorder;public RuntimeActiveRecorder CreatedRecorder;public RuntimeActiveCounter Added;public int Frame;public int Immediate;public int DormantImmediate;public int CreatedImmediate;public bool ParentSelf;public bool ParentHierarchy;public bool ChildSelf;public bool ChildHierarchy;public bool DormantSelf;public bool DormantHierarchy;public bool CreatedSelf;public bool CreatedHierarchy;
 void Update(){Frame++;Recorder.Code=0;DormantRecorder.Code=0;CreatedRecorder.Code=0;
 if(Frame==2){Parent.SetActive(false);}
 if(Frame==3){Parent.SetActive(false);Child.SetActive(false);}
 if(Frame==4){Parent.SetActive(true);}
 if(Frame==5){Child.SetActive(true);}
 if(Frame==6){Parent.SetActive(false);Child.SetActive(false);Child.SetActive(true);}
 if(Frame==7){Parent.SetActive(true);}
 if(Frame==8){Child.SetActive(false);Child.SetActive(true);}
 if(Frame==9){Dormant.SetActive(true);}
 if(Frame==10){Dormant.SetActive(false);Sleeping.enabled=false;}
 if(Frame==11){Dormant.SetActive(true);}
 if(Frame==12){Sleeping.enabled=true;}
 if(Frame==13){Created=new GameObject("Initially inactive runtime object");Created.SetActive(false);Added=Created.AddComponent<RuntimeActiveCounter>();Added.Recorder=CreatedRecorder;}
 if(Frame==14){Created.SetActive(true);}
 if(Frame==15){Created.SetActive(false);Destroy(Added);}
 if(Frame==16){Parent.SetActive(false);Destroy(Counter);}
 Immediate=Recorder.Code;DormantImmediate=DormantRecorder.Code;CreatedImmediate=CreatedRecorder.Code;ParentSelf=Parent.activeSelf;ParentHierarchy=Parent.activeInHierarchy;ChildSelf=Child.activeSelf;ChildHierarchy=Child.activeInHierarchy;DormantSelf=Dormant.activeSelf;DormantHierarchy=Dormant.activeInHierarchy;
 if(Created!=null){CreatedSelf=Created.activeSelf;CreatedHierarchy=Created.activeInHierarchy;}
 if(Frame==20){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/RuntimeActiveDriver.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareActive {public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var driver=new GameObject("Active Driver").AddComponent<RuntimeActiveDriver>();driver.Recorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();driver.DormantRecorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();driver.CreatedRecorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();driver.Parent=new GameObject("Toggle Parent");driver.Child=new GameObject("Toggle Child");driver.Child.transform.SetParent(driver.Parent.transform);driver.Counter=driver.Child.AddComponent<RuntimeActiveCounter>();driver.Counter.Recorder=driver.Recorder;driver.Dormant=new GameObject("Authored Dormant");driver.Dormant.SetActive(false);driver.Sleeping=driver.Dormant.AddComponent<RuntimeActiveCounter>();driver.Sleeping.Recorder=driver.DormantRecorder;EditorSceneManager.SaveScene(scene,"Assets/RuntimeActive/Active.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimeActive/Active.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(driver),-100);AssetDatabase.SaveAssets();Debug.Log("RUNTIME_ACTIVE_FIXTURE_READY");}}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareActive.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true
foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareActive.Run','-logFile',($Project+'/prepare-active.log'))){$s.ArgumentList.Add($a)}
$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Active fixture failed'};Write-Output 'RUNTIME_ACTIVE_FIXTURE_READY'

