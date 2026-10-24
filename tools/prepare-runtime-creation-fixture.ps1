param([string]$Project='artifacts/runtime-creation-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeCreation';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeCreation:MonoBehaviour {
 public GameObject LongNamed;public GameObject UpperCase;public GameObject LowerCase;public GameObject Created;public GameObject Child;public GameObject DefaultObject;public GameObject Clone;public Transform Parent;public int Frame;public bool DuringDestroy;public Vector3 ReadPosition;public Vector3 ReadLocal;public Vector3 ReadScale;public Quaternion ReadRotation;public Transform ReadParent;
 void Update(){Frame++;if(Frame==2){Created=new GameObject("런타임 \"객체\"");Child=new GameObject("자식");DefaultObject=new GameObject();LongNamed=new GameObject("이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름이름");UpperCase=new GameObject("Object");LowerCase=new GameObject("object");}
 if(Frame==3){Created.name="변경된 객체";Created.transform.SetParent(Parent,false);Created.transform.localPosition=new Vector3(1f,2f,3f);Created.transform.localScale=new Vector3(1.5f,1.5f,1.5f);Child.transform.parent=Created.transform;Child.transform.localPosition=new Vector3(2f,0f,1f);}
 if(Frame==4){Created.transform.SetParent(null,true);}
 if(Frame==5){Created.transform.SetParent(Parent,false);}
 if(Frame==6){Created.transform.position=new Vector3(5f,6f,7f);Created.transform.rotation=new Quaternion(0f,0.70710677f,0f,0.70710677f);ReadPosition=Created.transform.position;ReadLocal=Created.transform.localPosition;ReadScale=Created.transform.localScale;ReadRotation=Created.transform.rotation;ReadParent=Created.transform.parent;}
 if(Frame==8){Clone=Instantiate(Created);}
 if(Frame==10){Clone.transform.SetParent(Parent,true);Clone.transform.localRotation=Quaternion.identity;Clone.transform.localScale=new Vector3(2f,2f,2f);}
 if(Frame==12){Destroy(Created);DuringDestroy=Created!=null;}
 if(Frame==14){Destroy(Clone);Destroy(DefaultObject);Destroy(LongNamed);Destroy(UpperCase);Destroy(LowerCase);}
 if(Frame==18){Created=new GameObject("재생성");Created.transform.position=new Vector3(-2f,1f,3f);}
 if(Frame==24){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/RuntimeCreation.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareCreation {
 public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var parent=new GameObject("Creation Parent");parent.transform.position=new Vector3(10f,2f,-5f);parent.transform.rotation=Quaternion.Euler(0,35,0);parent.transform.localScale=Vector3.one*2f;var driver=new GameObject("Creation Driver").AddComponent<RuntimeCreation>();driver.Parent=parent.transform;EditorSceneManager.SaveScene(scene,"Assets/RuntimeCreation/Creation.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimeCreation/Creation.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_CREATION_FIXTURE_READY");}
}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareCreation.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareCreation.Run','-logFile',($Project+'/prepare-creation.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Creation fixture failed'};Write-Output 'RUNTIME_CREATION_FIXTURE_READY'
