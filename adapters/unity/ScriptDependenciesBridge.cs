// Follow compiled component type references, including generic Add/GetComponent arguments.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEditor;
using UnityEngine;
public static partial class EngineMigratorBridge
{
    static readonly Dictionary<short,OpCode> ScriptOpcodes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).GroupBy(o=>o.Value).ToDictionary(g=>g.Key,g=>g.First());
    static void ExportScriptClosure(Type initial,EMPackage package)
    {
        var pending=new Queue<Type>();var seen=new HashSet<Type>();pending.Enqueue(initial);
        Action<Type> enqueue=t=>{if(t!=null&&t!=typeof(MonoBehaviour)&&typeof(MonoBehaviour).IsAssignableFrom(t)&&t.Namespace==null)pending.Enqueue(t);};
        var available=MonoImporter.GetAllRuntimeMonoScripts();
        while(pending.Count>0)
        {
            var type=pending.Dequeue();if(!seen.Add(type))continue;
            var script=available.FirstOrDefault(s=>s.GetClass()==type);if(!script)throw new Exception("참조된 MonoBehaviour 원본 스크립트 누락: "+type.FullName);
            const string orderWarning="명시적 Script Execution Order는 작성된 C++ 스크립트의 초기 Awake/OnEnable·Start 정렬, Update Tick 선행 조건과 Unity 복원 메타데이터에 반영됩니다. 동일 순위·임의 동적 생성·모든 콜백 조합의 동등성은 별도 검증 범위입니다.";
            if(MonoImporter.GetExecutionOrder(script)!=0&&!package.warnings.Contains(orderWarning))package.warnings.Add(orderWarning);
            var path=AssetDatabase.GetAssetPath(script);if(!path.StartsWith("Assets/",StringComparison.Ordinal))throw new Exception("외부 컴포넌트 의존성 미지원: "+path);
            if(!package.scripts.Any(s=>s.name==path))package.scripts.Add(ScriptDefinition(script,path));enqueue(type.BaseType);
            foreach(var field in type.GetFields(BindingFlags.DeclaredOnly|BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))enqueue(field.FieldType);
            foreach(var method in type.GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
            {
                enqueue(method.ReturnType);foreach(var parameter in method.GetParameters())enqueue(parameter.ParameterType);
                var body=method.GetMethodBody();if(body==null)continue;foreach(var local in body.LocalVariables)enqueue(local.LocalType);
                var il=body.GetILAsByteArray();int index=0;
                while(index<il.Length)
                {
                    short code=il[index++];if(code==0xfe)code=(short)(0xfe00|il[index++]);var op=ScriptOpcodes[code];int size;
                    switch(op.OperandType){case OperandType.InlineNone:size=0;break;case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:size=1;break;case OperandType.InlineVar:size=2;break;case OperandType.InlineI8:case OperandType.InlineR:size=8;break;case OperandType.InlineSwitch:size=4+4*BitConverter.ToInt32(il,index);break;default:size=4;break;}
                    if(op.OperandType==OperandType.InlineMethod||op.OperandType==OperandType.InlineField||op.OperandType==OperandType.InlineType||op.OperandType==OperandType.InlineTok)
                    {
                        var member=method.Module.ResolveMember(BitConverter.ToInt32(il,index));enqueue(member as Type);enqueue(member.DeclaringType);
                        var call=member as MethodInfo;if(call!=null&&call.IsGenericMethod)foreach(var argument in call.GetGenericArguments())enqueue(argument);
                        var field=member as FieldInfo;if(field!=null)enqueue(field.FieldType);
                    }
                    index+=size;
                }
            }
        }
    }
}
