param([string]$Project='artifacts/runtime-physics-prefab-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);if(!$Project.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
$folder=$Project+'/Assets/RuntimePhysicsPrefab';New-Item $folder -ItemType Directory -Force|Out-Null
@'
using UnityEngine;
public class RuntimePhysicsCounter:MonoBehaviour {public int Awakes;public int Enables;public int Starts;public int Updates;public int Disables;public GameObject Target;void Awake(){Awakes++;}void OnEnable(){Enables++;}void Start(){Starts++;}void Update(){Updates++;}void OnDisable(){Disables++;}}
'@|Set-Content ($folder+'/RuntimePhysicsCounter.cs')
@'
using UnityEngine;using UnityEngine.SceneManagement;
public class RuntimePhysicsDriver:MonoBehaviour {
 public int Frame;public int ReturnAwakes;public int ReturnStarts;public GameObject Box;public GameObject Sphere;public GameObject Capsule;public GameObject Dormant;
 public GameObject First;public GameObject Ball;public GameObject Pill;public GameObject Sleeping;public GameObject Second;public RuntimePhysicsCounter Counter;public RuntimePhysicsCounter SleepingCounter;public bool Linked;
 void Update(){Frame++;
 if(Frame==2){First=Instantiate(Box,new Vector3(0f,10f,0f),Quaternion.identity);Counter=First.GetComponent<RuntimePhysicsCounter>();ReturnAwakes=Counter.Awakes;ReturnStarts=Counter.Starts;}
 if(Frame==3){Ball=Instantiate(Sphere,new Vector3(5f,10f,0f),Quaternion.identity);}
 if(Frame==4){Pill=Instantiate(Capsule,new Vector3(-5f,7f,0f),Quaternion.identity);}
 if(Frame==5){Sleeping=Instantiate(Dormant,new Vector3(15f,10f,0f),Quaternion.identity);SleepingCounter=Sleeping.GetComponent<RuntimePhysicsCounter>();}
 if(Frame==6){Second=Instantiate(Box,new Vector3(10f,10f,0f),Quaternion.identity);}
 if(Frame==7){Sleeping.SetActive(true);}
 if(Frame==9){First.SetActive(false);}
 if(Frame==11){First.SetActive(true);}
 if(Counter!=null){var parent=Counter.Target.transform.parent;Linked=parent==First.transform;}
 if(Frame==14){Destroy(First);Destroy(Ball);Destroy(Pill);Destroy(Sleeping);Destroy(Second);}
 if(Frame==20){SceneManager.LoadScene(1);}
 }
}
'@|Set-Content ($folder+'/RuntimePhysicsDriver.cs')
@'
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPreparePhysicsPrefab {
 static GameObject Make(string name,string shape,bool kinematic,bool active){var root=new GameObject(name);root.transform.position=new Vector3(0,10,0);if(shape=="box")root.AddComponent<BoxCollider>();if(shape=="sphere")root.AddComponent<SphereCollider>();if(shape=="capsule")root.AddComponent<CapsuleCollider>();var body=root.AddComponent<Rigidbody>();body.mass=2;body.useGravity=!kinematic;body.isKinematic=kinematic;body.linearDamping=0;body.angularDamping=0;body.constraints=RigidbodyConstraints.FreezeRotation;root.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectScopeFixture/Shared.asset");root.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectScopeFixture/Shared.mat");var child=new GameObject(name+" Child");child.transform.SetParent(root.transform,false);child.transform.localPosition=new Vector3(0,1,0);root.AddComponent<RuntimePhysicsCounter>().Target=child;root.SetActive(active);var asset=PrefabUtility.SaveAsPrefabAsset(root,"Assets/RuntimePhysicsPrefab/"+name+".prefab");Object.DestroyImmediate(root);return asset;}
 public static void Run(){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var driver=new GameObject("Physics Prefab Driver").AddComponent<RuntimePhysicsDriver>();driver.Box=Make("PhysicsBox","box",false,true);driver.Sphere=Make("PhysicsSphere","sphere",false,true);driver.Capsule=Make("PhysicsCapsule","capsule",true,true);driver.Dormant=Make("PhysicsDormant","box",false,false);MonoImporter.SetExecutionOrder(MonoScript.FromMonoBehaviour(driver),-1000);EditorSceneManager.SaveScene(scene,"Assets/RuntimePhysicsPrefab/PhysicsPrefab.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/RuntimePhysicsPrefab/PhysicsPrefab.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeMiddle.unity",true),new EditorBuildSettingsScene("Assets/RuntimeWorld/RuntimeFinal.unity",true)};AssetDatabase.SaveAssets();Debug.Log("RUNTIME_PHYSICS_PREFAB_FIXTURE_READY");}
}
'@|Set-Content ($Project+'/Assets/Editor/EMPreparePhysicsPrefab.cs')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPreparePhysicsPrefab.Run','-logFile',($Project+'/prepare-physics-prefab.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Physics prefab fixture failed'};Write-Output 'RUNTIME_PHYSICS_PREFAB_FIXTURE_READY'
