param([string]$Project='artifacts/runtime-component-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeComponents';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimeAddedBase:MonoBehaviour {public RuntimeComponents Driver;public int Value=2;public int Awakes;public int Starts;public int Updates;public Vector3 AwakePosition;public int ReadValue(){return Value;}public bool IsAbove(int threshold){return Value>threshold;}public GameObject Echo(GameObject value){return value;}public double Sum(double a,double b){return a+b;}public float Scale(float value){return value*2f;}public virtual int AddValue(int amount){Value+=amount;return Value;}protected virtual void Awake(){Awakes++;AwakePosition=transform.position;}protected virtual void Start(){Starts++;}protected virtual void Update(){Updates++;}}
'@|Set-Content ($folder+'/RuntimeAddedBase.cs')
@'
using UnityEngine;
public class RuntimeAddedCounter:RuntimeAddedBase {public int DerivedAwakes;public override int AddValue(int amount){return base.AddValue(amount+1);}protected override void Awake(){base.Awake();DerivedAwakes++;Value=4;}}
'@|Set-Content ($folder+'/RuntimeAddedCounter.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeComponents:MonoBehaviour {
 public RuntimeAddedBase Authored;public GameObject Created;public GameObject Clone;public RuntimeAddedCounter Added;public RuntimeAddedCounter Second;public RuntimeAddedBase Found;public RuntimeAddedBase AfterRemoval;public int Frame;public int AtAddAwakes;public int AtAddStarts;public int AtAddValue;public bool Same;public bool MissingMatches;public bool DuringDestroy;public int AfterSetValue;public int AfterCall;public int ReadCall;public bool CallBool;public GameObject CallOwner;public double CallNumber;public float CallFloat;
 void Update(){Frame++;if(Frame==2){Created=new GameObject("동적 컴포넌트");Created.transform.position=new Vector3(3f,4f,5f);Added=Created.AddComponent<RuntimeAddedCounter>();AtAddAwakes=Added.Awakes;AtAddStarts=Added.Starts;AtAddValue=Added.Value;Added.Value=7;Added.Driver=Authored.Driver;AfterSetValue=Added.Value;AfterCall=Added.AddValue(3);ReadCall=Added.ReadValue();CallBool=Added.IsAbove(9);CallOwner=Added.Echo(Created);CallNumber=Added.Sum(1.25d,2.5d);CallFloat=Added.Scale(1.5f);Found=Created.GetComponent<RuntimeAddedBase>();Same=Found==Added;}
 if(Frame==4){Second=Created.AddComponent<RuntimeAddedCounter>();Second.Value=13;}
 if(Frame==6){Destroy(Added);DuringDestroy=Added!=null;}
 if(Frame==7){RuntimeAddedBase missing=null;MissingMatches=missing==Added;}if(Frame==8){AfterRemoval=Created.GetComponent<RuntimeAddedBase>();Clone=Instantiate(Created);}
 if(Frame==12){Destroy(Created);Destroy(Clone);}
 if(Frame==18){SceneManager.LoadScene(1);}}
}
'@|Set-Content ($folder+'/RuntimeComponents.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareComponents {public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var driver=new GameObject("Component Driver").AddComponent<RuntimeComponents>();var target=new GameObject("Authored Component");target.transform.position=new Vector3(10f,0f,0f);var authored=target.AddComponent<RuntimeAddedBase>();driver.Authored=authored;authored.Driver=driver;EditorSceneManager.SaveScene(scene,"Assets/RuntimeComponents/Components.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimeComponents/Components.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_COMPONENT_FIXTURE_READY");}}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareComponents.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareComponents.Run','-logFile',($Project+'/prepare-components.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Component fixture failed'};Write-Output 'RUNTIME_COMPONENT_FIXTURE_READY'
