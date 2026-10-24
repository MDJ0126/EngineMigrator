param([string]$Project='artifacts/runtime-destroy-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeDestroy';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimeDestroyRecord:MonoBehaviour {public int Code;public int Awakes;public int Enables;public int Starts;public int Updates;public int Disables;public int Destroys;public bool DisEnabled;public bool DisSelf;public bool DisHierarchy;public bool DesEnabled;public bool DesSelf;public bool DesHierarchy;public bool AfterEnabled;public bool AfterSelf;public bool AfterHierarchy;}
'@|Set-Content ($folder+'/RuntimeDestroyRecord.cs')
@'
using UnityEngine;
public class RuntimeWholeCounter:MonoBehaviour {
 public RuntimeDestroyRecord Record;public bool KillAwake;public bool KillStart;public bool EnableInDisable;public bool ActivateInDisable;public bool Inside;
 void Awake(){Record.Code=Record.Code*10+1;Record.Awakes=Record.Awakes+1;if(KillAwake){Destroy(gameObject);}}
 void OnEnable(){Record.Code=Record.Code*10+2;Record.Enables=Record.Enables+1;}
 void Start(){Record.Code=Record.Code*10+3;Record.Starts=Record.Starts+1;if(KillStart){Destroy(gameObject);}}
 void Update(){Record.Updates=Record.Updates+1;}
 void OnDisable(){Record.Code=Record.Code*10+4;Record.Disables=Record.Disables+1;Record.DisEnabled=enabled;Record.DisSelf=gameObject.activeSelf;Record.DisHierarchy=gameObject.activeInHierarchy;if(!Inside){Inside=true;if(EnableInDisable){enabled=true;}if(ActivateInDisable){gameObject.SetActive(true);}Inside=false;}Record.AfterEnabled=enabled;Record.AfterSelf=gameObject.activeSelf;Record.AfterHierarchy=gameObject.activeInHierarchy;}
 void OnDestroy(){Record.Code=Record.Code*10+5;Record.Destroys=Record.Destroys+1;Record.DesEnabled=enabled;Record.DesSelf=gameObject.activeSelf;Record.DesHierarchy=gameObject.activeInHierarchy;}
}
'@|Set-Content ($folder+'/RuntimeWholeCounter.cs')
@'
using UnityEngine;
public class RuntimePrefabProducer:MonoBehaviour {
 public GameObject Source;public GameObject AwakeClone;public GameObject StartClone;public RuntimeWholeCounter AwakeCounter;public RuntimeWholeCounter StartCounter;public RuntimeDestroyRecord AwakeRecord;public RuntimeDestroyRecord StartRecord;public int AwakeImmediate;public int AwakeStartAtReturn;public int StartImmediate;public int StartAtReturn;
 void Awake(){AwakeClone=Instantiate(Source);AwakeCounter=AwakeClone.GetComponent<RuntimeWholeCounter>();var saved=AwakeClone.GetComponent<RuntimeDestroyRecord>();AwakeImmediate=saved.Code;AwakeStartAtReturn=saved.Starts;AwakeRecord.Code=saved.Code;AwakeRecord.Awakes=saved.Awakes;AwakeRecord.Enables=saved.Enables;AwakeCounter.Record=AwakeRecord;}
 void Start(){StartClone=Instantiate(Source);StartCounter=StartClone.GetComponent<RuntimeWholeCounter>();var saved=StartClone.GetComponent<RuntimeDestroyRecord>();StartImmediate=saved.Code;StartAtReturn=saved.Starts;StartRecord.Code=saved.Code;StartRecord.Awakes=saved.Awakes;StartRecord.Enables=saved.Enables;StartCounter.Record=StartRecord;}
}
'@|Set-Content ($folder+'/RuntimePrefabProducer.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeDestroyDriver:MonoBehaviour {
 public GameObject Root;public GameObject Disabled;public GameObject Inactive;public GameObject Created;public RuntimeWholeCounter RootCounter;public RuntimeWholeCounter ChildCounter;public RuntimeWholeCounter DisabledCounter;public RuntimeWholeCounter InactiveCounter;public RuntimeWholeCounter Added;public RuntimeDestroyRecord RootRecord;public RuntimeDestroyRecord ChildRecord;public RuntimeDestroyRecord DisabledRecord;public RuntimeDestroyRecord InactiveRecord;public RuntimeDestroyRecord AwakeKillRecord;public RuntimeDestroyRecord StartKillRecord;public RuntimePrefabProducer Producer;public int Frame;public int Immediate;public bool RootEnabledAtReturn;public bool ChildEnabledAtReturn;public bool RootSelfAtReturn;public bool RootHierarchyAtReturn;public bool RootAliveAtReturn;
 void Update(){Frame++;RootRecord.Code=0;ChildRecord.Code=0;DisabledRecord.Code=0;InactiveRecord.Code=0;AwakeKillRecord.Code=0;StartKillRecord.Code=0;
 if(Frame==2){Destroy(Root);Immediate=RootRecord.Code;RootEnabledAtReturn=RootCounter.enabled;ChildEnabledAtReturn=ChildCounter.enabled;RootSelfAtReturn=Root.activeSelf;RootHierarchyAtReturn=Root.activeInHierarchy;RootAliveAtReturn=Root!=null;}
 if(Frame==3){Destroy(Disabled);Immediate=DisabledRecord.Code;}
 if(Frame==4){Destroy(Inactive);Immediate=InactiveRecord.Code;}
 if(Frame==5){Created=new GameObject("Whole delete in Awake");Created.SetActive(false);Added=Created.AddComponent<RuntimeWholeCounter>();Added.Record=AwakeKillRecord;Added.KillAwake=true;Created.SetActive(true);Immediate=AwakeKillRecord.Code;}
 if(Frame==6){Created=new GameObject("Whole delete in late Start");Created.SetActive(false);Added=Created.AddComponent<RuntimeWholeCounter>();Added.Record=StartKillRecord;Added.KillStart=true;Added.EnableInDisable=true;Added.ActivateInDisable=true;Created.SetActive(true);Immediate=StartKillRecord.Code;}
 if(Frame==7){Destroy(Producer.AwakeClone);Immediate=Producer.AwakeRecord.Code;}
 if(Frame==8){Destroy(Producer.StartClone);Immediate=Producer.StartRecord.Code;}
 if(Frame==20){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/RuntimeDestroyDriver.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareDestroy {public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var driver=new GameObject("Destroy Driver").AddComponent<RuntimeDestroyDriver>();driver.RootRecord=driver.gameObject.AddComponent<RuntimeDestroyRecord>();driver.ChildRecord=driver.gameObject.AddComponent<RuntimeDestroyRecord>();driver.DisabledRecord=driver.gameObject.AddComponent<RuntimeDestroyRecord>();driver.InactiveRecord=driver.gameObject.AddComponent<RuntimeDestroyRecord>();driver.AwakeKillRecord=driver.gameObject.AddComponent<RuntimeDestroyRecord>();driver.StartKillRecord=driver.gameObject.AddComponent<RuntimeDestroyRecord>();driver.Root=new GameObject("Delete Root");driver.RootCounter=driver.Root.AddComponent<RuntimeWholeCounter>();driver.RootCounter.Record=driver.RootRecord;var child=new GameObject("Delete Child");child.transform.SetParent(driver.Root.transform);driver.ChildCounter=child.AddComponent<RuntimeWholeCounter>();driver.ChildCounter.Record=driver.ChildRecord;driver.Disabled=new GameObject("Delete Disabled Component Owner");driver.DisabledCounter=driver.Disabled.AddComponent<RuntimeWholeCounter>();driver.DisabledCounter.Record=driver.DisabledRecord;driver.DisabledCounter.enabled=false;driver.Inactive=new GameObject("Delete Never Active");driver.Inactive.SetActive(false);driver.InactiveCounter=driver.Inactive.AddComponent<RuntimeWholeCounter>();driver.InactiveCounter.Record=driver.InactiveRecord;var prefab=new GameObject("Initial Script Prefab");var record=prefab.AddComponent<RuntimeDestroyRecord>();prefab.AddComponent<RuntimeWholeCounter>().Record=record;var asset=PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/RuntimeDestroy/Initial.prefab");Object.DestroyImmediate(prefab);driver.Producer=driver.gameObject.AddComponent<RuntimePrefabProducer>();driver.Producer.Source=asset;driver.Producer.AwakeRecord=driver.gameObject.AddComponent<RuntimeDestroyRecord>();driver.Producer.StartRecord=driver.gameObject.AddComponent<RuntimeDestroyRecord>();MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(driver),-1000);MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(driver.Producer),-900);EditorSceneManager.SaveScene(scene,"Assets/RuntimeDestroy/Destroy.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimeDestroy/Destroy.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_DESTROY_FIXTURE_READY");}}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareDestroy.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareDestroy.Run','-logFile',($Project+'/prepare-destroy.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Destroy fixture failed'};Write-Output 'RUNTIME_DESTROY_FIXTURE_READY'
