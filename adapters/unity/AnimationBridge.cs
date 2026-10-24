using System;using System.Collections.Generic;using System.Linq;using UnityEditor;using UnityEngine;
[Serializable] public class EMAnimationKey {public float time,value,inTangent,outTangent;public string mode;}
[Serializable] public class EMAnimationCurve {public string target,path,property;public List<EMAnimationKey> keys=new List<EMAnimationKey>();}
[Serializable] public class EMAnimationClip {public string id,name,wrap;public float frameRate,length;public List<EMAnimationCurve> curves=new List<EMAnimationCurve>();}
[Serializable] public class EMAnimation {public string defaultClip;public bool enabled=true,autoPlay;public List<EMAnimationClip> clips=new List<EMAnimationClip>();}
public partial class EMNode {public bool hasAnimation;public EMAnimation animation;}
public static partial class EngineMigratorBridge {
 static string ClipId(AnimationClip clip){var imp=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));const string prefix="EngineMigratorSourceId=";var value=imp&&imp.userData.StartsWith(prefix,StringComparison.Ordinal)?imp.userData.Substring(prefix.Length):Id(clip);if(!System.Text.RegularExpressions.Regex.IsMatch(value,"^[A-Za-z0-9_]+$"))throw new Exception("Invalid persisted animation identity");return value;}
 static void ExportAnimation(GameObject go,EMNode n,EMPackage p,Dictionary<Transform,string> ids){
  var a=go.GetComponent<Animation>();if(!a)return;n.hasAnimation=true;n.animation=new EMAnimation{enabled=a.enabled,autoPlay=a.playAutomatically};
  if(a.animatePhysics||a.cullingType!=AnimationCullingType.AlwaysAnimate)p.blockers.Add(go.name+": Animation 물리 업데이트·화면 컬링 미지원");
  var clips=AnimationUtility.GetAnimationClips(go).Distinct().ToArray();foreach(var clip in clips){
   var data=new EMAnimationClip{id=ClipId(clip),name=clip.name,frameRate=clip.frameRate,length=clip.length,wrap=clip.wrapMode==WrapMode.Once?"Once":clip.wrapMode.ToString()};n.animation.clips.Add(data);if(a.clip==clip)n.animation.defaultClip=data.id;
   if(!clip.legacy||AnimationUtility.GetAnimationEvents(clip).Length!=0)p.blockers.Add(clip.name+": Legacy Transform clip 외·AnimationEvent 미지원");
   if(clip.wrapMode!=WrapMode.Loop&&clip.wrapMode!=WrapMode.Once&&clip.wrapMode!=WrapMode.Default&&clip.wrapMode!=WrapMode.ClampForever)p.blockers.Add(clip.name+": 재생 wrap 모드 미지원");
   foreach(var binding in AnimationUtility.GetCurveBindings(clip)){
    var target=string.IsNullOrEmpty(binding.path)?go.transform:go.transform.Find(binding.path);
    if(binding.type!=typeof(Transform)||!new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"}.Contains(binding.propertyName)||!target||!ids.ContainsKey(target)){p.blockers.Add(clip.name+": 지원하지 않는 애니메이션 바인딩 "+binding.path+"/"+binding.propertyName);continue;}
    if(target.GetComponent<Rigidbody>()){p.blockers.Add(clip.name+": 물리와 동일 Transform 동시 제어 미지원");continue;}
    var curve=AnimationUtility.GetEditorCurve(clip,binding);var c=new EMAnimationCurve{target=ids[target],path=binding.path,property=binding.propertyName};data.curves.Add(c);
    for(int i=0;i<curve.length;i++){var key=curve.keys[i];var left=AnimationUtility.GetKeyLeftTangentMode(curve,i);var right=AnimationUtility.GetKeyRightTangentMode(curve,i);if(key.weightedMode!=WeightedMode.None)p.blockers.Add(clip.name+": 가중 접선 미지원");c.keys.Add(new EMAnimationKey{time=key.time,value=key.value,inTangent=float.IsInfinity(key.inTangent)?0:key.inTangent,outTangent=float.IsInfinity(key.outTangent)?0:key.outTangent,mode=right==AnimationUtility.TangentMode.Constant?"constant":right==AnimationUtility.TangentMode.Linear&&left==AnimationUtility.TangentMode.Linear?"linear":"cubic"});}
   }
   if(AnimationUtility.GetObjectReferenceCurveBindings(clip).Length>0)p.blockers.Add(clip.name+": 객체 교체 애니메이션 미지원");
  }
  if(a.playAutomatically&&a.clip==null)p.blockers.Add(go.name+": 자동 재생 기본 clip 누락");
 }
 static void ImportAnimation(GameObject go,EMNode n,string root){
  if(!n.hasAnimation)return;var a=go.AddComponent<Animation>();a.enabled=n.animation.enabled;a.playAutomatically=n.animation.autoPlay;a.cullingType=AnimationCullingType.AlwaysAnimate;
  foreach(var data in n.animation.clips){var clip=new AnimationClip{name=data.name,legacy=true,frameRate=data.frameRate,wrapMode=(WrapMode)Enum.Parse(typeof(WrapMode),data.wrap)};
   foreach(var c in data.curves){var keys=c.keys.Select(k=>new Keyframe(k.time,k.value,k.inTangent,k.outTangent)).ToArray();var curve=new AnimationCurve(keys);for(int i=0;i<c.keys.Count;i++){var mode=c.keys[i].mode=="constant"?AnimationUtility.TangentMode.Constant:c.keys[i].mode=="linear"?AnimationUtility.TangentMode.Linear:AnimationUtility.TangentMode.Free;AnimationUtility.SetKeyBroken(curve,i,true);AnimationUtility.SetKeyLeftTangentMode(curve,i,mode);AnimationUtility.SetKeyRightTangentMode(curve,i,mode);}AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(c.path,typeof(Transform),c.property),curve);}
   var path=ResourceAssetPath(root,"Animations",n.id+"_"+data.id,data.name,".anim");AssetDatabase.CreateAsset(clip,path);var importer=AssetImporter.GetAtPath(path);importer.userData="EngineMigratorSourceId="+data.id;importer.SaveAndReimport();a.AddClip(clip,data.name);if(data.id==n.animation.defaultClip)a.clip=clip;
  }
 }
 static void VerifyAnimation(GameObject go,EMNode n,Dictionary<Transform,string> ids){
  if(!n.hasAnimation)return;var read=new EMNode();var p=new EMPackage();ExportAnimation(go,read,p,ids);var a=n.animation;var b=read.animation;
  if(p.blockers.Count>0||b==null||a.enabled!=b.enabled||a.autoPlay!=b.autoPlay||a.defaultClip!=b.defaultClip||a.clips.Count!=b.clips.Count)throw new Exception("Animation component/reference mismatch after reload");
  foreach(var clip in a.clips){var other=b.clips.Single(c=>c.id==clip.id);if(clip.name!=other.name||clip.wrap!=other.wrap||Mathf.Abs(clip.frameRate-other.frameRate)>1e-5||Mathf.Abs(clip.length-other.length)>1e-5||clip.curves.Count!=other.curves.Count)throw new Exception("Animation clip mismatch");foreach(var curve in clip.curves){var actual=other.curves.Single(c=>c.path==curve.path&&c.property==curve.property&&c.target==curve.target);if(curve.keys.Count!=actual.keys.Count)throw new Exception("Animation key count mismatch");for(int i=0;i<curve.keys.Count;i++){var x=curve.keys[i];var y=actual.keys[i];if(x.mode!=y.mode||Mathf.Abs(x.time-y.time)>2e-6||Mathf.Abs(x.value-y.value)>1e-5||Mathf.Abs(x.inTangent-y.inTangent)>1e-5||Mathf.Abs(x.outTangent-y.outTangent)>1e-5)throw new Exception("Animation channel/tangent mismatch after reload");}}}
 }
}
