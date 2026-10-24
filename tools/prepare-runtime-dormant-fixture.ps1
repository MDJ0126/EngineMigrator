param([string]$Project='artifacts/runtime-dormant-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeDormant';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimeDormantLedger:MonoBehaviour {public int Deaths;}
'@|Set-Content ($folder+'/RuntimeDormantLedger.cs')
@'
using UnityEngine;
public class RuntimeDormantCounter:MonoBehaviour {
 public int Awakes;public int Enables;public int Starts;public int Updates;public int Disables;public int Destroys;public GameObject Target;public RuntimeDormantCounter Peer;public RuntimeDormantLedger Ledger;public GameObject Copy;public bool CloneInDisable;public bool Inside;
 void Awake(){Awakes++;}void OnEnable(){Enables++;}void Start(){Starts++;}void Update(){Updates++;}
 void OnDisable(){Disables++;if(CloneInDisable&&!Inside){Inside=true;Copy=Instantiate(gameObject);Inside=false;}}
 void OnDestroy(){Destroys++;if(Ledger!=null){Ledger.Deaths=Ledger.Deaths+1;}}
}
'@|Set-Content ($folder+'/RuntimeDormantCounter.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeDormantDriver:MonoBehaviour {
 public GameObject Source;public GameObject First;public GameObject Second;public GameObject SceneSource;public GameObject ReentrySource;public GameObject PendingCopy;public GameObject ReentryCopy;public RuntimeDormantCounter FirstCounter;public RuntimeDormantCounter FirstChild;public RuntimeDormantCounter SecondCounter;public RuntimeDormantCounter SecondChild;public RuntimeDormantCounter SceneCounter;public RuntimeDormantCounter ReentryCounter;public RuntimeDormantCounter PendingCounter;public RuntimeDormantCounter ReentryCopyCounter;public RuntimeDormantLedger Ledger;
 public int Frame;public int FirstAwakes;public int FirstEnables;public int FirstStarts;public int FirstUpdates;public int ChildAwakes;public int ChildStarts;public int ChildUpdates;public int SecondAwakes;public int SecondEnables;public int SecondStarts;public int SecondUpdates;public int SecondChildAwakes;public int SecondChildStarts;public int PendingAwakes;public int PendingEnables;public int PendingStarts;public int PendingUpdates;public int ReentryAwakes;public int ReentryEnables;public int ReentryStarts;public int ReentryUpdates;public int Deaths;public bool FirstSelf;public bool FirstHierarchy;public bool SecondSelf;public bool SecondHierarchy;public bool PendingSelf;public bool PendingHierarchy;public bool PendingEnabled;public bool PendingAlive;public bool ReentrySelf;public bool ReentryHierarchy;public bool ReentryAlive;public bool FirstLinked;public bool SecondLinked;public bool PendingFirstAlive;
 void Update(){Frame++;
 if(Frame==2){First=Instantiate(Source);FirstCounter=First.GetComponent<RuntimeDormantCounter>();FirstChild=FirstCounter.Target.GetComponent<RuntimeDormantCounter>();}
 if(Frame==3){First.SetActive(true);}
 if(Frame==4){FirstCounter.Target.SetActive(true);}
 if(Frame==5){First.SetActive(false);}
 if(Frame==6){Second=Instantiate(First);SecondCounter=Second.GetComponent<RuntimeDormantCounter>();SecondChild=SecondCounter.Target.GetComponent<RuntimeDormantCounter>();}
 if(Frame==7){Second.SetActive(true);}
 if(Frame==8){Destroy(SceneSource);PendingCopy=SceneCounter.Copy;PendingCounter=PendingCopy.GetComponent<RuntimeDormantCounter>();}
 if(Frame==9){PendingCounter.enabled=true;}
 if(Frame==10){Destroy(PendingCopy);}
 if(Frame==11){Destroy(First);}
 if(Frame==12){Destroy(Second);}
 if(Frame==13){ReentrySource.SetActive(false);ReentryCopy=ReentryCounter.Copy;ReentryCopyCounter=ReentryCopy.GetComponent<RuntimeDormantCounter>();}
 if(Frame==14){ReentryCopy.SetActive(true);}
 if(Frame==15){ReentryCopy.SetActive(false);}
 if(Frame==16){Destroy(ReentryCopy);}
 if(Frame==17){Destroy(ReentrySource);}
 if(FirstCounter!=null){FirstAwakes=FirstCounter.Awakes;FirstEnables=FirstCounter.Enables;FirstStarts=FirstCounter.Starts;FirstUpdates=FirstCounter.Updates;ChildAwakes=FirstChild.Awakes;ChildStarts=FirstChild.Starts;ChildUpdates=FirstChild.Updates;FirstSelf=First.activeSelf;FirstHierarchy=First.activeInHierarchy;FirstLinked=FirstCounter.Peer==FirstChild;}
 if(SecondCounter!=null){SecondAwakes=SecondCounter.Awakes;SecondEnables=SecondCounter.Enables;SecondStarts=SecondCounter.Starts;SecondUpdates=SecondCounter.Updates;SecondChildAwakes=SecondChild.Awakes;SecondChildStarts=SecondChild.Starts;SecondSelf=Second.activeSelf;SecondHierarchy=Second.activeInHierarchy;SecondLinked=SecondCounter.Peer==SecondChild;}
 if(PendingCounter!=null){PendingAwakes=PendingCounter.Awakes;PendingEnables=PendingCounter.Enables;PendingStarts=PendingCounter.Starts;PendingUpdates=PendingCounter.Updates;PendingSelf=PendingCopy.activeSelf;PendingHierarchy=PendingCopy.activeInHierarchy;PendingEnabled=PendingCounter.enabled;}
 if(ReentryCopyCounter!=null){ReentryAwakes=ReentryCopyCounter.Awakes;ReentryEnables=ReentryCopyCounter.Enables;ReentryStarts=ReentryCopyCounter.Starts;ReentryUpdates=ReentryCopyCounter.Updates;ReentrySelf=ReentryCopy.activeSelf;ReentryHierarchy=ReentryCopy.activeInHierarchy;}
 PendingAlive=PendingCopy!=null;ReentryAlive=ReentryCopy!=null;PendingFirstAlive=First!=null;Deaths=Ledger.Deaths;
 if(Frame==20){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/RuntimeDormantDriver.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareDormant {public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var driver=new GameObject("Dormant Driver").AddComponent<RuntimeDormantDriver>();driver.Ledger=driver.gameObject.AddComponent<RuntimeDormantLedger>();driver.SceneSource=new GameObject("Clone During Deletion");driver.SceneCounter=driver.SceneSource.AddComponent<RuntimeDormantCounter>();driver.SceneCounter.Ledger=driver.Ledger;driver.SceneCounter.CloneInDisable=true;driver.ReentrySource=new GameObject("Clone During Deactivation");driver.ReentryCounter=driver.ReentrySource.AddComponent<RuntimeDormantCounter>();driver.ReentryCounter.Ledger=driver.Ledger;driver.ReentryCounter.CloneInDisable=true;var prefab=new GameObject("Dormant Prefab");var root=prefab.AddComponent<RuntimeDormantCounter>();var child=new GameObject("Dormant Child");child.transform.SetParent(prefab.transform);var peer=child.AddComponent<RuntimeDormantCounter>();root.Target=child;root.Peer=peer;peer.Target=prefab;peer.Peer=root;child.SetActive(false);prefab.SetActive(false);driver.Source=PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/RuntimeDormant/Dormant.prefab");Object.DestroyImmediate(prefab);MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(driver),-1000);EditorSceneManager.SaveScene(scene,"Assets/RuntimeDormant/Dormant.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimeDormant/Dormant.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_DORMANT_FIXTURE_READY");}}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareDormant.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareDormant.Run','-logFile',($Project+'/prepare-dormant.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Dormant fixture failed'};Write-Output 'RUNTIME_DORMANT_FIXTURE_READY'
