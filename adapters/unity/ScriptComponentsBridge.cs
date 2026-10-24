// Editor adapter. Install into a working copy's Assets/Editor, not the original project.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[Serializable] public class EMBehaviour { public string id,className;public bool enabled;public List<EMParameter> fields=new List<EMParameter>(); }
[Serializable] public class EMScript { public string name,source,guid;public int executionOrder; }
public partial class EMNode { public List<EMBehaviour> behaviours=new List<EMBehaviour>(); }
public partial class EMPackage { public List<EMScript> scripts=new List<EMScript>(); }
public static partial class EngineMigratorBridge
{
    static EMScript ScriptDefinition(MonoScript script,string path)=>new EMScript{name=path,source=script.text,guid=AssetDatabase.AssetPathToGUID(path),executionOrder=MonoImporter.GetExecutionOrder(script)};
    static bool IsUserBehaviour(Component c)=>c is MonoBehaviour&&MonoScript.FromMonoBehaviour((MonoBehaviour)c)&&AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour((MonoBehaviour)c)).StartsWith("Assets/",StringComparison.Ordinal)&&c.GetType().Namespace==null;
    static IEnumerable<FieldInfo> SerializedFields(Type type)
    {
        for(var current=type;current!=typeof(MonoBehaviour);current=current.BaseType)
            foreach(var field in current.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly))
                if(!field.IsNotSerialized&&(field.IsPublic||field.IsDefined(typeof(SerializeField),true)))yield return field;
    }
    static void ExportBehaviours(GameObject go,EMNode node,EMPackage package,Dictionary<Transform,string> nodeIds)
    {
        foreach(var component in go.GetComponents<MonoBehaviour>().Where(c=>c&&IsUserBehaviour(c)))
        {
            var type=component.GetType();var behaviour=new EMBehaviour{id=node.id+"_behaviour_"+node.behaviours.Count,className=type.Name,enabled=component.enabled};
            ExportScriptClosure(type,package);
            for(var current=type;current!=typeof(MonoBehaviour);current=current.BaseType)
            {
                var script=MonoImporter.GetAllRuntimeMonoScripts().FirstOrDefault(s=>s.GetClass()==current);
                if(!script){package.blockers.Add("컴포넌트 원본 스크립트 누락: "+current.FullName);continue;}
                var path=AssetDatabase.GetAssetPath(script);
                if(!package.scripts.Any(s=>s.name==path))package.scripts.Add(ScriptDefinition(script,path));
            }
            foreach(var field in SerializedFields(type))
            {
                if(field.IsNotSerialized)continue;
                if(field.FieldType==typeof(Canvas)||field.FieldType==typeof(UnityEngine.UI.Button)){var target=field.GetValue(component) as Component;string identity="";if(target&&!nodeIds.TryGetValue(target.transform,out identity)){package.blockers.Add(go.name+": 장면 외부 UI 컴포넌트 참조 미지원: "+field.Name);continue;}behaviour.fields.Add(new EMParameter{name=field.Name,kind="node",value=identity});continue;}
                if(typeof(MonoBehaviour).IsAssignableFrom(field.FieldType)){
                    var target=(MonoBehaviour)field.GetValue(component);string identity="";
                    if(target){string owner;if(!IsUserBehaviour(target)||!nodeIds.TryGetValue(target.transform,out owner)){package.blockers.Add(go.name+": 장면 외부 컴포넌트 참조 미지원: "+field.Name);continue;}var index=Array.IndexOf(target.GetComponents<MonoBehaviour>().Where(c=>c&&IsUserBehaviour(c)).ToArray(),target);if(index<0)throw new Exception("컴포넌트 참조 순서 누락");identity=owner+"_behaviour_"+index;}
                    behaviour.fields.Add(new EMParameter{name=field.Name,kind="component",value=identity});continue;
                }
                if(field.FieldType==typeof(GameObject)||field.FieldType==typeof(Transform))
                {
                    var transformField=field.FieldType==typeof(Transform);var transformValue=transformField?(Transform)field.GetValue(component):null;var target=transformField?(transformValue?transformValue.gameObject:null):(GameObject)field.GetValue(component);string id="";
                    if(target&&!nodeIds.TryGetValue(target.transform,out id)){var path=AssetDatabase.GetAssetPath(target);if(!transformField&&AssetDatabase.Contains(target)&&path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)&&AssetDatabase.LoadAssetAtPath<GameObject>(path)==target&&PrefabUtility.GetPrefabAssetType(target)==PrefabAssetType.Regular){behaviour.fields.Add(new EMParameter{name=field.Name,kind="prefab",value=ProjectAssetId(path,"p_")});continue;}package.blockers.Add(go.name+": 장면 외부 GameObject 참조 미지원: "+field.Name);continue;}
                    behaviour.fields.Add(new EMParameter{name=field.Name,kind=transformField?"transform":"node",value=id});continue;
                }
                if(field.FieldType!=typeof(float)&&field.FieldType!=typeof(double)&&field.FieldType!=typeof(bool)&&field.FieldType!=typeof(int)&&field.FieldType!=typeof(Vector3)&&field.FieldType!=typeof(Quaternion)){package.blockers.Add(go.name+": 직렬화 필드 미지원 "+field.Name+" "+field.FieldType);continue;}
                behaviour.fields.Add(Parameter(field.Name,field.GetValue(component)));
            }
            node.behaviours.Add(behaviour);
            if(!package.warnings.Contains("MonoBehaviour는 원래 객체와 연결된 Actor Blueprint 또는 C++ Runtime Actor로 변환됩니다. 지원하는 상태·상속·좌표 API만 처리하며 임의 엔진 API와 전체 게임 포팅은 미완료입니다."))package.warnings.Add("MonoBehaviour는 원래 객체와 연결된 Actor Blueprint 또는 C++ Runtime Actor로 변환됩니다. 지원하는 상태·상속·좌표 API만 처리하며 임의 엔진 API와 전체 게임 포팅은 미완료입니다.");
        }
    }
    static void ImportBehaviours(GameObject go,EMNode node,Dictionary<string,GameObject> nodes)
    {
        foreach(var source in node.behaviours)
        {
            var type=FindType(source.className);if(type==null||!typeof(MonoBehaviour).IsAssignableFrom(type))throw new Exception("복원 스크립트 클래스 누락: "+source.className);
            var component=(MonoBehaviour)go.AddComponent(type);component.enabled=source.enabled;
            foreach(var parameter in source.fields){var field=SerializedFields(type).SingleOrDefault(f=>f.Name==parameter.name);if(field==null)throw new Exception("복원 필드 누락: "+parameter.name);if(parameter.kind=="component"){PendingComponentFields.Add(()=>field.SetValue(component,BehaviourObject(parameter,nodes,field.FieldType)));continue;}field.SetValue(component,parameter.kind is "node" or "prefab" or "transform"?BehaviourObject(parameter,nodes,field.FieldType):Value(parameter,field.FieldType));}
        }
    }
    static void VerifyBehaviours(GameObject go,EMNode node,Dictionary<string,GameObject> nodes)
    {
        var components=go.GetComponents<MonoBehaviour>().Where(c=>c&&IsUserBehaviour(c)).ToArray();
        if(components.Length!=node.behaviours.Count)throw new Exception("저장 후 스크립트 컴포넌트 수 불일치");
        for(var i=0;i<components.Length;i++)
        {
            var actual=components[i];var source=node.behaviours[i];if(actual.GetType().Name!=source.className||actual.enabled!=source.enabled)throw new Exception("스크립트 부착/활성 상태 불일치");
            foreach(var expected in source.fields){var field=SerializedFields(actual.GetType()).SingleOrDefault(f=>f.Name==expected.name);if(field==null)throw new Exception("스크립트 필드 누락: "+expected.name);if(expected.kind is "node" or "prefab" or "transform" or "component"){if((UnityEngine.Object)field.GetValue(actual)!=(UnityEngine.Object)BehaviourObject(expected,nodes,field.FieldType))throw new Exception("스크립트 객체 참조 불일치: "+expected.name);}else if(!ParameterEqual(expected,Parameter(expected.name,field.GetValue(actual))))throw new Exception("스크립트 직렬화 상태 불일치: "+expected.name);}
        }
    }
    static readonly List<Action> PendingComponentFields=new List<Action>();
    static object BehaviourObject(EMParameter parameter,Dictionary<string,GameObject> nodes,Type expectedType=null){if(string.IsNullOrEmpty(parameter.value))return null;if(parameter.kind=="node"&&(expectedType==typeof(Canvas)||expectedType==typeof(UnityEngine.UI.Button))){var component=nodes[parameter.value].GetComponent(expectedType);if(!component)throw new Exception("UI 컴포넌트 참조 누락: "+parameter.name);return component;}if(parameter.kind=="component"){var marker=parameter.value.LastIndexOf("_behaviour_",StringComparison.Ordinal);int index;if(marker<0||!int.TryParse(parameter.value.Substring(marker+11),out index))throw new Exception("컴포넌트 참조 ID 형식 오류");var owner=nodes[parameter.value.Substring(0,marker)];return owner.GetComponents<MonoBehaviour>().Where(c=>c&&IsUserBehaviour(c)).ElementAt(index);}if(parameter.kind=="transform")return nodes[parameter.value].transform;if(parameter.kind=="node")return nodes[parameter.value];if(!ProjectPrefabs.TryGetValue(parameter.value,out var path))throw new Exception("런타임 프리팹 정의 누락: "+parameter.value);return AssetDatabase.LoadAssetAtPath<GameObject>(path);}
}

