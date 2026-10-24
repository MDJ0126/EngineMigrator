param([string]$Project='artifacts/runtime-order-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeOrder';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimeOrderLedger:MonoBehaviour {public double Code;}
'@|Set-Content ($folder+'/RuntimeOrderLedger.cs')
@'
using UnityEngine;
public class RuntimeOrderBase:MonoBehaviour {
 public RuntimeOrderLedger Ledger;public RuntimeOrderBase Other;public int Id;public int Awakes;public int Enables;public int Starts;public int Updates;public int AwakeSawOther;public int EnableSawOther;public int StartSawOther;
 protected virtual void Awake(){AwakeSawOther=Other.Awakes;Awakes++;Ledger.Code=Ledger.Code*100d+Id;}
 protected virtual void OnEnable(){EnableSawOther=Other.Enables;Enables++;Ledger.Code=Ledger.Code*100d+Id+10;}
 protected virtual void Start(){StartSawOther=Other.Starts;Starts++;Ledger.Code=Ledger.Code*100d+Id+20;}
 void Update(){Updates++;}
}
'@|Set-Content ($folder+'/RuntimeOrderBase.cs')
@'
using UnityEngine;
public class RuntimeOrderFirst:RuntimeOrderBase {protected override void Awake(){base.Awake();}protected override void OnEnable(){base.OnEnable();}protected override void Start(){base.Start();}}
'@|Set-Content ($folder+'/RuntimeOrderFirst.cs')
@'
using UnityEngine;
public class RuntimeOrderSecond:RuntimeOrderBase {protected override void Awake(){base.Awake();}protected override void OnEnable(){base.OnEnable();}protected override void Start(){base.Start();}}
'@|Set-Content ($folder+'/RuntimeOrderSecond.cs')
@'
using UnityEngine;
public class RuntimeReenter:MonoBehaviour {
 public RuntimeActiveRecorder Recorder;public bool DisableOnEnable;public bool EnableOnDisable;public bool DeactivateOnEnable;public bool ActivateOnDisable;public bool Inside;
 void Awake(){Recorder.Code=Recorder.Code*10+1;Recorder.Awakes=Recorder.Awakes+1;}
 void OnEnable(){Recorder.Code=Recorder.Code*10+2;Recorder.Enables=Recorder.Enables+1;Recorder.Enabled=enabled;Recorder.Self=gameObject.activeSelf;Recorder.Hierarchy=gameObject.activeInHierarchy;
 if(DisableOnEnable&&!Inside){Inside=true;enabled=false;Inside=false;}if(DeactivateOnEnable&&!Inside){Inside=true;gameObject.SetActive(false);Inside=false;}}
 void Start(){Recorder.Code=Recorder.Code*10+3;Recorder.Starts=Recorder.Starts+1;}
 void Update(){Recorder.Updates=Recorder.Updates+1;}
 void OnDisable(){Recorder.Code=Recorder.Code*10+4;Recorder.Disables=Recorder.Disables+1;Recorder.Enabled=enabled;Recorder.Self=gameObject.activeSelf;Recorder.Hierarchy=gameObject.activeInHierarchy;
 if(EnableOnDisable&&!Inside){Inside=true;enabled=true;Inside=false;}if(ActivateOnDisable&&!Inside){Inside=true;gameObject.SetActive(true);Inside=false;}}
}
'@|Set-Content ($folder+'/RuntimeReenter.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeOrderDriver:MonoBehaviour {
 public RuntimeOrderLedger Ledger;public RuntimeOrderFirst First;public RuntimeOrderSecond Second;public GameObject ComponentNode;public GameObject ObjectNode;public RuntimeReenter Component;public RuntimeReenter Object;public RuntimeActiveRecorder ComponentRecorder;public RuntimeActiveRecorder ObjectRecorder;public int Frame;public int ComponentImmediate;public int ObjectImmediate;public bool ComponentEnabled;public bool ObjectEnabled;public bool ObjectSelf;public bool ObjectHierarchy;public double InitialCode;
 void Update(){Frame++;ComponentRecorder.Code=0;ObjectRecorder.Code=0;
 if(Frame==2){ComponentNode.SetActive(true);ObjectNode.SetActive(true);}
 if(Frame==3){Component.DisableOnEnable=false;Component.enabled=true;Object.DeactivateOnEnable=false;ObjectNode.SetActive(true);}
 if(Frame==4){Component.EnableOnDisable=true;Component.enabled=false;Object.ActivateOnDisable=true;ObjectNode.SetActive(false);}
 if(Frame==5){Component.EnableOnDisable=false;Component.enabled=false;Object.ActivateOnDisable=false;ObjectNode.SetActive(true);}
 if(Frame==6){Component.enabled=true;ObjectNode.SetActive(false);}
 if(Frame==7){ObjectNode.SetActive(true);}
 if(Frame==8){Object.enabled=false;}
 if(Frame==9){Object.enabled=true;}
 if(Frame==10){ComponentNode.SetActive(false);}
 if(Frame==11){ComponentNode.SetActive(true);}
 ComponentImmediate=ComponentRecorder.Code;ObjectImmediate=ObjectRecorder.Code;ComponentEnabled=Component.enabled;ObjectEnabled=Object.enabled;ObjectSelf=ObjectNode.activeSelf;ObjectHierarchy=ObjectNode.activeInHierarchy;InitialCode=Ledger.Code;
 if(Frame==20){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/RuntimeOrderDriver.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareOrder {public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var second=new GameObject("Created second in execution order").AddComponent<RuntimeOrderSecond>();var first=new GameObject("Created first in execution order").AddComponent<RuntimeOrderFirst>();var driver=new GameObject("Order Driver").AddComponent<RuntimeOrderDriver>();driver.Ledger=driver.gameObject.AddComponent<RuntimeOrderLedger>();first.Id=1;second.Id=2;first.Other=second;second.Other=first;first.Ledger=driver.Ledger;second.Ledger=driver.Ledger;driver.First=first;driver.Second=second;driver.ComponentRecorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();driver.ObjectRecorder=driver.gameObject.AddComponent<RuntimeActiveRecorder>();driver.ComponentNode=new GameObject("Component Reentry");driver.ComponentNode.SetActive(false);driver.Component=driver.ComponentNode.AddComponent<RuntimeReenter>();driver.Component.Recorder=driver.ComponentRecorder;driver.Component.DisableOnEnable=true;driver.ObjectNode=new GameObject("Object Reentry");driver.ObjectNode.SetActive(false);driver.Object=driver.ObjectNode.AddComponent<RuntimeReenter>();driver.Object.Recorder=driver.ObjectRecorder;driver.Object.DeactivateOnEnable=true;MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(driver),-1000);MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(first),-100);MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(second),100);EditorSceneManager.SaveScene(scene,"Assets/RuntimeOrder/Order.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimeOrder/Order.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_ORDER_FIXTURE_READY");}}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareOrder.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareOrder.Run','-logFile',($Project+'/prepare-order.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Order fixture failed'};Write-Output 'RUNTIME_ORDER_FIXTURE_READY'
