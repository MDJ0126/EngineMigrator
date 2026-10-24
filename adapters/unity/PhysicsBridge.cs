// Editor adapter: primitive 3D physics, with explicit rejection of unmatched features.
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using EMUnityPhysicsMaterial=UnityEngine.PhysicsMaterial;
using EMUnityPhysicsCombine=UnityEngine.PhysicsMaterialCombine;
#else
using EMUnityPhysicsMaterial=UnityEngine.PhysicMaterial;
using EMUnityPhysicsCombine=UnityEngine.PhysicMaterialCombine;
#endif

[Serializable] public class EMPhysics {
 public string shape; public Vector3 center,size; public float radius,height;
 public int direction=1,constraints; public bool enabled=true,trigger,hasBody,kinematic,gravity=true;
 public float mass=1,linearDamping,angularDamping=0.05f; public string detection="Discrete",interpolation="None";
 public bool hasMaterialMapping,customMaterial;public string materialId,materialName,frictionCombine="Average",bounceCombine="Average";public float dynamicFriction=.6f,staticFriction=.6f,bounciness;
}
public partial class EMNode { public bool hasPhysics; public EMPhysics physics; }
public partial class EMPackage { public Vector3 gravity=new Vector3(0,-9.81f,0); }
public static partial class EngineMigratorBridge {
 static bool IsPhysicsComponent(Component c)=>c is BoxCollider||c is SphereCollider||c is CapsuleCollider||c is Rigidbody;
 static void ExportPhysics(GameObject go,EMNode n,EMPackage p) {
  var colliders=go.GetComponents<Collider>();var body=go.GetComponent<Rigidbody>();
  if(colliders.Length==0&&!body)return;
  if(colliders.Length!=1){p.blockers.Add(go.name+": 단일 기본 Collider가 필요합니다. 복합·Collider 없는 Rigidbody 미지원");return;}
  var c=colliders[0]; var v=new EMPhysics{enabled=c.enabled,trigger=c.isTrigger,hasBody=body!=null};n.hasPhysics=true;n.physics=v;
  if(go.layer!=0)p.blockers.Add(go.name+": 기본 Layer 0 외 물리 충돌 채널 미지원");
  if(Physics.GetIgnoreLayerCollision(0,0))p.blockers.Add(go.name+": Layer 0 자체 충돌 해제 미지원");
  if(go.GetComponent<Camera>()||go.GetComponent<Light>())p.blockers.Add(go.name+": Camera/Light와 Physics 동일 객체 조합 미지원");
  if(c is BoxCollider box){v.shape="box";v.center=box.center;v.size=box.size;}
  else if(c is SphereCollider sphere){v.shape="sphere";v.center=sphere.center;v.radius=sphere.radius;}
  else if(c is CapsuleCollider capsule){v.shape="capsule";v.center=capsule.center;v.radius=capsule.radius;v.height=capsule.height;v.direction=capsule.direction;}
  else {p.blockers.Add(go.name+": 기본 Box/Sphere/Capsule Collider 외 미지원");return;}
  var original=c.sharedMaterial;var material=c.material;
  v.hasMaterialMapping=true;v.customMaterial=original;v.dynamicFriction=material.dynamicFriction;v.staticFriction=material.staticFriction;v.bounciness=material.bounciness;v.frictionCombine=material.frictionCombine.ToString();v.bounceCombine=material.bounceCombine.ToString();
  if(original){if(!AssetDatabase.Contains(original))p.blockers.Add(go.name+": 영구 에셋이 아닌 물리 재질 미지원");else {var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(original));v.materialId=importer.userData.StartsWith("EngineMigratorSourceId=",StringComparison.Ordinal)?importer.userData.Substring("EngineMigratorSourceId=".Length):Id(original);v.materialName=original.name;if(!System.Text.RegularExpressions.Regex.IsMatch(v.materialId,"^[A-Za-z0-9_]+$"))throw new Exception("Invalid physical material identity");}}
  c.sharedMaterial=original;if(material!=original&&!AssetDatabase.Contains(material))UnityEngine.Object.DestroyImmediate(material);
  if(v.frictionCombine!="Average"||v.bounceCombine!="Average")p.blockers.Add(go.name+": Average 외 물리 재질 혼합 규칙 미지원");
  if(c.contactOffset!=0.01f)p.blockers.Add(go.name+": 기본값 외 contactOffset 미지원");
  if(go.GetComponentInParent<Rigidbody>(true)!=body)p.blockers.Add(go.name+": 부모 Rigidbody의 복합 충돌체 미지원");
  if(body){
   v.mass=body.mass;v.kinematic=body.isKinematic;v.gravity=body.useGravity;v.constraints=(int)body.constraints;
   var so=new SerializedObject(body);v.linearDamping=so.FindProperty("m_LinearDamping")?.floatValue??so.FindProperty("m_Drag").floatValue;v.angularDamping=so.FindProperty("m_AngularDamping")?.floatValue??so.FindProperty("m_AngularDrag").floatValue;
   v.detection=body.collisionDetectionMode.ToString();v.interpolation=body.interpolation.ToString();
   if(!body.automaticCenterOfMass||!body.automaticInertiaTensor)p.blockers.Add(go.name+": 수동 질량 중심·관성 텐서 미지원");
   if(body.transform.parent)p.blockers.Add(go.name+": 계층 Rigidbody의 독립 물리 변환 미지원");
   if(v.trigger&&!v.kinematic)p.blockers.Add(go.name+": 동적 trigger Rigidbody 미지원");
   if(!body.detectCollisions||(!c.enabled&&!body.isKinematic))p.blockers.Add(go.name+": 동적 충돌 감지/Collider 해제 상태 미지원");
   if(v.detection!="Discrete"&&v.detection!="ContinuousDynamic")p.blockers.Add(go.name+": 해당 연속 충돌 모드 미지원 "+v.detection);
   if(v.interpolation!="None")p.blockers.Add(go.name+": 보간 모드의 표시 동등성 미지원");
  }
  if(v.shape!="box"&&(Mathf.Abs(go.transform.lossyScale.x-go.transform.lossyScale.y)>1e-5||Mathf.Abs(go.transform.lossyScale.y-go.transform.lossyScale.z)>1e-5))p.blockers.Add(go.name+": Sphere/Capsule 비균일 스케일 미지원");
  if(v.shape=="capsule"&&body&&!v.kinematic&&v.direction!=1)p.blockers.Add(go.name+": 동적 Capsule의 X/Z 축 미지원");
  if(v.center!=Vector3.zero&&body&&!v.kinematic)p.blockers.Add(go.name+": 동적 Rigidbody의 0 외 Collider 중심 미지원");
 }
 static readonly System.Collections.Generic.Dictionary<string,EMUnityPhysicsMaterial> PhysicsMaterials=new System.Collections.Generic.Dictionary<string,EMUnityPhysicsMaterial>();
 static void ImportPhysics(GameObject go,EMNode n,string root){
  if(!n.hasPhysics)return;var v=n.physics;Collider c;
  if(v.shape=="box"){var x=go.AddComponent<BoxCollider>();x.center=v.center;x.size=v.size;c=x;}
  else if(v.shape=="sphere"){var x=go.AddComponent<SphereCollider>();x.center=v.center;x.radius=v.radius;c=x;}
  else if(v.shape=="capsule"){var x=go.AddComponent<CapsuleCollider>();x.center=v.center;x.radius=v.radius;x.height=v.height;x.direction=v.direction;c=x;}
  else throw new Exception("Unknown physics shape");
  c.enabled=v.enabled;c.isTrigger=v.trigger;
  if(v.customMaterial){if(!PhysicsMaterials.TryGetValue(v.materialId,out var material)){material=new EMUnityPhysicsMaterial(v.materialName){dynamicFriction=v.dynamicFriction,staticFriction=v.staticFriction,bounciness=v.bounciness,frictionCombine=(EMUnityPhysicsCombine)Enum.Parse(typeof(EMUnityPhysicsCombine),v.frictionCombine),bounceCombine=(EMUnityPhysicsCombine)Enum.Parse(typeof(EMUnityPhysicsCombine),v.bounceCombine)};var path=ResourceAssetPath(root,"PhysicsMaterials",v.materialId,v.materialName,".physicMaterial");AssetDatabase.CreateAsset(material,path);var importer=AssetImporter.GetAtPath(path);importer.userData="EngineMigratorSourceId="+v.materialId;importer.SaveAndReimport();PhysicsMaterials.Add(v.materialId,material);}c.sharedMaterial=material;}
  if(v.hasBody){var b=go.AddComponent<Rigidbody>();b.mass=v.mass;b.isKinematic=v.kinematic;b.useGravity=v.gravity;b.constraints=(RigidbodyConstraints)v.constraints;b.collisionDetectionMode=(CollisionDetectionMode)Enum.Parse(typeof(CollisionDetectionMode),v.detection);b.interpolation=(RigidbodyInterpolation)Enum.Parse(typeof(RigidbodyInterpolation),v.interpolation);var so=new SerializedObject(b);(so.FindProperty("m_LinearDamping")??so.FindProperty("m_Drag")).floatValue=v.linearDamping;(so.FindProperty("m_AngularDamping")??so.FindProperty("m_AngularDrag")).floatValue=v.angularDamping;so.ApplyModifiedPropertiesWithoutUndo();}
 }
 static void VerifyPhysics(GameObject go,EMNode n){if(!n.hasPhysics)return;var a=new EMNode();var p=new EMPackage();ExportPhysics(go,a,p);if(p.blockers.Count>0||JsonUtility.ToJson(n.physics)!=JsonUtility.ToJson(a.physics))throw new Exception("Physics mismatch after reload: "+go.name);}
}
