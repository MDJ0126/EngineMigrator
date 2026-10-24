// Editor adapter. Install into a working copy's Assets/Editor, not the original project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[Serializable] public class EMParameter { public string name,kind,value; public bool overrideState; }
[Serializable] public class EMEffect { public string type;public bool active;public List<EMParameter> parameters=new List<EMParameter>(); }
[Serializable] public class EMCamera { public bool enabled=true,orthographic,audioListener,hasAudioListener,postProcessing;public float fov,aspect,near,far,size,depth;public Color background;public string clearFlags,unityData,urpData; }
[Serializable] public class EMLight { public bool enabled=true,useTemperature;public string type,shadows,unityData,urpData;public Color color;public float intensity,range,spotAngle,innerSpotAngle,temperature,bounceIntensity,shadowStrength; }
[Serializable] public class EMVolume { public string profileId,profileName;public bool enabled=true,global;public float priority,weight,blendDistance;public List<EMEffect> effects=new List<EMEffect>(); }
[Serializable] public class EMEnvironment { public bool fog;public string fogMode,ambientMode,reflectionMode,skyShader,sun;public string[] skyKeywords;public Color fogColor,ambientSky,ambientEquator,ambientGround,ambientLight;public float fogDensity,fogStart,fogEnd,ambientIntensity,reflectionIntensity;public List<EMParameter> skyProperties=new List<EMParameter>(); }
public partial class EMNode { public string tag="Untagged";public int layer;public bool effectiveActive=true,hasCamera,hasLight,hasVolume;public EMCamera camera;public EMLight light;public EMVolume volume; }
public partial class EMPackage { public bool hasEnvironment;public EMEnvironment environment;public List<string> warnings=new List<string>(); }

public static partial class EngineMigratorBridge
{
    const string CameraData="UnityEngine.Rendering.Universal.UniversalAdditionalCameraData",LightData="UnityEngine.Rendering.Universal.UniversalAdditionalLightData",VolumeType="UnityEngine.Rendering.Volume";
    static Type FindType(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
    static object Get(object value,string name)
    {
        var property=value.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);if(property!=null)return property.GetValue(value,null);
        var field=value.GetType().GetField(name,BindingFlags.Public|BindingFlags.Instance);if(field!=null)return field.GetValue(value);
        throw new Exception("Missing reflected member "+value.GetType().FullName+"."+name);
    }
    static void Set(object value,string name,object data)
    {
        var property=value.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);if(property!=null&&property.CanWrite){property.SetValue(value,data,null);return;}
        var field=value.GetType().GetField(name,BindingFlags.Public|BindingFlags.Instance);if(field!=null){field.SetValue(value,data);return;}
        throw new Exception("Missing writable member "+name);
    }
    static Component FindComponent(GameObject go,string name)=>go.GetComponents<Component>().FirstOrDefault(c=>c&&c.GetType().FullName==name);
    static Component AddReflected(GameObject go,string name)
    {
        var type=FindType(name);if(type==null)throw new Exception("대상 프로젝트에 필요한 렌더링 패키지가 없습니다: "+name);
        return go.AddComponent(type);
    }
    static string UserComponentJson(Component value)
    {
        var json=EditorJsonUtility.ToJson(value);
        json=System.Text.RegularExpressions.Regex.Replace(json,"\\\"m_(GameObject|Script|CorrespondingSourceObject|PrefabInstance|PrefabAsset)\\\"\\s*:\\s*\\{[^{}]*\\}\\s*,?","");
        json=System.Text.RegularExpressions.Regex.Replace(json,"\\\"m_(ObjectHideFlags|EditorHideFlags)\\\"\\s*:\\s*-?[0-9]+\\s*,?","");
        json=System.Text.RegularExpressions.Regex.Replace(json,"\\\"m_(Name|EditorClassIdentifier)\\\"\\s*:\\s*\\\"[^\\\"]*\\\"\\s*,?","");return json;
    }
    static bool IsSceneComponentSupported(Component c)=>c&&(c is Transform||c is MeshFilter||c is MeshRenderer||c is Camera||c is Light||c is AudioListener||c is Animation||IsUIComponent(c)||IsPhysicsComponent(c)||IsUserBehaviour(c)||new[]{CameraData,LightData,VolumeType}.Contains(c.GetType().FullName));
    static EMParameter Parameter(string name,object value,bool overridden=false)
    {
        var p=new EMParameter{name=name,overrideState=overridden};
        if(value==null||(value is Object unityObject&&!unityObject)){p.kind="null";p.value="";}
        else if(value.GetType().IsEnum){p.kind="enum";p.value=Convert.ToInt64(value,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);}
        else if(value is bool){p.kind="bool";p.value=(bool)value?"true":"false";}
        else if(value is float||value is double||value is int){p.kind=value is int?"int":"float";p.value=Convert.ToDouble(value,CultureInfo.InvariantCulture).ToString("R",CultureInfo.InvariantCulture);}
        else if(value is Color){p.kind="color";p.value=JsonUtility.ToJson((Color)value);}
        else if(value is Vector2){p.kind="vector2";p.value=JsonUtility.ToJson((Vector2)value);}
        else if(value is Vector3){p.kind="vector3";p.value=JsonUtility.ToJson((Vector3)value);}
        else if(value is Quaternion){p.kind="quaternion";p.value=JsonUtility.ToJson((Quaternion)value);}
        else if(value is Vector4){p.kind="vector4";p.value=JsonUtility.ToJson((Vector4)value);}
        else throw new Exception("후처리 값 유형 미지원: "+name+" "+value.GetType().FullName);
        return p;
    }
    static object Value(EMParameter p,Type type)
    {
        switch(p.kind){case "null":return null;case "bool":return bool.Parse(p.value);case "enum":return Enum.ToObject(type,long.Parse(p.value,CultureInfo.InvariantCulture));case "int":return int.Parse(p.value,CultureInfo.InvariantCulture);case "float":return Convert.ChangeType(double.Parse(p.value,CultureInfo.InvariantCulture),type,CultureInfo.InvariantCulture);case "color":return JsonUtility.FromJson<Color>(p.value);case "vector2":return JsonUtility.FromJson<Vector2>(p.value);case "vector3":return JsonUtility.FromJson<Vector3>(p.value);case "quaternion":return JsonUtility.FromJson<Quaternion>(p.value);case "vector4":return JsonUtility.FromJson<Vector4>(p.value);default:throw new Exception("Unknown parameter kind "+p.kind);}
    }
    static bool ParameterEqual(EMParameter a,EMParameter b)
    {
        if(a.kind!=b.kind||a.name!=b.name||a.overrideState!=b.overrideState)return false;
        if(a.kind=="float"){var x=double.Parse(a.value,CultureInfo.InvariantCulture);var y=double.Parse(b.value,CultureInfo.InvariantCulture);return Math.Abs(x-y)<=0.000001*Math.Max(1,Math.Max(Math.Abs(x),Math.Abs(y)));}
        if(a.kind=="color"){var x=JsonUtility.FromJson<Color>(a.value);var y=JsonUtility.FromJson<Color>(b.value);return Mathf.Max(Mathf.Abs(x.r-y.r),Mathf.Abs(x.g-y.g),Mathf.Abs(x.b-y.b),Mathf.Abs(x.a-y.a))<0.000001f;}
        if(a.kind=="vector2")return (JsonUtility.FromJson<Vector2>(a.value)-JsonUtility.FromJson<Vector2>(b.value)).sqrMagnitude<0.000000000001f;
        if(a.kind=="vector3")return (JsonUtility.FromJson<Vector3>(a.value)-JsonUtility.FromJson<Vector3>(b.value)).sqrMagnitude<0.000000000001f;
        if(a.kind=="quaternion"){var x=JsonUtility.FromJson<Quaternion>(a.value);var y=JsonUtility.FromJson<Quaternion>(b.value);return new Vector4(x.x-y.x,x.y-y.y,x.z-y.z,x.w-y.w).sqrMagnitude<0.000000000001f;}
        if(a.kind=="vector4")return (JsonUtility.FromJson<Vector4>(a.value)-JsonUtility.FromJson<Vector4>(b.value)).sqrMagnitude<0.000000000001f;
        return a.value==b.value;
    }
    static void ExportSceneComponents(GameObject go,EMNode node,EMPackage p)
    {
        node.hasCamera=go.GetComponent<Camera>();node.hasLight=go.GetComponent<Light>();node.hasVolume=FindComponent(go,VolumeType)!=null;node.tag=go.tag;node.layer=go.layer;node.effectiveActive=go.activeInHierarchy;
        if(node.layer!=0)p.blockers.Add(go.name+": 사용자 레이어 매핑 미구현");
        var camera=go.GetComponent<Camera>();var light=go.GetComponent<Light>();var volume=FindComponent(go,VolumeType);
        if(new[]{camera!=null,light!=null,volume!=null,go.GetComponent<MeshRenderer>()!=null}.Count(v=>v)>1)p.blockers.Add(go.name+": 한 객체의 복합 렌더링 컴포넌트 매핑 미구현");
        if(go.GetComponent<AudioListener>()&&!camera)p.blockers.Add(go.name+": 카메라 없는 AudioListener 매핑 미구현");
        if(camera)
        {
            if(camera.targetTexture||camera.rect!=new Rect(0,0,1,1)||camera.usePhysicalProperties||camera.cullingMask!=-1)p.blockers.Add(go.name+": 렌더 타깃/분할 화면/물리 카메라/커스텀 마스크 미구현");
            var extra=FindComponent(go,CameraData);
            if(extra!=null){if(Convert.ToInt32(Get(extra,"renderType"),CultureInfo.InvariantCulture)!=0)p.blockers.Add(go.name+": URP 오버레이 카메라 미구현");var trigger=Get(extra,"volumeTrigger") as Object;if(trigger)p.blockers.Add(go.name+": 볼륨 트리거 참조 미구현");if(((LayerMask)Get(extra,"volumeLayerMask")).value!=1)p.blockers.Add(go.name+": 사용자 볼륨 레이어 마스크 미구현");}
            if(extra!=null){var stack=(IList)Get(extra,"cameraStack");if(stack!=null&&stack.Count>0)p.blockers.Add(go.name+": URP 카메라 스택 미구현");}
            node.camera=new EMCamera{enabled=camera.enabled,orthographic=camera.orthographic,fov=camera.fieldOfView,aspect=camera.aspect,near=camera.nearClipPlane,far=camera.farClipPlane,size=camera.orthographicSize,depth=camera.depth,background=camera.backgroundColor,clearFlags=camera.clearFlags.ToString(),hasAudioListener=go.GetComponent<AudioListener>(),audioListener=go.GetComponent<AudioListener>()&&go.GetComponent<AudioListener>().enabled,postProcessing=extra!=null&&(bool)Get(extra,"renderPostProcessing"),unityData=UserComponentJson(camera),urpData=extra==null?null:UserComponentJson(extra)};
            p.warnings.Add(go.name+": 원근 원거리 클립·클리어 방식 및 URP 전용 카메라 옵션의 Unreal 렌더링 동등성은 미검증입니다. 원본 설정은 왕복용 메타데이터에 보존합니다.");
        }
        if(light)
        {
            if(light.type!=LightType.Directional)p.blockers.Add(go.name+": 현재 방향광만 네이티브 매핑합니다.");
            if(light.cookie||light.cullingMask!=-1)p.blockers.Add(go.name+": 라이트 쿠키/사용자 컬링 마스크 미구현");
            var extra=FindComponent(go,LightData);
            node.light=new EMLight{enabled=light.enabled,type=light.type.ToString(),color=light.color,intensity=light.intensity,range=light.range,spotAngle=light.spotAngle,innerSpotAngle=light.innerSpotAngle,shadows=light.shadows.ToString(),shadowStrength=light.shadowStrength,bounceIntensity=light.bounceIntensity,useTemperature=light.useColorTemperature,temperature=light.colorTemperature,unityData=UserComponentJson(light),urpData=extra==null?null:UserComponentJson(extra)};
            p.warnings.Add(go.name+": 광도 수치는 전달하지만 Unity 비물리 단위와 Unreal lux의 외형 동등성 및 그림자 필터 품질은 미검증입니다.");
        }
        if(volume)
        {
            var profile=(ScriptableObject)Get(volume,"sharedProfile");if(!profile){p.blockers.Add(go.name+": 볼륨 프로파일 없음");return;}
            node.volume=new EMVolume{enabled=((Behaviour)volume).enabled,global=(bool)Get(volume,"isGlobal"),priority=(float)Get(volume,"priority"),weight=(float)Get(volume,"weight"),blendDistance=(float)Get(volume,"blendDistance"),profileId=Id(profile),profileName=profile.name};
            if(!node.volume.global)p.blockers.Add(go.name+": 로컬 볼륨 경계 변환 미구현");
            foreach(var effect in (IList)Get(profile,"components"))
            {
                var type=effect.GetType();var data=new EMEffect{type=type.FullName,active=(bool)Get(effect,"active")};
                if(!new[]{"Bloom","Vignette","Tonemapping","MotionBlur"}.Contains(type.Name))p.blockers.Add(go.name+": 후처리 효과 미지원 "+type.FullName);
                foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))
                {
                    var value=field.GetValue(effect);if(value==null||value.GetType().GetProperty("overrideState")==null)continue;
                    try{data.parameters.Add(Parameter(field.Name,Get(value,"value"),(bool)Get(value,"overrideState")));}catch(Exception e){p.blockers.Add(go.name+": "+e.Message);}
                }
                node.volume.effects.Add(data);
            }
            p.warnings.Add(go.name+": Bloom/Vignette/MotionBlur의 대응 수치를 실제 Unreal 후처리에 연결합니다. 커널·Neutral/ACES 톤매핑·HDR 디스플레이 세부 값은 렌더링이 동일하지 않을 수 있으며 원본 값을 왕복 메타데이터에 보존합니다.");
        }
    }
    static void ImportSceneComponents(GameObject go,EMNode n,EMPackage package,string root)
    {
        go.layer=n.layer;if(!string.IsNullOrEmpty(n.tag))go.tag=n.tag;
        if(n.hasCamera)
        {
            var c=n.camera;var camera=go.AddComponent<Camera>();if(!string.IsNullOrEmpty(c.unityData))EditorJsonUtility.FromJsonOverwrite(c.unityData,camera);camera.enabled=c.enabled;camera.orthographic=c.orthographic;camera.fieldOfView=c.fov;camera.aspect=c.aspect;camera.nearClipPlane=c.near;camera.farClipPlane=c.far;camera.orthographicSize=c.size;camera.depth=c.depth;camera.backgroundColor=c.background;camera.clearFlags=(CameraClearFlags)Enum.Parse(typeof(CameraClearFlags),c.clearFlags);
            if(c.hasAudioListener||c.audioListener)go.AddComponent<AudioListener>().enabled=c.audioListener;
            if(!string.IsNullOrEmpty(c.urpData)){var extra=FindComponent(go,CameraData)??AddReflected(go,CameraData);EditorJsonUtility.FromJsonOverwrite(c.urpData,extra);Set(extra,"renderPostProcessing",c.postProcessing);}
        }
        if(n.hasLight)
        {
            var l=n.light;var light=go.AddComponent<Light>();if(!string.IsNullOrEmpty(l.unityData))EditorJsonUtility.FromJsonOverwrite(l.unityData,light);light.enabled=l.enabled;light.type=(LightType)Enum.Parse(typeof(LightType),l.type);light.color=l.color;light.intensity=l.intensity;light.range=l.range;light.spotAngle=l.spotAngle;light.innerSpotAngle=l.innerSpotAngle;light.shadows=(LightShadows)Enum.Parse(typeof(LightShadows),l.shadows);light.shadowStrength=l.shadowStrength;light.bounceIntensity=l.bounceIntensity;light.useColorTemperature=l.useTemperature;light.colorTemperature=l.temperature;
            if(!string.IsNullOrEmpty(l.urpData)){var extra=FindComponent(go,LightData)??AddReflected(go,LightData);EditorJsonUtility.FromJsonOverwrite(l.urpData,extra);}
        }
        if(n.hasVolume)
        {
            var v=n.volume;var volume=AddReflected(go,VolumeType);((Behaviour)volume).enabled=v.enabled;Set(volume,"isGlobal",v.global);Set(volume,"priority",v.priority);Set(volume,"weight",v.weight);Set(volume,"blendDistance",v.blendDistance);
            if(!System.Text.RegularExpressions.Regex.IsMatch(v.profileId,"^[A-Za-z0-9_]+$"))throw new Exception("Unsafe volume profile identifier");
            if(string.IsNullOrWhiteSpace(v.profileName)||v.profileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())>=0||v.profileName=="."||v.profileName=="..")throw new Exception("Unsafe volume profile name");
            var directory=root+"/Profiles/"+v.profileId;System.IO.Directory.CreateDirectory(directory);AssetDatabase.Refresh();
            var profilePath=directory+"/"+v.profileName+".asset";
            var profile=AssetDatabase.LoadAssetAtPath<ScriptableObject>(profilePath);
            if(ProjectImportMode&&ProjectProfiles.TryGetValue(v.profileId,out var sharedProfile)){Set(volume,"sharedProfile",sharedProfile);return;}
            if(profile){Set(volume,"sharedProfile",profile);return;}
            profile=ScriptableObject.CreateInstance(FindType("UnityEngine.Rendering.VolumeProfile"));profile.name=v.profileName;AssetDatabase.CreateAsset(profile,profilePath);
            foreach(var data in v.effects)
            {
                var type=FindType(data.type);if(type==null)throw new Exception("볼륨 효과 유형 없음 "+data.type);var effect=ScriptableObject.CreateInstance(type);Set(effect,"active",data.active);
                foreach(var parameter in data.parameters){var value=Get(effect,parameter.name);var property=value.GetType().GetProperty("value");Set(value,"value",Value(parameter,property.PropertyType));Set(value,"overrideState",parameter.overrideState);}
                ((IList)Get(profile,"components")).Add(effect);AssetDatabase.AddObjectToAsset(effect,profile);
            }
            Set(volume,"sharedProfile",profile);EditorUtility.SetDirty(profile);if(ProjectImportMode)ProjectProfiles[v.profileId]=profile;
        }
    }
    static void VerifySceneComponents(GameObject go,EMNode expected,EMPackage package)
    {
        var read=new EMNode();var p=new EMPackage();ExportSceneComponents(go,read,p);
        if(go.tag!=expected.tag||go.layer!=expected.layer)throw new Exception("Scene tag/layer mismatch");
        if(expected.hasCamera){var a=expected.camera;var b=read.camera;if(b==null||a.enabled!=b.enabled||a.orthographic!=b.orthographic||Math.Abs(a.fov-b.fov)>0.0001||Math.Abs(a.near-b.near)>0.0001||Math.Abs(a.far-b.far)>0.01||Math.Abs(a.aspect-b.aspect)>0.0001||a.audioListener!=b.audioListener)throw new Exception("Camera property mismatch after reload");}
        if(expected.hasLight){var a=expected.light;var b=read.light;if(b==null||a.enabled!=b.enabled||a.type!=b.type||Math.Abs(a.intensity-b.intensity)>0.0001||Math.Abs(a.temperature-b.temperature)>0.01||a.useTemperature!=b.useTemperature||a.shadows!=b.shadows)throw new Exception("Light property mismatch after reload");}
        if(expected.hasVolume){var a=expected.volume;var b=read.volume;if(b==null||a.profileName!=b.profileName||a.enabled!=b.enabled||a.global!=b.global||Math.Abs(a.priority-b.priority)>0.0001||Math.Abs(a.weight-b.weight)>0.0001||a.effects.Count!=b.effects.Count)throw new Exception("Volume property mismatch");foreach(var effect in a.effects){var other=b.effects.Single(e=>e.type==effect.type);if(effect.active!=other.active)throw new Exception("Volume active mismatch");foreach(var parameter in effect.parameters){var actual=other.parameters.Single(x=>x.name==parameter.name);if(!ParameterEqual(parameter,actual))throw new Exception("Volume parameter mismatch "+effect.type+"."+parameter.name);}}}
    }
    static EMEnvironment ExportEnvironment(Dictionary<Transform,string> nodeIds,EMPackage package)
    {
        var e=new EMEnvironment{fog=RenderSettings.fog,fogColor=RenderSettings.fogColor,fogMode=RenderSettings.fogMode.ToString(),fogDensity=RenderSettings.fogDensity,fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance,ambientMode=RenderSettings.ambientMode.ToString(),ambientSky=RenderSettings.ambientSkyColor,ambientEquator=RenderSettings.ambientEquatorColor,ambientGround=RenderSettings.ambientGroundColor,ambientLight=RenderSettings.ambientLight,ambientIntensity=RenderSettings.ambientIntensity,reflectionIntensity=RenderSettings.reflectionIntensity,reflectionMode=RenderSettings.defaultReflectionMode.ToString(),sun=RenderSettings.sun&&nodeIds.ContainsKey(RenderSettings.sun.transform)?nodeIds[RenderSettings.sun.transform]:null};
        if(e.fog)package.blockers.Add("장면 안개 렌더링 변환 미구현");
        var material=RenderSettings.skybox;if(material){e.skyShader=material.shader.name;e.skyKeywords=material.shaderKeywords.OrderBy(k=>k).ToArray();if(e.skyShader!="Skybox/Procedural")package.blockers.Add("현재 절차적 기본 하늘만 환경 매핑합니다: "+e.skyShader);for(var i=0;i<ShaderUtil.GetPropertyCount(material.shader);i++){var name=ShaderUtil.GetPropertyName(material.shader,i);var type=ShaderUtil.GetPropertyType(material.shader,i);if(type==ShaderUtil.ShaderPropertyType.Color)e.skyProperties.Add(Parameter(name,material.GetColor(name)));else if(type==ShaderUtil.ShaderPropertyType.Float||type==ShaderUtil.ShaderPropertyType.Range)e.skyProperties.Add(Parameter(name,material.GetFloat(name)));else package.blockers.Add("하늘 셰이더 속성 미지원 "+name);}}
        package.warnings.Add("하늘·환경광은 실제 Unreal SkyAtmosphere/SkyLight에 대응하지만 Unity 절차적 하늘과 물리 렌더링 동등성은 미검증입니다. 원본 파라미터는 왕복용으로 보존합니다.");return e;
    }
    static void ImportEnvironment(EMEnvironment e,Dictionary<string,GameObject> nodes,string root)
    {
        if(e==null)return;RenderSettings.fog=e.fog;RenderSettings.fogColor=e.fogColor;RenderSettings.fogMode=(FogMode)Enum.Parse(typeof(FogMode),e.fogMode);RenderSettings.fogDensity=e.fogDensity;RenderSettings.fogStartDistance=e.fogStart;RenderSettings.fogEndDistance=e.fogEnd;
        RenderSettings.ambientMode=(UnityEngine.Rendering.AmbientMode)Enum.Parse(typeof(UnityEngine.Rendering.AmbientMode),e.ambientMode);RenderSettings.ambientSkyColor=e.ambientSky;RenderSettings.ambientEquatorColor=e.ambientEquator;RenderSettings.ambientGroundColor=e.ambientGround;RenderSettings.ambientLight=e.ambientLight;RenderSettings.ambientIntensity=e.ambientIntensity;RenderSettings.reflectionIntensity=e.reflectionIntensity;RenderSettings.defaultReflectionMode=(UnityEngine.Rendering.DefaultReflectionMode)Enum.Parse(typeof(UnityEngine.Rendering.DefaultReflectionMode),e.reflectionMode);
        if(!string.IsNullOrEmpty(e.skyShader)){var shader=Shader.Find(e.skyShader);if(!shader)throw new Exception("Sky shader missing");var material=new Material(shader);foreach(var p in e.skyProperties)if(p.kind=="color")material.SetColor(p.name,(Color)Value(p,typeof(Color)));else material.SetFloat(p.name,(float)Value(p,typeof(float)));if(e.skyKeywords!=null)material.shaderKeywords=e.skyKeywords;AssetDatabase.CreateAsset(material,root+"/EnvironmentSky.mat");RenderSettings.skybox=material;}else RenderSettings.skybox=null;
        RenderSettings.sun=string.IsNullOrEmpty(e.sun)?null:nodes[e.sun].GetComponent<Light>();
    }
    static void VerifyEnvironment(EMEnvironment expected,Dictionary<string,GameObject> nodes)
    {
        var ids=nodes.ToDictionary(p=>p.Value.transform,p=>p.Key);var actual=ExportEnvironment(ids,new EMPackage());
        var expectedOther=JsonUtility.FromJson<EMEnvironment>(JsonUtility.ToJson(expected));var actualOther=JsonUtility.FromJson<EMEnvironment>(JsonUtility.ToJson(actual));expectedOther.skyProperties.Clear();actualOther.skyProperties.Clear();if(expected.skyKeywords==null)actualOther.skyKeywords=null;
        var matches=JsonUtility.ToJson(expectedOther)==JsonUtility.ToJson(actualOther)&&expected.skyProperties.Count==actual.skyProperties.Count&&expected.skyProperties.All(p=>actual.skyProperties.Any(v=>ParameterEqual(p,v)));
        if(!matches){System.IO.File.WriteAllText(System.IO.Path.Combine(PackageRoot,"environment-expected.json"),JsonUtility.ToJson(expected,true));System.IO.File.WriteAllText(System.IO.Path.Combine(PackageRoot,"environment-actual.json"),JsonUtility.ToJson(actual,true));throw new Exception("Environment/sky settings mismatch after reload");}
    }
}


