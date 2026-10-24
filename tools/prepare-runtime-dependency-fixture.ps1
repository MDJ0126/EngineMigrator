param([string]$Project='artifacts/runtime-dependency-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeDependency';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimeDependencyNode:MonoBehaviour {
 public int Kind;public int Awakes;public int Enables;public int Starts;public int Updates;public int Disables;
 public GameObject Next;public GameObject SelfAsset;public GameObject LocalRoot;public RuntimeDependencyNode Peer;
 void Awake(){Awakes++;}void OnEnable(){Enables++;}void Start(){Starts++;}void Update(){Updates++;}void OnDisable(){Disables++;}
}
'@|Set-Content ($folder+'/RuntimeDependencyNode.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeDependencyDriver:MonoBehaviour {
 public int Frame;public GameObject Source;public GameObject First;public GameObject Second;public GameObject Third;public GameObject Again;public GameObject Copy;public GameObject Extra;
 public RuntimeDependencyNode A;public RuntimeDependencyNode B;public RuntimeDependencyNode C;public RuntimeDependencyNode A2;public RuntimeDependencyNode Copied;public RuntimeDependencyNode B2;
 public bool AAsset;public bool BAsset;public bool CAsset;public bool A2Asset;public bool CopyAsset;public bool LocalLinked;public bool CopyLinked;public bool PeerLinked;public bool CopyPeerLinked;
 public int FirstKind;public int SecondKind;public int ThirdKind;public int AgainKind;public int CopyKind;public int ExtraKind;public int ReturnAwakes;public int ReturnStarts;
 void Update(){Frame++;
 if(Frame==2){First=Instantiate(Source);A=First.GetComponent<RuntimeDependencyNode>();ReturnAwakes=A.Awakes;ReturnStarts=A.Starts;}
 if(Frame==3){Second=Instantiate(A.Next);B=Second.GetComponent<RuntimeDependencyNode>();}
 if(Frame==4){Third=Instantiate(B.Next);C=Third.GetComponent<RuntimeDependencyNode>();}
 if(Frame==5){Again=Instantiate(C.Next);A2=Again.GetComponent<RuntimeDependencyNode>();}
 if(Frame==6){Copy=Instantiate(Again);Copied=Copy.GetComponent<RuntimeDependencyNode>();}
 if(Frame==7){Destroy(First);}
 if(Frame==8){Extra=Instantiate(A2.Next);B2=Extra.GetComponent<RuntimeDependencyNode>();}
 if(Frame==9){Second.SetActive(false);}
 if(Frame==10){Second.SetActive(true);}
 if(A!=null){FirstKind=A.Kind;AAsset=A.SelfAsset.GetComponent<RuntimeDependencyNode>()==Source.GetComponent<RuntimeDependencyNode>();LocalLinked=A.LocalRoot.GetComponent<RuntimeDependencyNode>()==A;PeerLinked=A.Peer.Peer==A;}
 if(B!=null){SecondKind=B.Kind;BAsset=B.SelfAsset.GetComponent<RuntimeDependencyNode>()==Source.GetComponent<RuntimeDependencyNode>().Next.GetComponent<RuntimeDependencyNode>();}
 if(C!=null){ThirdKind=C.Kind;CAsset=C.SelfAsset.GetComponent<RuntimeDependencyNode>()==B.Next.GetComponent<RuntimeDependencyNode>();}
 if(A2!=null){AgainKind=A2.Kind;A2Asset=A2.SelfAsset.GetComponent<RuntimeDependencyNode>()==Source.GetComponent<RuntimeDependencyNode>();}
 if(Copied!=null){CopyKind=Copied.Kind;CopyAsset=Copied.SelfAsset.GetComponent<RuntimeDependencyNode>()==Source.GetComponent<RuntimeDependencyNode>();CopyLinked=Copied.LocalRoot.GetComponent<RuntimeDependencyNode>()==Copied;CopyPeerLinked=Copied.Peer.Peer==Copied;}
 if(B2!=null){ExtraKind=B2.Kind;}
 if(Frame==12){Destroy(Second);Destroy(Third);}
 if(Frame==13){Destroy(Again);Destroy(Copy);Destroy(Extra);}
 if(Frame==20){SceneManager.LoadScene(1);}
 }
}
'@|Set-Content ($folder+'/RuntimeDependencyDriver.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareDependency {
 static GameObject Make(string name,int kind){var root=new GameObject(name);var c=root.AddComponent<RuntimeDependencyNode>();c.Kind=kind;c.LocalRoot=root;var child=new GameObject(name+" Child");child.transform.SetParent(root.transform);var p=child.AddComponent<RuntimeDependencyNode>();p.Kind=kind+10;p.LocalRoot=root;c.Peer=p;p.Peer=c;var asset=PrefabUtility.SaveAsPrefabAsset(root,"Assets/RuntimeDependency/"+name+".prefab");Object.DestroyImmediate(root);return asset;}
 static void Link(GameObject asset,GameObject next){var path=AssetDatabase.GetAssetPath(asset);var root=PrefabUtility.LoadPrefabContents(path);try{foreach(var c in root.GetComponentsInChildren<RuntimeDependencyNode>(true)){c.Next=next;c.SelfAsset=asset;}PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
 public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var a=Make("DependencyA",1);var b=Make("DependencyB",2);var c=Make("DependencyC",3);Link(a,b);Link(b,c);Link(c,a);var driver=new GameObject("Dependency Driver").AddComponent<RuntimeDependencyDriver>();driver.Source=a;MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(driver),-1000);EditorSceneManager.SaveScene(scene,"Assets/RuntimeDependency/Dependency.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimeDependency/Dependency.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_DEPENDENCY_FIXTURE_READY");}
}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareDependency.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareDependency.Run','-logFile',($Project+'/prepare-dependency.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Dependency fixture failed'};Write-Output 'RUNTIME_DEPENDENCY_FIXTURE_READY'
