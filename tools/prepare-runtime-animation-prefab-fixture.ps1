param([string]$Project='artifacts/runtime-animation-prefab-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimeAnimationPrefab';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimeAnimatedCounter:MonoBehaviour {public int Awakes;public int Enables;public int Starts;public int Updates;public int Disables;public GameObject Target;void Awake(){Awakes++;}void OnEnable(){Enables++;}void Start(){Starts++;}void Update(){Updates++;}void OnDisable(){Disables++;}}
'@|Set-Content ($folder+'/RuntimeAnimatedCounter.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimeAnimationDriver:MonoBehaviour {
 public int Frame;public GameObject Loop;public GameObject Dormant;public GameObject Clamp;public GameObject Once;public GameObject Disabled;
 public GameObject First;public GameObject Second;public GameObject Copy;public GameObject Sleeping;public GameObject Ended;public GameObject Single;public GameObject Still;
 public RuntimeAnimatedCounter A;public RuntimeAnimatedCounter B;public RuntimeAnimatedCounter C;public RuntimeAnimatedCounter D;public RuntimeAnimatedCounter E;public RuntimeAnimatedCounter F;public RuntimeAnimatedCounter G;
 public Vector3 FirstPose;public Vector3 SecondPose;public Vector3 CopyPose;public Vector3 SleepingPose;public Vector3 ClampPose;public Vector3 OncePose;public Vector3 DisabledPose;
 void Update(){Frame++;
 if(Frame==2){First=Instantiate(Loop);A=First.GetComponent<RuntimeAnimatedCounter>();}
 if(Frame==4){Second=Instantiate(Loop);B=Second.GetComponent<RuntimeAnimatedCounter>();}
 if(Frame==6){Copy=Instantiate(First);C=Copy.GetComponent<RuntimeAnimatedCounter>();}
 if(Frame==7){Destroy(First);}
 if(Frame==8){Sleeping=Instantiate(Dormant);D=Sleeping.GetComponent<RuntimeAnimatedCounter>();}
 if(Frame==9){Sleeping.SetActive(true);}
 if(Frame==10){Second.SetActive(false);}
 if(Frame==12){Second.SetActive(true);}
 if(Frame==13){Ended=Instantiate(Clamp);E=Ended.GetComponent<RuntimeAnimatedCounter>();}
 if(Frame==14){Single=Instantiate(Once);F=Single.GetComponent<RuntimeAnimatedCounter>();}
 if(Frame==15){Still=Instantiate(Disabled);G=Still.GetComponent<RuntimeAnimatedCounter>();}
 if(A!=null){FirstPose=A.Target.transform.localPosition;}
 if(B!=null){SecondPose=B.Target.transform.localPosition;}
 if(C!=null){CopyPose=C.Target.transform.localPosition;}
 if(D!=null){SleepingPose=D.Target.transform.localPosition;}
 if(E!=null){ClampPose=E.Target.transform.localPosition;}
 if(F!=null){OncePose=F.Target.transform.localPosition;}
 if(G!=null){DisabledPose=G.Target.transform.localPosition;}
 if(Frame==20){Destroy(Second);Destroy(Copy);Destroy(Ended);Destroy(Single);Destroy(Still);}
 if(Frame==22){SceneManager.LoadScene(1);}
 }
}
'@|Set-Content ($folder+'/RuntimeAnimationDriver.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareAnimationPrefab {
 static AnimationClip Clip(string name,WrapMode mode,float length){var clip=new AnimationClip{name=name,legacy=true,frameRate=50,wrapMode=mode};var curve=AnimationCurve.Linear(0,0,length,1);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Animated Child",typeof(Transform),"m_LocalPosition.x"),curve);var scale=AnimationCurve.Linear(0,1,length,2);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("Animated Child",typeof(Transform),"m_LocalScale.y"),scale);AssetDatabase.CreateAsset(clip,"Assets/RuntimeAnimationPrefab/"+name+".anim");return clip;}
 static GameObject Make(string name,AnimationClip clip,bool active,bool enabled){var root=new GameObject(name);var child=new GameObject("Animated Child");child.transform.SetParent(root.transform,false);child.transform.localPosition=new Vector3(0,2,3);child.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectScopeFixture/Shared.asset");child.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectScopeFixture/Shared.mat");root.AddComponent<RuntimeAnimatedCounter>().Target=child;var animation=root.AddComponent<Animation>();animation.AddClip(clip,clip.name);animation.clip=clip;animation.playAutomatically=true;animation.cullingType=AnimationCullingType.AlwaysAnimate;animation.enabled=enabled;root.SetActive(active);var asset=PrefabUtility.SaveAsPrefabAsset(root,"Assets/RuntimeAnimationPrefab/"+name+".prefab");Object.DestroyImmediate(root);return asset;}
 public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var loop=Clip("PrefabLoop",WrapMode.Loop,.2f);var clamp=Clip("PrefabClamp",WrapMode.ClampForever,.06f);var once=Clip("PrefabOnce",WrapMode.Once,.06f);var driver=new GameObject("Animation Prefab Driver").AddComponent<RuntimeAnimationDriver>();driver.Loop=Make("AnimationLoop",loop,true,true);driver.Dormant=Make("AnimationDormant",loop,false,true);driver.Clamp=Make("AnimationClamp",clamp,true,true);driver.Once=Make("AnimationOnce",once,true,true);driver.Disabled=Make("AnimationDisabled",loop,true,false);MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(driver),-1000);EditorSceneManager.SaveScene(scene,"Assets/RuntimeAnimationPrefab/AnimationPrefab.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimeAnimationPrefab/AnimationPrefab.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_ANIMATION_PREFAB_FIXTURE_READY");}
}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareAnimationPrefab.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareAnimationPrefab.Run','-logFile',($Project+'/prepare-animation-prefab.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Animation prefab fixture failed'};Write-Output 'RUNTIME_ANIMATION_PREFAB_FIXTURE_READY'
