using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineMigrator.Core;

public sealed record ActorFile(string Name,string Code);
public static class ActorScripts
{
    private sealed record MemberInfo(string Name,string Type,string Access,string Flags,string Parameters);
    private static readonly string[] Events=["Awake","OnEnable","Start","FixedUpdate","Update","LateUpdate","OnDisable","OnDestroy"];
    private static readonly HashSet<string> Types=["float","double","bool","int","GameObject","Transform","Quaternion","Vector3","Canvas","Button","void"];
    private static readonly HashSet<string> UiTypes=["Canvas","Button"];
    private static string CppType(string type,bool field=false)=>type=="int"?"int32":type=="Vector3"?"FVector3f":type=="Quaternion"?"FVector4f":type=="Transform"?(field?"TObjectPtr<AActor>":"EM_TransformRef"):type=="GameObject"?(field?"TObjectPtr<AActor>":"AActor*"):UiTypes.Contains(type)?(field?"TObjectPtr<AActor>":"EM_UIRef_"+type):Types.Contains(type)?type:field?"TObjectPtr<AActor>":"EM_ComponentRef_"+N(type);
    private const string LegacyIntHelpers="protected:\nstatic int32 EM_IntAdd(int32 A,int32 B) { uint32 Bits=uint32(A)+uint32(B); int32 Value; FMemory::Memcpy(&Value,&Bits,sizeof(Value)); return Value; }\nstatic int32 EM_IntSub(int32 A,int32 B) { uint32 Bits=uint32(A)-uint32(B); int32 Value; FMemory::Memcpy(&Value,&Bits,sizeof(Value)); return Value; }\nstatic int32 EM_IntMul(int32 A,int32 B) { uint32 Bits=uint32(A)*uint32(B); int32 Value; FMemory::Memcpy(&Value,&Bits,sizeof(Value)); return Value; }\nstatic int32 EM_IntNeg(int32 A) { return EM_IntSub(0,A); }\n";
    private const string FinalizeHelper="bool EM_Initialized = false;\nbool EM_Finalized = false;\nvoid EM_FinalizeDestroy() { if (EM_Initialized && !EM_Finalized) { EM_Finalized = true; EM_OnDestroy(); } }\n";
    private const string LegacyEndPlayWrapper="virtual void EndPlay(const EEndPlayReason::Type Reason) override { EM_FinalizeDestroy(); Super::EndPlay(Reason); }\n";
    internal const string ValidationStub=ActorValidationSurface.Source;
    private const string LegacyTransformHelpers="protected:\nAActor* EM_TransformOwner() const { auto* EM_Owner=GetAttachParentActor(); return EM_Owner?EM_Owner:const_cast<AActor*>(static_cast<const AActor*>(this)); }\nFVector3f EM_GetPosition() const { const auto V=EM_TransformOwner()->GetActorLocation(); return FVector3f(float(V.Y/100.0),float(V.Z/100.0),float(V.X/100.0)); }\nvoid EM_SetPosition(FVector3f Value) { EM_TransformOwner()->SetActorLocation(FVector(Value.Z*100.0,Value.X*100.0,Value.Y*100.0)); }\nFVector3f EM_GetLocalPosition() const { const auto V=EM_TransformOwner()->GetRootComponent()->GetRelativeLocation(); return FVector3f(float(V.Y/100.0),float(V.Z/100.0),float(V.X/100.0)); }\nvoid EM_SetLocalPosition(FVector3f Value) { EM_TransformOwner()->SetActorRelativeLocation(FVector(Value.Z*100.0,Value.X*100.0,Value.Y*100.0)); }\n";
    private static string SafeHelperNames(string code)=>Regex.Replace(code,@"\b(A|B|Bits|Copy|Value|V|Reason)\b",m=>"EM_"+m.Value);
    private static readonly string EndPlayWrapper=SafeHelperNames(LegacyEndPlayWrapper);
    private static readonly string PreviousIntHelpers=SafeHelperNames(LegacyIntHelpers);
    // MSVC 14.44 /O2 misoptimizes a memcpy-based wrapped induction variable as
    // unsigned. Preserve the defined bit copy while blocking that range folding.
    private static readonly string IntHelpers=SafeHelperNames(LegacyIntHelpers.Replace("uint32 Bits=","volatile uint32 Bits=").Replace("int32 Value; FMemory::Memcpy(&Value,&Bits,sizeof(Value));","const uint32 Copy=Bits; int32 Value; FMemory::Memcpy(&Value,&Copy,sizeof(Value));"));
    private static readonly string TransformHelpers=SafeHelperNames(LegacyTransformHelpers);
    private const string BitHelpers="""
protected:
static int32 EM_IntFromBits(uint32 EM_Bits) { int32 EM_Value; volatile uint32 EM_Stored=EM_Bits; const uint32 EM_Copy=EM_Stored; FMemory::Memcpy(&EM_Value,&EM_Copy,sizeof(EM_Value)); return EM_Value; }
static int32 EM_IntAnd(int32 EM_A,int32 EM_B) { return EM_IntFromBits(uint32(EM_A)&uint32(EM_B)); }
static int32 EM_IntOr(int32 EM_A,int32 EM_B) { return EM_IntFromBits(uint32(EM_A)|uint32(EM_B)); }
static int32 EM_IntXor(int32 EM_A,int32 EM_B) { return EM_IntFromBits(uint32(EM_A)^uint32(EM_B)); }
static int32 EM_IntNot(int32 EM_A) { return EM_IntFromBits(~uint32(EM_A)); }
static int32 EM_IntShiftLeft(int32 EM_A,int32 EM_B) { return EM_IntFromBits(uint32(EM_A)<<(uint32(EM_B)&31u)); }
static int32 EM_IntShiftRight(int32 EM_A,int32 EM_B) { const uint32 EM_Count=uint32(EM_B)&31u; uint32 EM_Bits=uint32(EM_A)>>EM_Count; if(EM_A<0&&EM_Count!=0)EM_Bits|=~uint32(0)<<(32u-EM_Count); return EM_IntFromBits(EM_Bits); }

""";
    private static string? BitOperation(string op)=>op switch {"&"=>"And","|"=>"Or","^"=>"Xor","<<"=>"ShiftLeft",">>"=>"ShiftRight",_=>null};
    private const string ReferenceValueHelper="static AActor* EM_ReferenceValue(AActor* EM_Value) { return EM_Value; }\n";
    private const string LegacyObjectHelper="protected:\nstatic bool EM_ObjectValid(const AActor* Value) { return IsValid(Value); }\n";
    private static readonly string ObjectHelper=SafeHelperNames(LegacyObjectHelper);
    private const string TransformAdditions="""
protected:
using EM_TransformRef=AActor*;
static EM_TransformRef EM_ObjectTransform(AActor* EM_Value){return EM_Value;}
EM_TransformRef EM_SelfTransform() const {return EM_TransformOwner();}
EM_TransformRef EM_GetParent() const {return EM_TransformOwner()->GetAttachParentActor();}
FVector4f EM_GetRotation() const {const FQuat EM_Q=EM_TransformOwner()->GetActorQuat();return FVector4f(EM_Q.Y,EM_Q.Z,EM_Q.X,EM_Q.W);}
FVector4f EM_GetLocalRotation() const {const FQuat EM_Q=EM_TransformOwner()->GetRootComponent()->GetRelativeRotation().Quaternion();return FVector4f(EM_Q.Y,EM_Q.Z,EM_Q.X,EM_Q.W);}

""";
    private static string N(string value,bool prefixed=false)
    {
        var reserved=new[]{"DeltaSeconds","Super","Tick","BeginPlay","Destroyed","PostInitializeComponents","RootComponent","PrimaryActorTick","CreateDefaultSubobject","template","friend","typename","unsigned","signed","union","mutable","delete","nullptr","auto","const","extern","namespace"};
        if(!Regex.IsMatch(value,"^[A-Za-z][A-Za-z0-9_]*$")||SyntaxFacts.GetKeywordKind(value)!=SyntaxKind.None||(reserved.Contains(value)&&!(prefixed&&new[]{"template","friend","typename","unsigned","signed","union","mutable","delete","auto"}.Contains(value)))||value.StartsWith("EM_",StringComparison.Ordinal))throw new NotSupportedException("예약어/충돌 식별자 미지원: "+value);
        return value;
    }
    private static string NativeField(string name)=>name=="Owner"?"EM_Field_Owner":name;
    private static string Type(TypeSyntax type,bool allowVoid=false) => Types.Contains(type.ToString())&&(allowVoid||type.ToString()!="void")?type.ToString():throw new NotSupportedException("Actor 필드/인수 타입 미지원: "+type);

    public static IReadOnlyList<ActorFile> FromUnity(string source)=>FromUnityCore(source,false,null,true);
    // Internal override keeps both legacy and weak-reference fixtures replayable.
    internal static IReadOnlyList<ActorFile> FromUnity(string source,bool useWeakGameObjects)=>FromUnityCore(source,useWeakGameObjects,null);
    internal static IReadOnlyList<ActorFile> FromUnityComponentsForValidation(string source)=>FromUnityCore(source,true,null,false,true);
    private static IReadOnlyList<ActorFile> FromUnityCore(string source,bool defaultWeak,IReadOnlySet<string>? weakClasses,bool autoWeak=false,bool defaultWeakComponents=false,IReadOnlySet<string>? weakComponentClasses=null,bool defaultWeakTransforms=false,IReadOnlySet<string>? weakTransformClasses=null,bool defaultWeakUI=false,IReadOnlySet<string>? weakUIClasses=null,IReadOnlySet<string>? uiOwnerNameClasses=null)
    {
        var tree=CSharpSyntaxTree.ParseText(source);var root=tree.GetCompilationUnitRoot();
        if(root.ContainsDirectives||root.Usings.Any(u=>u.ToString() is not ("using UnityEngine;" or "using UnityEngine.SceneManagement;" or "using UnityEngine.UI;"))||root.Members.Any(m=>m is not ClassDeclarationSyntax))throw new NotSupportedException("UnityEngine using 및 최상위 컴포넌트 클래스만 지원합니다.");
        var compilation=CSharpCompilation.Create("ActorValidation",[tree,CSharpSyntaxTree.ParseText(ValidationStub)],[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors=compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();if(errors.Length>0)throw new NotSupportedException(string.Join("\n",errors.Select(e=>e.ToString())));
        var semantic=compilation.GetSemanticModel(tree);var classes=root.Members.Cast<ClassDeclarationSyntax>().ToArray();var names=classes.Select(c=>N(c.Identifier.ValueText)).ToHashSet();
        if(autoWeak){
            var comparisons=classes.SelectMany(c=>c.DescendantNodes().OfType<BinaryExpressionSyntax>()).Where(b=>(b.IsKind(SyntaxKind.EqualsExpression)||b.IsKind(SyntaxKind.NotEqualsExpression))&&!IsNullReference(b.Left)&&!IsNullReference(b.Right)).ToArray();
            defaultWeakComponents=comparisons.Any(b=>names.Contains(semantic.GetTypeInfo(b.Left).Type?.Name??"")&&names.Contains(semantic.GetTypeInfo(b.Right).Type?.Name??""));
            defaultWeakTransforms=comparisons.Any(b=>semantic.GetTypeInfo(b.Left).Type?.ToDisplayString()=="UnityEngine.Transform"&&semantic.GetTypeInfo(b.Right).Type?.ToDisplayString()=="UnityEngine.Transform");
            defaultWeakUI=classes.SelectMany(c=>c.DescendantNodes().OfType<IdentifierNameSyntax>()).Any(id=>semantic.GetTypeInfo(id).Type?.ToDisplayString() is "UnityEngine.Canvas" or "UnityEngine.UI.Button");
            defaultWeak=defaultWeakUI||defaultWeakTransforms||defaultWeakComponents||comparisons.Any(b=>semantic.GetTypeInfo(b.Left).Type?.ToDisplayString()=="UnityEngine.GameObject"&&semantic.GetTypeInfo(b.Right).Type?.ToDisplayString()=="UnityEngine.GameObject");
        }
        var useWeakGameObjects=defaultWeak;
        var useWeakComponents=defaultWeakComponents;
        var useWeakTransforms=defaultWeakTransforms;
        var useWeakUI=defaultWeakUI;
        var useUiOwnerNames=true;
        string CppType(string value,bool field=false)=>useWeakUI&&UiTypes.Contains(value)?(field?"TWeakObjectPtr<AActor>":"EM_UIRef_"+value):useWeakTransforms&&value=="Transform"?(field?"TWeakObjectPtr<AActor>":"EM_TransformRef"):useWeakComponents&&names.Contains(value)?(field?"TWeakObjectPtr<AActor>":"EM_ComponentRef_"+N(value)):useWeakGameObjects&&value=="GameObject"?"TWeakObjectPtr<AActor>":ActorScripts.CppType(value,field);
        bool WeakParameter(string value)=>(useWeakUI&&UiTypes.Contains(value))||(useWeakTransforms&&value=="Transform")||(useWeakGameObjects&&value=="GameObject")||(useWeakComponents&&names.Contains(value));
        string SignatureType(string value)=>WeakParameter(value)?"TWeakObjectPtr<AActor>":CppType(value);
        if(names.Count!=classes.Length||classes.Length==0)throw new NotSupportedException("클래스 이름 중복/누락");
        string Type(TypeSyntax value,bool allowVoid=false)=>names.Contains(value.ToString())?value.ToString():ActorScripts.Type(value,allowVoid);
        string LocalType(TypeSyntax value){
            if(!value.IsVar)return Type(value);var inferred=semantic.GetTypeInfo(value).Type??throw new NotSupportedException("지역 변수 타입을 확인할 수 없습니다.");
            var name=inferred.SpecialType switch{SpecialType.System_Int32=>"int",SpecialType.System_Single=>"float",SpecialType.System_Double=>"double",SpecialType.System_Boolean=>"bool",_=>inferred.Name};
            return Types.Contains(name)||names.Contains(name)?name:throw new NotSupportedException("추론된 지역 변수 타입 미지원: "+inferred);
        }
        var lateUpdate=classes.Any(c=>c.Members.OfType<MethodDeclarationSyntax>().Any(m=>m.Identifier.ValueText=="LateUpdate"));
        var timeScale=classes.SelectMany(c=>c.DescendantNodes().OfType<MemberAccessExpressionSyntax>()).Any(m=>m.Expression.ToString()=="Time"&&m.Name.ToString()=="timeScale");
        var fixedUpdate=classes.Any(c=>c.Members.OfType<MethodDeclarationSyntax>().Any(m=>m.Identifier.ValueText=="FixedUpdate"))||classes.SelectMany(c=>c.DescendantNodes().OfType<MemberAccessExpressionSyntax>()).Any(m=>m.Expression.ToString()=="Time"&&m.Name.ToString()=="fixedDeltaTime");
        var uiNames=classes.SelectMany(c=>c.DescendantNodes().OfType<AssignmentExpressionSyntax>()).Any(a=>a.Left is MemberAccessExpressionSyntax m&&m.Name.ToString()=="name"&&UiTypes.Contains(semantic.GetTypeInfo(m.Expression).Type?.Name??""));
        var dynamicUI=classes.SelectMany(c=>c.DescendantNodes().OfType<InvocationExpressionSyntax>()).Any(c=>semantic.GetSymbolInfo(c).Symbol is IMethodSymbol {Name:"AddComponent",IsGenericMethod:true} m&&UiTypes.Contains(m.TypeArguments.Single().Name));
        var files=new List<ActorFile>();var runtime=true;
        foreach(var cls in classes)
        {
            var className=N(cls.Identifier.ValueText);
            useWeakGameObjects=weakClasses?.Contains(className)??defaultWeak;
            useWeakComponents=weakComponentClasses?.Contains(className)??defaultWeakComponents;
            useWeakTransforms=weakTransformClasses?.Contains(className)??defaultWeakTransforms;
            useWeakUI=weakUIClasses?.Contains(className)??defaultWeakUI;
            useUiOwnerNames=uiOwnerNameClasses?.Contains(className)??true;
            if(cls.AttributeLists.Count>0||cls.TypeParameterList is not null||!cls.Modifiers.Any(SyntaxKind.PublicKeyword)||cls.Modifiers.Any(m=>!m.IsKind(SyntaxKind.PublicKeyword)))throw new NotSupportedException("public 비제네릭 클래스만 지원합니다.");
            if(cls.BaseList?.Types.Count!=1)throw new NotSupportedException("단일 상속이 필요합니다.");
            var parent=cls.BaseList.Types[0].Type.ToString();if(parent!="MonoBehaviour"&&!names.Contains(parent))throw new NotSupportedException("외부/엔진 상속 미지원: "+parent);
            var actorMetadata=useWeakComponents?JsonSerializer.Serialize(new{name=className,parent,weakGameObjects=true,weakComponents=true}):useWeakGameObjects?JsonSerializer.Serialize(new{name=className,parent,weakGameObjects=true}):JsonSerializer.Serialize(new{name=className,parent});
            if(useWeakTransforms)actorMetadata=actorMetadata[..^1]+",\"weakTransforms\":true}";
            if(useWeakUI)actorMetadata=actorMetadata[..^1]+",\"weakUI\":true}";
            if(!lateUpdate)actorMetadata=actorMetadata[..^1]+",\"startQueue\":1}";
            if(lateUpdate)actorMetadata=actorMetadata[..^1]+",\"lateUpdate\":true,\"lateClock\":3}";
            if(timeScale)actorMetadata=actorMetadata[..^1]+",\"timeScale\":true,\"timeScaleClock\":2}";
            if(fixedUpdate)actorMetadata=actorMetadata[..^1]+",\"fixedUpdate\":true,\"fixedClock\":5}";
            if(uiNames&&useUiOwnerNames)actorMetadata=actorMetadata[..^1]+",\"uiOwnerNames\":true}";
            var text=new System.Text.StringBuilder("// EM_ACTOR "+actorMetadata+"\n#pragma once\n#include \"CoreMinimal.h\"\n#include \"Components/SceneComponent.h\"\n#include \""+(parent=="MonoBehaviour"?"GameFramework/Actor.h":parent+".h")+"\"\n#include \""+className+".generated.h\"\nUCLASS()\nclass A"+className+" : public "+(parent=="MonoBehaviour"?"AActor":"A"+parent)+"\n{\nGENERATED_BODY()\n");
            if(parent=="MonoBehaviour")
            {
                if(fixedUpdate)text.Append(FixedUpdatePorting.CurrentHelpers(lateUpdate));
                if(lateUpdate)text.Append(LateUpdatePorting.Helpers);else text.Append(StartQueuePorting.Helpers);
                if(timeScale)text.Append(TimeScalePorting.Helpers);
                if(uiNames&&useUiOwnerNames)text.Append(UiNamePorting.Helpers);
                foreach(var componentName in names.OrderBy(v=>v))text.Append("using EM_ComponentRef_"+componentName+"="+(useWeakComponents?"TWeakObjectPtr<AActor>":"AActor*")+";\n");
                if(runtime){text.Replace("#pragma once\n","#pragma once\n"+RuntimeObjectPorting.Includes+RuntimeComponentPorting.Includes+RuntimePhysicsPrefabPorting.Includes+RuntimeAnimationPrefabPorting.Includes);text.Append(useWeakUI?(dynamicUI?DynamicUiPorting.RuntimeHelpers:DynamicUiPorting.PreviousHelpers):UiComponentPorting.RuntimeHelpers).Append(ObjectActivationPorting.CreationHelpers).Append(ObjectActivationPorting.ComponentHelpers).Append(RuntimeUiPrefabPorting.Helpers(className));}
                if(classes.SelectMany(c=>c.DescendantNodes().OfType<PredefinedTypeSyntax>()).Any(t=>t.Keyword.IsKind(SyntaxKind.IntKeyword))||classes.SelectMany(c=>c.DescendantNodes().OfType<VariableDeclarationSyntax>()).Any(v=>v.Type.IsVar&&semantic.GetTypeInfo(v.Type).Type?.SpecialType==SpecialType.System_Int32))text.Append(IntHelpers);
                if(runtime||classes.SelectMany(c=>c.DescendantNodes().OfType<IdentifierNameSyntax>()).Any(t=>t.Identifier.ValueText is "GameObject" or "gameObject" or "Transform"))text.Append(ObjectHelper);
                if(useWeakGameObjects)text.Append(WeakGameObjectPorting.Helpers);
                if(runtime||classes.SelectMany(c=>c.DescendantNodes().OfType<IdentifierNameSyntax>()).Any(t=>t.Identifier.ValueText is "Vector3" or "transform" or "gameObject" or "Transform" or "Quaternion"))text.Append(TransformHelpers).Append(useWeakTransforms?TransformAdditions.Replace("using EM_TransformRef=AActor*;","using EM_TransformRef=TWeakObjectPtr<AActor>;",StringComparison.Ordinal):TransformAdditions);
                if(classes.Any(c=>c.DescendantNodes().Any(n=>n is BinaryExpressionSyntax bit&&BitOperation(bit.OperatorToken.Text) is not null||n is PrefixUnaryExpressionSyntax unary&&unary.IsKind(SyntaxKind.BitwiseNotExpression)||n is AssignmentExpressionSyntax assignment&&BitOperation(assignment.OperatorToken.Text.TrimEnd('=')) is not null)))text.Append(BitHelpers);
                if(classes.SelectMany(c=>c.DescendantNodes().OfType<ConditionalExpressionSyntax>()).Any(c=>(semantic.GetTypeInfo(c).Type??semantic.GetTypeInfo(c).ConvertedType) is { } t&&(t.Name is "GameObject" or "Transform"||UiTypes.Contains(t.Name)||names.Contains(t.Name))))text.Append(ReferenceValueHelper);
                if(classes.SelectMany(c=>c.DescendantNodes().OfType<InvocationExpressionSyntax>()).Any(c=>semantic.GetSymbolInfo(c).Symbol is IMethodSymbol m&&m.ContainingType.ToDisplayString()=="UnityEngine.SceneManagement.SceneManager"&&c.ArgumentList.Arguments.Count==1&&semantic.GetTypeInfo(c.ArgumentList.Arguments[0].Expression).Type?.SpecialType==SpecialType.System_Int32&&!semantic.GetConstantValue(c.ArgumentList.Arguments[0].Expression).HasValue))text.Append(RuntimeObjectPorting.SceneIndexHelper);
                text.Append("public:\nA"+className+"() { PrimaryActorTick.bCanEverTick = true; PrimaryActorTick.TickGroup = TG_PostPhysics; RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT(\"EM_Root\")); }\nprotected:\n"+FinalizeHelper+"virtual void PostInitializeComponents() override { Super::PostInitializeComponents(); EM_Initialized = true; EM_Awake(); }\nvirtual void BeginPlay() override { Super::BeginPlay(); EM_Start(); }\nvirtual void Tick(float DeltaSeconds) override { Super::Tick(DeltaSeconds); EM_Update(DeltaSeconds); }\nvirtual void Destroyed() override { EM_FinalizeDestroy(); Super::Destroyed(); }\n"+EndPlayWrapper);
                foreach(var ev in Events.Where(ev=>(ev!="FixedUpdate"||fixedUpdate)&&(ev!="LateUpdate"||lateUpdate)).Where(ev=>!cls.Members.OfType<MethodDeclarationSyntax>().Any(m=>m.Identifier.ValueText==ev)))text.Append("virtual void EM_"+ev+"("+(ev is "Update" or "FixedUpdate" or "LateUpdate"?"float DeltaSeconds":"")+") {}\n");
            }
            foreach(var member in cls.Members)
            {
                if(member is FieldDeclarationSyntax field)
                {
                    var attributes=field.AttributeLists.SelectMany(a=>a.Attributes).ToArray();var serialized=attributes.Any(a=>a.Name.ToString()=="SerializeField");
                    if(attributes.Any(a=>a.Name.ToString()!="SerializeField"||a.ArgumentList is not null)||field.Modifiers.Any(m=>!new[]{"public","protected","private"}.Contains(m.Text)))throw new NotSupportedException("정적/readonly/미지원 속성 필드");
                    var type=Type(field.Declaration.Type);var access=Access(field.Modifiers);
                    foreach(var v in field.Declaration.Variables)
                    {
                        var name=N(v.Identifier.ValueText);var value=v.Initializer is null?(type=="bool"?"false":type is "GameObject" or "Transform"||UiTypes.Contains(type)||names.Contains(type)?"nullptr":type=="Quaternion"?"FVector4f(0,0,0,0)":type=="Vector3"?"FVector3f::ZeroVector":type=="int"?"0":type=="float"?"0.0f":"0.0"):Expression(v.Initializer.Value,null);
                        if(v.Initializer is not null&&!semantic.GetConstantValue(v.Initializer.Value).HasValue&&!(type is "Vector3" or "Quaternion"&&v.Initializer.Value is ObjectCreationExpressionSyntax creation&&creation.ArgumentList!.Arguments.All(a=>semantic.GetConstantValue(a.Expression).HasValue))&&!(type=="Vector3"&&v.Initializer.Value is MemberAccessExpressionSyntax{Expression:IdentifierNameSyntax{Identifier.ValueText:"Vector3"}})&&!(type=="Quaternion"&&v.Initializer.Value.ToString()=="Quaternion.identity"))throw new NotSupportedException("필드 초기값은 상수/벡터 상수만 지원합니다.");
                        text.Append("// EM_FIELD "+JsonSerializer.Serialize(new MemberInfo(name,type,access,serialized?"SerializeField":"",""))+"\n"+access+":\nUPROPERTY("+(access=="public"||serialized?"EditAnywhere, Category=\"EngineMigrator\""+(serialized&&access!="public"?", meta=(AllowPrivateAccess=\"true\")":""):"")+")\n"+CppType(type,true)+" "+NativeField(name)+" = "+value+";\n");
                    }
                    continue;
                }
                if(member is not MethodDeclarationSyntax method)throw new NotSupportedException("생성자·프로퍼티·이벤트 선언·중첩 타입 미지원");
                var methodName=N(method.Identifier.ValueText);var isEvent=Events.Contains(methodName);var returnType=Type(method.ReturnType,true);
                if((methodName.StartsWith("On",StringComparison.Ordinal)&&!Events.Contains(methodName))||new[]{"Reset"}.Contains(methodName))throw new NotSupportedException("해당 Unity 메시지 대응은 아직 미구현입니다: "+methodName);
                if(method.AttributeLists.Count>0||method.TypeParameterList is not null||method.Modifiers.Any(m=>!new[]{"public","protected","private","virtual","override"}.Contains(m.Text)))throw new NotSupportedException("메서드 한정자/속성 미지원");
                if(isEvent&&(returnType!="void"||method.ParameterList.Parameters.Count>0))throw new NotSupportedException("생명주기는 인수 없는 void 함수여야 합니다.");
                foreach(var p in method.ParameterList.Parameters)if(p.Type is null||p.Modifiers.Count>0||p.Default is not null||p.AttributeLists.Count>0)throw new NotSupportedException("ref/out/default 인수 미지원");else{Type(p.Type);N(p.Identifier.ValueText,true);}
                var args=string.Join(", ",method.ParameterList.Parameters.Select(p=>p.Type+" "+p.Identifier.ValueText));
                var cppArgs=string.Join(", ",method.ParameterList.Parameters.Select(p=>WeakParameter(p.Type!.ToString())?"const "+SignatureType(p.Type!.ToString())+"& EM_in_"+p.Identifier.ValueText:SignatureType(p.Type!.ToString())+" EM_arg_"+p.Identifier.ValueText));
                var flags=method.Modifiers.Any(SyntaxKind.OverrideKeyword)?"override":method.Modifiers.Any(SyntaxKind.VirtualKeyword)?"virtual":"";
                var info=new MemberInfo(methodName,returnType,Access(method.Modifiers),flags,args);
                var body=method.Body is not null?Statement(method.Body,methodName):method.ExpressionBody is not null?"{ "+(returnType=="void"?"":"return ")+Expression(method.ExpressionBody.Expression,methodName)+"; }":throw new NotSupportedException("빈 메서드 미지원");
                if(useWeakGameObjects||useWeakComponents)body=body.Insert(1,WeakGameObjectPorting.ParameterCopyPrefix(method.ParameterList.Parameters.Where(p=>WeakParameter(p.Type!.ToString())).Select(p=>p.Identifier.ValueText)));
                var reflection=!isEvent&&flags!="override"&&info.Access=="public"&&(!names.Contains(returnType)||useWeakComponents)&&(!UiTypes.Contains(returnType)||useWeakUI)&&returnType is not ("Vector3" or "Quaternion")&&(returnType!="Transform"||useWeakTransforms)&&!method.ParameterList.Parameters.Any(p=>(UiTypes.Contains(p.Type!.ToString())&&!useWeakUI)||p.Type!.ToString() is "Vector3" or "Quaternion"||(p.Type!.ToString()=="Transform"&&!useWeakTransforms)||(!useWeakComponents&&names.Contains(p.Type!.ToString())))?"UFUNCTION(BlueprintCallable, Category=\"EngineMigrator\")\n":"";
                if(reflection.Length>0&&(WeakParameter(returnType)||method.ParameterList.Parameters.Any(p=>WeakParameter(p.Type!.ToString()))))reflection="UFUNCTION()\n";
                text.Append("// EM_METHOD "+JsonSerializer.Serialize(info)+"\n"+(isEvent?"protected":info.Access)+":\n"+reflection+(isEvent||flags.Length>0?"virtual ":"")+SignatureType(returnType)+" "+(isEvent?"EM_":"")+methodName+"("+(methodName is "Update" or "FixedUpdate" or "LateUpdate"?"float DeltaSeconds":cppArgs)+")"+(flags=="override"?" override":"")+" "+body+"\n// EM_END_METHOD\n");
            }
            if(runtime&&parent=="MonoBehaviour"){
                text.Replace("EM_Initialized = true; EM_Awake();","if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))){EM_Initialized = true; EM_Awake();}");
                text.Replace("Super::BeginPlay(); EM_Start();","Super::BeginPlay(); if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))) EM_Start(); else SetActorTickEnabled(false);");
                text.Replace("Super::Tick(DeltaSeconds); EM_Update(DeltaSeconds);","Super::Tick(DeltaSeconds); if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))) EM_Update(DeltaSeconds);");
                text.Replace("Super::BeginPlay(); if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))) EM_Start(); else SetActorTickEnabled(false);",RuntimeComponentPorting.BeginPlayBody);
                text.Replace("Super::Tick(DeltaSeconds); if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))) EM_Update(DeltaSeconds);",RuntimeComponentPorting.TickBody);
                text.Replace("Super::PostInitializeComponents(); if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))){EM_Initialized = true; EM_Awake();}",StartupLifecyclePorting.InitializeBody);
                text.Replace(RuntimeComponentPorting.BeginPlayBody,StartupLifecyclePorting.BeginPlayBody);text.Replace(RuntimeComponentPorting.TickBody,ObjectActivationPorting.TickBody);text.Replace(FinalizeHelper,BehaviourActivationPorting.Finalize);
            }
            if(!lateUpdate&&parent=="MonoBehaviour"){text.Replace(LateUpdatePorting.PreviousTickOrder,LateUpdatePorting.TickOrder);text.Replace(LateUpdatePorting.LegacyStartHelper(className),StartQueuePorting.DeferStart);text.Replace(StartQueuePorting.OldStartup,StartQueuePorting.Startup);text.Replace("Super::Tick(DeltaSeconds); ","Super::Tick(DeltaSeconds); "+StartQueuePorting.TickPrefix);text.Replace("#include \""+className+".generated.h\"\n",StartQueuePorting.Includes+"#include \""+className+".generated.h\"\n"+StartQueuePorting.Runtime);text.Replace(StartupLifecyclePorting.BeginPlayBody,StartupLifecyclePorting.BeginPlayBody+StartQueuePorting.BeginSuffix);}
            if(lateUpdate&&parent=="MonoBehaviour"){text.Replace(LateUpdatePorting.PreviousTickOrder,LateUpdatePorting.TickOrder);text.Replace(LateUpdatePorting.LegacyStartHelper(className),LateUpdatePorting.DeferStart);text.Replace("EM_EnableNotified=true;",LateUpdatePorting.EnableStamp+"EM_EnableNotified=true;");text.Replace("const bool EM_NextPre=EM_IsPostStartDispatch();",LateUpdatePorting.BeforeDestroy);text.Replace("#include \""+className+".generated.h\"\n","#include \""+className+".generated.h\"\n"+LateUpdatePorting.Runtime);text.Replace(StartupLifecyclePorting.BeginPlayBody,StartupLifecyclePorting.BeginPlayBody+LateUpdatePorting.BeginSuffix);}
            if(timeScale&&parent=="MonoBehaviour"){text.Replace("#include \""+className+".generated.h\"\n", TimeScalePorting.Includes+"#include \""+className+".generated.h\"\n"+TimeScalePorting.Runtime);text.Replace(StartupLifecyclePorting.BeginPlayBody,StartupLifecyclePorting.BeginPlayBody+TimeScalePorting.BeginSuffix);}
            if(fixedUpdate&&parent=="MonoBehaviour"){text.Replace("#include \""+className+".generated.h\"\n", FixedUpdatePorting.Includes+"#include \""+className+".generated.h\"\n"+FixedUpdatePorting.Runtime);text.Replace(StartupLifecyclePorting.BeginPlayBody,StartupLifecyclePorting.BeginPlayBody+FixedUpdatePorting.BeginSuffix);}
            if(useWeakUI)text.Append(WeakUiPorting.ClassHelpers(className,cls.Members.OfType<FieldDeclarationSyntax>().Where(f=>UiTypes.Contains(f.Declaration.Type.ToString())).SelectMany(f=>f.Declaration.Variables.Select(v=>(NativeField(N(v.Identifier.ValueText)),f.Declaration.Type.ToString())))));
            text.Append("};\n");var generated=useWeakGameObjects?WeakGameObjectPorting.AdaptRuntime(text.ToString()):text.ToString();files.Add(new(className+".h",useWeakUI?WeakUiPorting.AdaptRuntime(generated):generated));
        }
        return files;

        IFieldSymbol? ComponentField(MemberAccessExpressionSyntax value)=>value.Expression is not ThisExpressionSyntax&&names.Contains(semantic.GetTypeInfo(value.Expression).Type?.Name??"")&&semantic.GetSymbolInfo(value).Symbol is IFieldSymbol field&&names.Contains(field.ContainingType.Name)?field:null;
        string ComponentFieldType(IFieldSymbol field)=>field.Type.SpecialType switch{SpecialType.System_Int32=>"int",SpecialType.System_Single=>"float",SpecialType.System_Double=>"double",SpecialType.System_Boolean=>"bool",_=>field.Type.Name};
        string? UiProperty(MemberAccessExpressionSyntax value){var type=semantic.GetTypeInfo(value.Expression).Type?.ToDisplayString();return (type,value.Name.ToString()) switch{("UnityEngine.Canvas","enabled")=>"CanvasEnabled",("UnityEngine.UI.Button","enabled")=>"ButtonEnabled",("UnityEngine.UI.Button","interactable")=>"ButtonInteractable",_=>null};}
        bool ReadOnlyComponentLookup(InvocationExpressionSyntax call)=>semantic.GetSymbolInfo(call).Symbol is IMethodSymbol {IsGenericMethod:true,Name:"GetComponent"} method&&method.ContainingType.ToDisplayString() is "UnityEngine.GameObject" or "UnityEngine.MonoBehaviour"&&call.ArgumentList.Arguments.Count==0;
        static ExpressionSyntax UnwrapParentheses(ExpressionSyntax value)
        {
            while(value is ParenthesizedExpressionSyntax parenthesized)value=parenthesized.Expression;
            return value;
        }
        bool IsReferenceDefault(ExpressionSyntax value)
        {
            value=UnwrapParentheses(value);
            if(value is not DefaultExpressionSyntax&&!value.IsKind(SyntaxKind.DefaultLiteralExpression))return false;
            bool Supported(ITypeSymbol? type)=>type?.IsReferenceType==true&&(type.Name is "GameObject" or "Transform"||type.ToDisplayString()=="UnityEngine.Object"||UiTypes.Contains(type.Name)||names.Contains(type.Name));
            var info=semantic.GetTypeInfo(value);
            if(Supported(info.Type)||Supported(info.ConvertedType))return true;
            // A default literal in a reference equality can bind as a typeless null.
            ExpressionSyntax operand=value;
            while(operand.Parent is ParenthesizedExpressionSyntax wrapper)operand=wrapper;
            if(value.IsKind(SyntaxKind.DefaultLiteralExpression)&&operand.Parent is BinaryExpressionSyntax binary&&(binary.IsKind(SyntaxKind.EqualsExpression)||binary.IsKind(SyntaxKind.NotEqualsExpression)))
            {
                return Supported(semantic.GetTypeInfo(binary.Left==operand?binary.Right:binary.Left).Type);
            }
            return false;
        }
        bool IsNullReference(ExpressionSyntax value)=>UnwrapParentheses(value).IsKind(SyntaxKind.NullLiteralExpression)||IsReferenceDefault(value);
        string Constant(ILocalSymbol local)
        {
            if(!local.HasConstantValue)throw new NotSupportedException("지역 상수 값을 확인할 수 없습니다: "+local.Name);
            if(local.ConstantValue is bool flag)return flag?"true":"false";
            if(local.ConstantValue is int integer)return integer==int.MinValue?"EM_IntSub(EM_IntNeg(2147483647), 1)":integer<0?"EM_IntNeg("+(-integer).ToString(System.Globalization.CultureInfo.InvariantCulture)+")":integer.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(local.ConstantValue is float or double)
            {
                var number=Convert.ToDouble(local.ConstantValue,System.Globalization.CultureInfo.InvariantCulture);
                if(!double.IsFinite(number))throw new NotSupportedException("NaN/Infinity 지역 상수는 아직 미지원입니다: "+local.Name);
                var negative=BitConverter.DoubleToInt64Bits(number)<0;
                var literal=Math.Abs(number).ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                if(!literal.Contains('.')&&!literal.Contains('E'))literal+=".0";
                if(local.ConstantValue is float)literal+="f";
                return negative?"(-"+literal+")":literal;
            }
            throw new NotSupportedException("지역 상수는 int/float/double/bool만 지원합니다: "+local.Type);
        }
        string ConstantDeclaration(LocalDeclarationStatementSyntax declaration)
        {
            _=LocalType(declaration.Declaration.Type);
            foreach(var variable in declaration.Declaration.Variables){N(variable.Identifier.ValueText,true);if(semantic.GetDeclaredSymbol(variable) is not ILocalSymbol {IsConst:true} local)throw new NotSupportedException("지역 상수 선언 오류");_ = Constant(local);}
            // C# local constants have no runtime storage. Inline their values so
            // unused constants do not trigger native warning-as-error failures.
            return "";
        }
        string SafeIntegerDivision(string left,string operation,ExpressionSyntax right,string? context)
        {
            var constant=semantic.GetConstantValue(right);
            if(!constant.HasValue||constant.Value is not int divisor||divisor is 0 or -1)
                throw new NotSupportedException("정수 나눗셈/나머지는 0·-1이 아닌 int 상수 나눗수만 지원합니다. 런타임 예외 동등성은 미구현입니다.");
            return "("+left+" "+operation+" "+Expression(right,context)+")";
        }
        bool TruthReference(ITypeSymbol? type)=>type is not null&&(type.Name is "GameObject" or "Transform"||UiTypes.Contains(type.Name)||names.Contains(type.Name));
        string NativeExpression(ExpressionSyntax e,string? context)=>e is not ThisExpressionSyntax&&((useWeakUI&&UiTypes.Contains(semantic.GetTypeInfo(e).Type?.Name??""))||(useWeakTransforms&&semantic.GetTypeInfo(e).Type?.ToDisplayString()=="UnityEngine.Transform")||(useWeakGameObjects&&semantic.GetTypeInfo(e).Type?.ToDisplayString()=="UnityEngine.GameObject")||(useWeakComponents&&names.Contains(semantic.GetTypeInfo(e).Type?.Name??"")))?"EM_NativeGameObject("+Expression(e,context)+")":Expression(e,context);
        string ValidExpression(ExpressionSyntax e,string? context)=>useWeakUI&&UiTypes.Contains(semantic.GetTypeInfo(e).Type?.Name??"")?"EM_UIValid("+Expression(e,context,true)+", TEXT("+JsonSerializer.Serialize(semantic.GetTypeInfo(e).Type!.Name)+"))":"EM_ObjectValid("+Expression(e,context,true)+")";
        string Expression(ExpressionSyntax e,string? context,bool suppressTruthConversion=false)
        {
            var type=semantic.GetTypeInfo(e).Type;
            if(!suppressTruthConversion&&TruthReference(type)&&semantic.GetTypeInfo(e).ConvertedType?.SpecialType==SpecialType.System_Boolean)return ValidExpression(e,context);
            if(type is null&&e is ConditionalExpressionSyntax)type=semantic.GetTypeInfo(e).ConvertedType;
            if(e is BinaryExpressionSyntax binary&&binary.OperatorToken.Text is not ("&&" or "||")&&!((binary.IsKind(SyntaxKind.EqualsExpression)||binary.IsKind(SyntaxKind.NotEqualsExpression))&&(IsNullReference(binary.Left)||IsNullReference(binary.Right)))&&binary.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call=>!ReadOnlyComponentLookup(call)))throw new NotSupportedException("상태 변경 호출이 포함된 수식의 C#/C++ 평가 순서는 아직 지원하지 않습니다. 호출 결과를 지역 변수로 분리해 주세요.");
            switch(e)
            {
                case ThisExpressionSyntax:return "this";
                case LiteralExpressionSyntax l when l.Token.Value is bool b:return b?"true":"false";
                case LiteralExpressionSyntax l when l.IsKind(SyntaxKind.NullLiteralExpression):return "nullptr";
                case ExpressionSyntax defaultValue when IsReferenceDefault(defaultValue):return "nullptr";
                case LiteralExpressionSyntax l when l.Token.Value is int i:return i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case LiteralExpressionSyntax l when l.Token.Value is int or double or float:
                    var value=System.Convert.ToDouble(l.Token.Value,System.Globalization.CultureInfo.InvariantCulture).ToString("R",System.Globalization.CultureInfo.InvariantCulture);if(!value.Contains('.')&&!value.Contains('E'))value+=".0";return value+(l.Token.Value is float?"f":"");
                case IdentifierNameSyntax id:
                    if(id.Identifier.ValueText=="enabled"&&semantic.GetSymbolInfo(id).Symbol?.ContainingType.ToDisplayString()=="UnityEngine.MonoBehaviour")return "EM_ReadBehaviourEnabled(this)";
                    if(id.Identifier.ValueText=="transform"&&semantic.GetSymbolInfo(id).Symbol?.ContainingType.Name=="MonoBehaviour")return "EM_SelfTransform()";
                    if(id.Identifier.ValueText=="gameObject"&&semantic.GetSymbolInfo(id).Symbol?.ContainingType.Name=="MonoBehaviour")return "EM_TransformOwner()";
                    var identifier=semantic.GetSymbolInfo(id).Symbol;if(identifier is ILocalSymbol {IsConst:true} constant)return Constant(constant);if(identifier is IFieldSymbol sourceField&&names.Contains(sourceField.ContainingType.Name))return NativeField(N(sourceField.Name));return (identifier is IParameterSymbol?"EM_arg_":identifier is ILocalSymbol?"EM_local_":"")+N(id.Identifier.ValueText,identifier is IParameterSymbol or ILocalSymbol);
                case ParenthesizedExpressionSyntax p:return Expression(p.Expression,context,suppressTruthConversion);
                case ConditionalExpressionSyntax conditional when type?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Boolean:
                    return "("+Expression(conditional.Condition,context)+" ? "+Expression(conditional.WhenTrue,context)+" : "+Expression(conditional.WhenFalse,context)+")";
                case ConditionalExpressionSyntax conditional when type is not null&&(type.Name is "GameObject" or "Transform"||UiTypes.Contains(type.Name)||names.Contains(type.Name)):
                    return "("+Expression(conditional.Condition,context)+" ? EM_ReferenceValue("+Expression(conditional.WhenTrue,context)+") : EM_ReferenceValue("+Expression(conditional.WhenFalse,context)+"))";
                case ConditionalExpressionSyntax:throw new NotSupportedException("조건 연산자 결과 타입은 아직 미지원입니다: "+type);
                case MemberAccessExpressionSyntax a when a.Name.ToString()=="enabled"&&semantic.GetSymbolInfo(a).Symbol?.ContainingType.ToDisplayString()=="UnityEngine.MonoBehaviour":return "EM_ReadBehaviourEnabled("+(a.Expression is ThisExpressionSyntax?"this":NativeExpression(a.Expression,context))+")";
                case MemberAccessExpressionSyntax a when UiProperty(a) is string uiProperty:return "EM_UiRead"+uiProperty+"("+NativeExpression(a.Expression,context)+")";
                case MemberAccessExpressionSyntax a when UiTypes.Contains(semantic.GetTypeInfo(a.Expression).Type?.Name??"")&&a.Name.ToString() is "gameObject" or "transform":return (a.Name.ToString()=="gameObject"?"EM_UiOwner(":"EM_ObjectTransform(EM_UiOwner(")+NativeExpression(a.Expression,context)+(a.Name.ToString()=="gameObject"?")":"))");
                case MemberAccessExpressionSyntax a when a.Name.ToString() is "activeSelf" or "activeInHierarchy"&&semantic.GetSymbolInfo(a).Symbol?.ContainingType.ToDisplayString()=="UnityEngine.GameObject":return (a.Name.ToString()=="activeSelf"?"EM_ObjectActiveSelf(":"EM_ObjectActiveInHierarchy(")+NativeExpression(a.Expression,context)+")";
                case ObjectCreationExpressionSyntax creation when creation.Type.ToString()=="GameObject"&&creation.Initializer is null:
                    if(creation.ArgumentList is null||creation.ArgumentList.Arguments.Count>1||creation.ArgumentList.Arguments.Any(a=>a.NameColon is not null||a.Expression is not LiteralExpressionSyntax{Token.Value:string}))throw new NotSupportedException("GameObject 생성자는 기본 생성자 또는 이름 문자열 상수만 지원합니다.");
                    return "EM_NewGameObject(TEXT("+CppString(creation.ArgumentList.Arguments.Count==0?"New Game Object":(string)((LiteralExpressionSyntax)creation.ArgumentList.Arguments[0].Expression).Token.Value!)+"))";
                case ObjectCreationExpressionSyntax creation when creation.Type.ToString()=="Quaternion"&&creation.Initializer is null:
                    if(creation.ArgumentList!.Arguments.Count!=4||creation.ArgumentList.Arguments.Any(a=>a.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()))throw new NotSupportedException("Quaternion 생성자는 상태 변경 없는 x/y/z/w 인수 네 개만 지원합니다.");
                    return "FVector4f("+string.Join(", ",creation.ArgumentList.Arguments.Select(a=>Expression(a.Expression,context)))+")";
                case MemberAccessExpressionSyntax a when a.ToString()=="Quaternion.identity":return "FVector4f(0,0,0,1)";
                case MemberAccessExpressionSyntax a when a.Name.ToString()=="transform"&&semantic.GetTypeInfo(a.Expression).Type?.Name=="GameObject":return "EM_ObjectTransform("+NativeExpression(a.Expression,context)+")";
                case MemberAccessExpressionSyntax a when a.Name.ToString() is "transform" or "gameObject"&&names.Contains(semantic.GetTypeInfo(a.Expression).Type?.Name??"")&&semantic.GetSymbolInfo(a).Symbol?.ContainingType.Name=="MonoBehaviour":return a.Name.ToString()=="transform"?"EM_ObjectTransform(EM_ComponentOwner("+NativeExpression(a.Expression,context)+"))":"EM_ComponentOwner("+NativeExpression(a.Expression,context)+")";
                case MemberAccessExpressionSyntax a when a.Expression.ToString() is "transform" or "this.transform"&&a.Name.ToString() is "rotation" or "localRotation":return a.Name.ToString()=="rotation"?"EM_GetRotation()":"EM_GetLocalRotation()";
                case MemberAccessExpressionSyntax a when a.Expression.ToString() is "transform" or "this.transform"&&a.Name.ToString()=="parent":return "EM_GetParent()";
                case ObjectCreationExpressionSyntax creation when creation.Type.ToString()=="Vector3"&&creation.Initializer is null:
                    if(creation.ArgumentList!.Arguments.Any(a=>a.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()))throw new NotSupportedException("벡터 생성자 인수의 상태 변경 호출 평가 순서는 미지원입니다.");
                    return creation.ArgumentList!.Arguments.Count==0?"FVector3f::ZeroVector":"FVector3f("+string.Join(", ",creation.ArgumentList.Arguments.Select(a=>Expression(a.Expression,context)))+")";
                case MemberAccessExpressionSyntax a when a.Expression.ToString()=="Vector3"&&new[]{"zero","one","right","up","forward"}.Contains(a.Name.ToString()):
                    return a.Name.ToString() switch{"zero"=>"FVector3f::ZeroVector","one"=>"FVector3f(1.0f, 1.0f, 1.0f)","right"=>"FVector3f(1.0f, 0.0f, 0.0f)","up"=>"FVector3f(0.0f, 1.0f, 0.0f)",_=>"FVector3f(0.0f, 0.0f, 1.0f)"};
                case MemberAccessExpressionSyntax a when IsPosition(a):return a.Name.ToString()=="position"?"EM_GetPosition()":"EM_GetLocalPosition()";
                case MemberAccessExpressionSyntax a when semantic.GetTypeInfo(a.Expression).Type?.ToDisplayString()=="UnityEngine.Transform"&&TransformProperty(a.Name.ToString()) is string property:
                    return "EM_Read"+property+"("+NativeExpression(a.Expression,context)+")";
                case MemberAccessExpressionSyntax a when semantic.GetTypeInfo(a.Expression).Type?.Name is "Vector3" or "Quaternion"&&a.Name.ToString() is "x" or "y" or "z" or "w":return "("+Expression(a.Expression,context)+")."+a.Name.ToString().ToUpperInvariant();
                case MemberAccessExpressionSyntax a when ComponentField(a) is IFieldSymbol field:return "EM_ComponentRead<"+CppType(ComponentFieldType(field))+">("+NativeExpression(a.Expression,context)+", TEXT("+JsonSerializer.Serialize(NativeField(field.Name))+"))";
                case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.EqualsExpression)||b.IsKind(SyntaxKind.NotEqualsExpression):
                    if(IsNullReference(b.Left)||IsNullReference(b.Right))
                    {
                        var reference=IsNullReference(b.Left)?b.Right:b.Left;
                        if(semantic.GetTypeInfo(reference).Type?.Name is not ("GameObject" or "Transform")&&!UiTypes.Contains(semantic.GetTypeInfo(reference).Type?.Name??"")&&!names.Contains(semantic.GetTypeInfo(reference).Type?.Name??""))throw new NotSupportedException("객체 null 비교 유형 미지원");
                        return b.IsKind(SyntaxKind.EqualsExpression)?"(!"+ValidExpression(reference,context)+")":ValidExpression(reference,context);
                    }
                    if(semantic.GetTypeInfo(b.Left).ConvertedType?.SpecialType==SpecialType.System_Boolean&&semantic.GetTypeInfo(b.Right).ConvertedType?.SpecialType==SpecialType.System_Boolean)return "("+Expression(b.Left,context)+" "+b.OperatorToken.Text+" "+Expression(b.Right,context)+")";
                    if(useWeakUI&&UiTypes.Contains(semantic.GetTypeInfo(b.Left).Type?.Name??"")&&UiTypes.Contains(semantic.GetTypeInfo(b.Right).Type?.Name??""))
                        return (b.IsKind(SyntaxKind.NotEqualsExpression)?"(!EM_UIEqual(":"EM_UIEqual(")+Expression(b.Left,context)+", TEXT("+JsonSerializer.Serialize(semantic.GetTypeInfo(b.Left).Type!.Name)+"), "+Expression(b.Right,context)+", TEXT("+JsonSerializer.Serialize(semantic.GetTypeInfo(b.Right).Type!.Name)+")"+(b.IsKind(SyntaxKind.NotEqualsExpression)?"))":")");
                    if(useWeakUI&&(UiTypes.Contains(semantic.GetTypeInfo(b.Left).Type?.Name??"")||UiTypes.Contains(semantic.GetTypeInfo(b.Right).Type?.Name??"")))throw new NotSupportedException("UI 참조와 다른 참조 종류 간 비교는 아직 미지원입니다.");
                    if(semantic.GetTypeInfo(b.Left).Type?.ToDisplayString()=="UnityEngine.Transform"&&semantic.GetTypeInfo(b.Right).Type?.ToDisplayString()=="UnityEngine.Transform"&&useWeakTransforms)
                        return (b.IsKind(SyntaxKind.NotEqualsExpression)?"(!EM_GameObjectEqual(":"EM_GameObjectEqual(")+Expression(b.Left,context)+", "+Expression(b.Right,context)+(b.IsKind(SyntaxKind.NotEqualsExpression)?"))":")");
                    if(semantic.GetTypeInfo(b.Left).Type?.Name=="GameObject"||semantic.GetTypeInfo(b.Right).Type?.Name=="GameObject")
                    {
                        if(!useWeakGameObjects)throw new NotSupportedException("GameObject 동등성 비교에는 GC 식별 정보를 보존하는 참조 형식이 필요합니다.");
                        if(semantic.GetTypeInfo(b.Left).Type?.ToDisplayString()!="UnityEngine.GameObject"||semantic.GetTypeInfo(b.Right).Type?.ToDisplayString()!="UnityEngine.GameObject")throw new NotSupportedException("GameObject와 다른 객체 유형 간 동등성 비교는 아직 미지원입니다.");
                        // A destroyed wrapper equals null, but two distinct destroyed
                        // wrappers do not equal each other. Reuse the component helper
                        // which distinguishes pointer identity from native validity.
                        return (b.IsKind(SyntaxKind.NotEqualsExpression)?"(!EM_GameObjectEqual(":"EM_GameObjectEqual(")+Expression(b.Left,context)+", "+Expression(b.Right,context)+(b.IsKind(SyntaxKind.NotEqualsExpression)?"))":")");
                    }
                    if(names.Contains(semantic.GetTypeInfo(b.Left).Type?.Name??"")||names.Contains(semantic.GetTypeInfo(b.Right).Type?.Name??"")){
                        if(useWeakComponents&&(!names.Contains(semantic.GetTypeInfo(b.Left).Type?.Name??"")||!names.Contains(semantic.GetTypeInfo(b.Right).Type?.Name??"")))throw new NotSupportedException("사용자 컴포넌트와 다른 참조 유형 간 동등성 비교는 아직 미지원입니다.");
                        var equality=useWeakComponents?"EM_GameObjectEqual":"EM_ComponentEqual";
                        return (b.IsKind(SyntaxKind.NotEqualsExpression)?"(!"+equality+"(":equality+"(")+Expression(b.Left,context)+", "+Expression(b.Right,context)+(b.IsKind(SyntaxKind.NotEqualsExpression)?"))":")");
                    }
                    return "("+Expression(b.Left,context)+" "+b.OperatorToken.Text+" "+Expression(b.Right,context)+")";
                case BinaryExpressionSyntax b when type?.SpecialType==SpecialType.System_Int32&&new[]{"+","-","*"}.Contains(b.OperatorToken.Text):return "EM_Int"+(b.OperatorToken.Text=="+"?"Add":b.OperatorToken.Text=="-"?"Sub":"Mul")+"("+Expression(b.Left,context)+", "+Expression(b.Right,context)+")";
                case BinaryExpressionSyntax b when type?.SpecialType==SpecialType.System_Int32&&b.OperatorToken.Text is "/" or "%":return SafeIntegerDivision(Expression(b.Left,context),b.OperatorToken.Text,b.Right,context);
                case BinaryExpressionSyntax b when type?.SpecialType==SpecialType.System_Int32&&BitOperation(b.OperatorToken.Text) is string operation:return "EM_Int"+operation+"("+Expression(b.Left,context)+", "+Expression(b.Right,context)+")";
                case PrefixUnaryExpressionSyntax u when type?.SpecialType==SpecialType.System_Int32&&u.IsKind(SyntaxKind.BitwiseNotExpression):return "EM_IntNot("+Expression(u.Operand,context)+")";
                case BinaryExpressionSyntax b when type?.SpecialType==SpecialType.System_Int32:throw new NotSupportedException("정수 나눗셈/나머지/비트 연산 미지원: "+b.OperatorToken.Text);
                case BinaryExpressionSyntax b when type?.Name=="Vector3"&&b.OperatorToken.Text=="/":throw new NotSupportedException("벡터 나눗셈의 비정상 값 처리는 아직 미지원입니다.");
                case BinaryExpressionSyntax b when new[]{"+","-","*","/","<",">","<=",">=","==","!=","&&","||"}.Contains(b.OperatorToken.Text):return "("+Expression(b.Left,context)+" "+b.OperatorToken.Text+" "+Expression(b.Right,context)+")";
                case PrefixUnaryExpressionSyntax u when u.OperatorToken.Text=="!"&&TruthReference(semantic.GetTypeInfo(u.Operand).Type):return "(!"+ValidExpression(u.Operand,context)+")";
                case PrefixUnaryExpressionSyntax u when type?.SpecialType==SpecialType.System_Int32&&u.OperatorToken.Text=="-":return "EM_IntNeg("+Expression(u.Operand,context)+")";
                case PrefixUnaryExpressionSyntax u when new[]{"-","+","!"}.Contains(u.OperatorToken.Text):return "("+u.OperatorToken.Text+Expression(u.Operand,context)+")";
                case MemberAccessExpressionSyntax a when a.Expression.ToString()=="Time"&&a.Name.ToString()=="timeScale":return "EM_TimeScale()";
                case MemberAccessExpressionSyntax a when a.Expression.ToString()=="Time"&&a.Name.ToString()=="fixedDeltaTime":return "EM_FixedStep()";
                case MemberAccessExpressionSyntax a when a.Expression.ToString()=="Time"&&a.Name.ToString()=="deltaTime"&&context is "Update" or "FixedUpdate" or "LateUpdate":return "DeltaSeconds";
                case MemberAccessExpressionSyntax a when a.Expression is ThisExpressionSyntax:return "this->"+(semantic.GetSymbolInfo(a).Symbol is IFieldSymbol?NativeField(N(a.Name.ToString())):N(a.Name.ToString()));
                case InvocationExpressionSyntax call:
                    var engineSymbol=semantic.GetSymbolInfo(call).Symbol as IMethodSymbol;
                    if(engineSymbol?.ContainingType.ToDisplayString()=="UnityEngine.GameObject"&&engineSymbol.Name=="SetActive"&&call.Expression is MemberAccessExpressionSyntax activeCall){
                        if(call.ArgumentList.Arguments.Count!=1||call.ArgumentList.Arguments.Any(a=>a.NameColon is not null||a.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())||activeCall.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())throw new NotSupportedException("SetActive 상태 변경 인수는 지역 변수로 분리해 주세요.");
                        return "EM_SetObjectActive("+NativeExpression(activeCall.Expression,context)+", "+Expression(call.ArgumentList.Arguments[0].Expression,context)+")";
                    }
                    if(engineSymbol is {IsGenericMethod:true}&&engineSymbol.Name is "AddComponent" or "GetComponent"&&engineSymbol.ContainingType.ToDisplayString() is "UnityEngine.GameObject" or "UnityEngine.MonoBehaviour"){
                        var component=engineSymbol.TypeArguments.Single().Name;if((!names.Contains(component)&&!UiTypes.Contains(component))||call.ArgumentList.Arguments.Count!=0)throw new NotSupportedException("변환 프로젝트의 사용자 MonoBehaviour와 지원 UI 컴포넌트만 조회할 수 있습니다.");
                        var owner=call.Expression is MemberAccessExpressionSyntax member?NativeExpression(member.Expression,context):useWeakGameObjects?"EM_NativeGameObject(EM_TransformOwner())":"EM_TransformOwner()";
                        if(call.Expression is MemberAccessExpressionSyntax receiver&&names.Contains(semantic.GetTypeInfo(receiver.Expression).Type?.Name??""))owner="EM_ComponentOwner("+owner+")";
                        if(UiTypes.Contains(component)){return "EM_Ui"+(engineSymbol.Name=="AddComponent"?"Add":"Get")+component+"("+owner+")";}
                        return (engineSymbol.Name=="AddComponent"?"EM_AddComponent(":"EM_GetComponent(")+owner+", TEXT("+JsonSerializer.Serialize("/Script/EMActorPort."+component)+"))";
                    }
                    if(engineSymbol?.ContainingType.ToDisplayString()=="UnityEngine.Transform"&&engineSymbol.Name=="SetParent"&&call.Expression is MemberAccessExpressionSyntax parentCall){
                        if(call.ArgumentList.Arguments.Count is <1 or >2||call.ArgumentList.Arguments.Any(a=>a.NameColon is not null||a.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())||parentCall.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())throw new NotSupportedException("SetParent 상태 변경 인수는 지역 변수로 분리해 주세요.");
                        return "EM_SetParent("+NativeExpression(parentCall.Expression,context)+", "+NativeExpression(call.ArgumentList.Arguments[0].Expression,context)+", "+(call.ArgumentList.Arguments.Count==1?"true":Expression(call.ArgumentList.Arguments[1].Expression,context))+")";
                    }
                    if(engineSymbol?.ContainingType.ToDisplayString()=="UnityEngine.Object"){
                        if(call.ArgumentList.Arguments.Any(a=>a.NameColon is not null||a.RefKindKeyword.Kind()!=SyntaxKind.None)||call.ArgumentList.Arguments.Count==0||(semantic.GetTypeInfo(call.ArgumentList.Arguments[0].Expression).Type?.Name!="GameObject"&&!(engineSymbol.Name=="Destroy"&&(names.Contains(semantic.GetTypeInfo(call.ArgumentList.Arguments[0].Expression).Type?.Name??"")||(useWeakUI&&UiTypes.Contains(semantic.GetTypeInfo(call.ArgumentList.Arguments[0].Expression).Type?.Name??""))))))throw new NotSupportedException("GameObject 생성·삭제 인수 미지원");
                        if(call.ArgumentList.Arguments.Count>1&&call.ArgumentList.Arguments.Any(a=>a.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()))throw new NotSupportedException("생성 인수의 상태 변경 호출은 지역 변수로 먼저 분리해 주세요.");
                        var signature=string.Join(",",engineSymbol.Parameters.Select(p=>p.Type.Name));
                        var helper=engineSymbol.Name=="Destroy"&&signature=="GameObject"?"EM_DestroyGameObject":engineSymbol.Name=="Destroy"&&signature=="MonoBehaviour"?"EM_Destroy":engineSymbol.Name=="Destroy"&&useWeakUI&&UiTypes.Contains(signature)?"EM_Destroy"+signature:engineSymbol.Name!="Instantiate"?throw new NotSupportedException("Object API 미지원"):signature switch{"GameObject"=>"EM_Instantiate","GameObject,Transform"=>"EM_InstantiateParent","GameObject,Transform,Boolean"=>"EM_InstantiateParentWorld","GameObject,Vector3,Quaternion"=>"EM_InstantiatePose","GameObject,Vector3,Quaternion,Transform"=>"EM_InstantiatePoseParent",_=>throw new NotSupportedException("Instantiate 오버로드 미지원: "+signature)};
                        return helper+"("+string.Join(", ",call.ArgumentList.Arguments.Select(a=>NativeExpression(a.Expression,context)))+")";
                    }
                    if(engineSymbol?.ContainingType.ToDisplayString()=="UnityEngine.SceneManagement.SceneManager"){
                        if(engineSymbol.Name!="LoadScene"||call.ArgumentList.Arguments.Count!=1||call.ArgumentList.Arguments[0].NameColon is not null||call.ArgumentList.Arguments[0].RefKindKeyword.Kind()!=SyntaxKind.None)throw new NotSupportedException("LoadScene은 단일 장면 이름 또는 int 빌드 인덱스만 지원합니다.");
                        var argument=call.ArgumentList.Arguments[0].Expression;var sceneConstant=semantic.GetConstantValue(argument);
                        if(!sceneConstant.HasValue&&semantic.GetTypeInfo(argument).Type?.SpecialType==SpecialType.System_Int32)return "EM_LoadSceneIndex("+Expression(argument,context)+")";
                        if(!sceneConstant.HasValue||sceneConstant.Value is not (string or int))throw new NotSupportedException("LoadScene 이름은 상수 문자열이어야 합니다.");
                        var key=sceneConstant.Value is int index?"#"+index:(string)sceneConstant.Value;
                        if(sceneConstant.Value is string sceneName&&(sceneName.Contains('/')||sceneName.Contains('\\')||sceneName.Contains('=')||sceneName.Contains(':')||sceneName.StartsWith('#')))throw new NotSupportedException("LoadScene 문자열은 활성 빌드 장면의 고유한 파일 이름만 지원합니다. 경로·특수 키는 미지원입니다.");
                        return "EM_LoadScene(TEXT("+JsonSerializer.Serialize(key)+"))";
                    }
                    if(call.ArgumentList.Arguments.Any(a=>a.NameColon is not null||a.RefKindKeyword.Kind()!=SyntaxKind.None))throw new NotSupportedException("호출 인수 미지원");
                    if(engineSymbol is not null&&names.Contains(engineSymbol.ContainingType.Name)&&call.Expression is MemberAccessExpressionSyntax otherCall&&otherCall.Expression is not (ThisExpressionSyntax or BaseExpressionSyntax)){
                        bool Callable(ITypeSymbol value)=>value.SpecialType is SpecialType.System_Void or SpecialType.System_Int32 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Boolean||value.Name=="GameObject"||(useWeakComponents&&names.Contains(value.Name))||(useWeakTransforms&&value.Name=="Transform")||(useWeakUI&&UiTypes.Contains(value.Name));
                        if(engineSymbol.DeclaredAccessibility!=Accessibility.Public||Events.Contains(engineSymbol.Name)||!Callable(engineSymbol.ReturnType)||engineSymbol.Parameters.Any(p=>!Callable(p.Type))||otherCall.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()||call.ArgumentList.Arguments.Any(a=>a.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()))throw new NotSupportedException("다른 컴포넌트 호출은 분리한 수신자와 상태 변경 없는 인수의 public 기본형 함수만 지원합니다.");
                        var returnType=engineSymbol.ReturnType.SpecialType switch{SpecialType.System_Void=>"void",SpecialType.System_Int32=>"int",SpecialType.System_Single=>"float",SpecialType.System_Double=>"double",SpecialType.System_Boolean=>"bool",_=>engineSymbol.ReturnType.Name};
                        return "EM_ComponentCall<"+CppType(returnType)+">("+NativeExpression(otherCall.Expression,context)+", TEXT("+JsonSerializer.Serialize(engineSymbol.Name)+")"+(call.ArgumentList.Arguments.Count==0?"":", "+string.Join(", ",call.ArgumentList.Arguments.Select(a=>Expression(a.Expression,context))))+")";
                    }
                    if(call.ArgumentList.Arguments.Count>1&&call.ArgumentList.Arguments.Any(a=>a.DescendantNodes().OfType<InvocationExpressionSyntax>().Any()))throw new NotSupportedException("복수 인수 안의 상태 변경 호출 평가 순서는 미지원입니다.");
                    var symbol=semantic.GetSymbolInfo(call).Symbol as IMethodSymbol;if(symbol is null||!names.Contains(symbol.ContainingType.Name))throw new NotSupportedException("외부/엔진 호출 미지원: "+call);
                    var target=N(symbol.Name);var isBase=call.Expression is MemberAccessExpressionSyntax { Expression:BaseExpressionSyntax };if(call.Expression is not IdentifierNameSyntax&&!isBase&&call.Expression is not MemberAccessExpressionSyntax { Expression:ThisExpressionSyntax })throw new NotSupportedException("다른 객체 호출 미지원");
                    if(Events.Contains(target)&&!isBase)throw new NotSupportedException("생명주기 직접 호출은 부모 호출만 지원합니다.");
                    return (isBase?"Super::":"")+(Events.Contains(target)?"EM_":"")+target+"("+(target is "Update" or "FixedUpdate" or "LateUpdate"?"DeltaSeconds":string.Join(", ",call.ArgumentList.Arguments.Select(a=>Expression(a.Expression,context))))+")";
                case CastExpressionSyntax cast when cast.Type.ToString()=="bool"&&TruthReference(semantic.GetTypeInfo(cast.Expression).Type):return ValidExpression(cast.Expression,context);
                case CastExpressionSyntax cast when cast.Type.ToString() is "float" or "double" && semantic.GetTypeInfo(cast.Expression).Type?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Single or SpecialType.System_Double:return "static_cast<"+cast.Type+">("+Expression(cast.Expression,context)+")";
                case CastExpressionSyntax cast when cast.Type.ToString()=="Transform"&&IsNull(cast.Expression):return "static_cast<EM_TransformRef>(nullptr)";
                default:throw new NotSupportedException("Actor 미지원 표현식: "+e.Kind()+" — "+e.ToString());
            }
        }
        string Statement(StatementSyntax s,string context)=>s switch
        {
            BlockSyntax b=>"{\n"+string.Join("\n",b.Statements.Select(n=>Statement(n,context)))+"\n}",
            ExpressionStatementSyntax { Expression:AssignmentExpressionSyntax a } when a.Left is IdentifierNameSyntax or MemberAccessExpressionSyntax && new[]{"=","+=","-=","*=","/=","%=","&=","|=","^=","<<=",">>="}.Contains(a.OperatorToken.Text)=>Assignment(a,context),
            ExpressionStatementSyntax { Expression:InvocationExpressionSyntax c }=>Expression(c,context)+";",
            ExpressionStatementSyntax { Expression:PostfixUnaryExpressionSyntax p } when p.OperatorToken.Text is "++" or "--"=>Increment(p.Operand,p.OperatorToken.Text,context),
            ExpressionStatementSyntax { Expression:PrefixUnaryExpressionSyntax p } when p.OperatorToken.Text is "++" or "--"=>Increment(p.Operand,p.OperatorToken.Text,context),
            ReturnStatementSyntax r=>"return"+(r.Expression is null?"":" "+Expression(r.Expression,context))+";",
            IfStatementSyntax b=>"if ("+Expression(b.Condition,context)+") "+Statement(b.Statement,context)+(b.Else is null?"":" else "+Statement(b.Else.Statement,context)),
            WhileStatementSyntax w=>"while ("+Expression(w.Condition,context)+") "+Statement(w.Statement,context),
            ForStatementSyntax f when f.AttributeLists.Count==0=>"for ("+(f.Declaration is null?string.Join(", ",f.Initializers.Select(e=>ForClause(e,context))):Declaration(f.Declaration,context))+"; "+(f.Condition is null?"":Expression(f.Condition,context))+"; "+string.Join(", ",f.Incrementors.Select(e=>ForClause(e,context)))+") "+Statement(f.Statement,context),
            DoStatementSyntax d when d.AttributeLists.Count==0=>"do "+Statement(d.Statement,context)+" while ("+Expression(d.Condition,context)+");",
            SwitchStatementSyntax branch when branch.AttributeLists.Count==0=>Switch(branch,context),
            EmptyStatementSyntax=>";",
            BreakStatementSyntax=>"break;",ContinueStatementSyntax=>"continue;",
            LocalDeclarationStatementSyntax l when l.Modifiers.Count==1&&l.Modifiers[0].IsKind(SyntaxKind.ConstKeyword)=>ConstantDeclaration(l),
            LocalDeclarationStatementSyntax l when l.Modifiers.Count==0=>Declaration(l.Declaration,context)+";",
            _=>throw new NotSupportedException("Actor 미지원 문장: "+s.Kind())
        };
        string Switch(SwitchStatementSyntax branch,string context)
        {
            if(semantic.GetTypeInfo(branch.Expression).Type?.SpecialType!=SpecialType.System_Int32)throw new NotSupportedException("switch 분기는 int 선택값만 지원합니다.");
            var localSections=new Dictionary<ISymbol,int>(SymbolEqualityComparer.Default);
            var liftedDeclarations=new HashSet<int>();
            for(var i=0;i<branch.Sections.Count;i++)
                foreach(var declaration in branch.Sections[i].DescendantNodes().OfType<VariableDeclaratorSyntax>())
                    if(semantic.GetDeclaredSymbol(declaration) is ILocalSymbol local)localSections.Add(local,i);
            for(var i=0;i<branch.Sections.Count;i++)
                foreach(var identifier in branch.Sections[i].DescendantNodes().OfType<IdentifierNameSyntax>())
                    if(semantic.GetSymbolInfo(identifier).Symbol is ILocalSymbol {IsConst:false} local&&localSections.TryGetValue(local,out var owner)&&owner!=i)
                    {
                        var syntax=local.DeclaringSyntaxReferences.Single().GetSyntax();
                        if(syntax.Parent?.Parent is not LocalDeclarationStatementSyntax declaration||declaration.Parent is not SwitchSectionSyntax||declaration.Modifiers.Count!=0)
                            throw new NotSupportedException("switch 공유 지역 변수의 선언 형태 미지원: "+local.Name);
                        liftedDeclarations.Add(declaration.SpanStart);
                    }
            var lifted=branch.Sections.SelectMany(s=>s.Statements.OfType<LocalDeclarationStatementSyntax>()).Where(d=>liftedDeclarations.Contains(d.SpanStart)).ToArray();
            // Reserve trivial value/reference storage before the switch, but run
            // each authored initializer only in its selected section and in order.
            string InitialValue(string type)=>type switch{"bool"=>"false","int"=>"0","float"=>"0.0f","double"=>"0.0","Vector3"=>"FVector3f::ZeroVector","Quaternion"=>"FVector4f(0,0,0,0)",_=>"nullptr"};
            var preamble=string.Join("\n",lifted.SelectMany(d=>d.Declaration.Variables.Select(v=>CppType(LocalType(d.Declaration.Type))+" EM_local_"+N(v.Identifier.ValueText,true)+" = "+InitialValue(LocalType(d.Declaration.Type))+";")));
            string SectionStatement(StatementSyntax statement)
            {
                if(statement is LocalDeclarationStatementSyntax declaration&&liftedDeclarations.Contains(declaration.SpanStart))
                    return string.Join("\n",declaration.Declaration.Variables.Where(v=>v.Initializer is not null).Select(v=>"EM_local_"+N(v.Identifier.ValueText,true)+" = "+Expression(v.Initializer!.Value,context)+";"));
                return Statement(statement,context);
            }
            string Label(SwitchLabelSyntax label)=>label switch
            {
                DefaultSwitchLabelSyntax=>"default:",
                CaseSwitchLabelSyntax c when semantic.GetConstantValue(c.Value).Value is int constant=>"case "+(constant==int.MinValue?"(-2147483647 - 1)":constant.ToString(System.Globalization.CultureInfo.InvariantCulture))+":",
                _=>throw new NotSupportedException("switch는 정수 상수 case와 default만 지원합니다. 패턴/when/goto case는 미지원입니다.")
            };
            string Section(SwitchSectionSyntax section)
            {
                var body=section.Statements.Count==1&&section.Statements[0] is BlockSyntax block?Statement(block,context):"{\n"+string.Join("\n",section.Statements.Select(SectionStatement))+"\n}";
                var labels=string.Join("\n",section.Labels.Select(Label));
                // A default-only switch triggers MSVC C4065. Case zero shares the
                // same body, so every selector still takes exactly the same path.
                if(!branch.Sections.SelectMany(s=>s.Labels).Any(l=>l is CaseSwitchLabelSyntax)&&section.Labels.Any(l=>l is DefaultSwitchLabelSyntax))labels="case 0:\n"+labels;
                return labels+"\n"+body;
            }
            // An empty C# switch still evaluates its selector. Keep that evaluation
            // while avoiding MSVC C4060 (no labels) in warning-as-error builds.
            var sections=branch.Sections.Count==0?"case 0:\ndefault:\n{\nbreak;\n}":string.Join("\n",branch.Sections.Select(Section));
            var result="switch ("+Expression(branch.Expression,context)+") {\n"+sections+"\n}";
            return lifted.Length==0?result:"{\n"+preamble+"\n"+result+"\n}";
        }
        string Declaration(VariableDeclarationSyntax declaration,string context)=>CppType(LocalType(declaration.Type))+" "+string.Join(", ",declaration.Variables.Select(v=>"EM_local_"+N(v.Identifier.ValueText,true)+" = "+Expression(v.Initializer?.Value??throw new NotSupportedException("초기값 필요"),context)));
        string ForClause(ExpressionSyntax expression,string context)=>expression switch
        {
            AssignmentExpressionSyntax a when a.Left is IdentifierNameSyntax or MemberAccessExpressionSyntax&&new[]{"=","+=","-=","*=","/=","%=","&=","|=","^=","<<=",">>="}.Contains(a.OperatorToken.Text)=>Assignment(a,context).TrimEnd(';'),
            InvocationExpressionSyntax call=>Expression(call,context),
            PostfixUnaryExpressionSyntax p when p.OperatorToken.Text is "++" or "--"=>Increment(p.Operand,p.OperatorToken.Text,context).TrimEnd(';'),
            PrefixUnaryExpressionSyntax p when p.OperatorToken.Text is "++" or "--"=>Increment(p.Operand,p.OperatorToken.Text,context).TrimEnd(';'),
            _=>throw new NotSupportedException("for 초기식/증감식 미지원: "+expression.Kind())
        };
        string Assignment(AssignmentExpressionSyntax a,string context)
        {
            if(BitOperation(a.OperatorToken.Text.TrimEnd('=')) is not null&&semantic.GetTypeInfo(a.Left).Type?.SpecialType!=SpecialType.System_Int32)throw new NotSupportedException("비트 복합 대입은 int만 지원합니다.");
            if(a.OperatorToken.Text=="%="&&semantic.GetTypeInfo(a.Left).Type?.SpecialType!=SpecialType.System_Int32)throw new NotSupportedException("나머지 복합 대입은 int만 지원합니다.");
            if(a.Left is MemberAccessExpressionSyntax ui&&UiProperty(ui) is string uiProperty){if(a.OperatorToken.Text!="="||a.Right.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()||ui.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())throw new NotSupportedException("UI 속성 대입의 호출은 지역 변수로 먼저 분리해 주세요.");return "EM_UiWrite"+uiProperty+"("+NativeExpression(ui.Expression,context)+", "+Expression(a.Right,context)+");";}
            if(semantic.GetSymbolInfo(a.Left).Symbol?.ContainingType.ToDisplayString()=="UnityEngine.MonoBehaviour"&&((a.Left is IdentifierNameSyntax enabledId&&enabledId.Identifier.ValueText=="enabled")||(a.Left is MemberAccessExpressionSyntax enabledMember&&enabledMember.Name.ToString()=="enabled"))){
                if(a.OperatorToken.Text!="="||a.Right.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())throw new NotSupportedException("enabled 속성은 상태 변경 호출을 분리한 단순 대입만 지원합니다.");
                var target=a.Left is MemberAccessExpressionSyntax member&&member.Expression is not ThisExpressionSyntax?NativeExpression(member.Expression,context):"this";
                return "EM_WriteBehaviourEnabled("+target+", "+Expression(a.Right,context)+");";
            }
            if(a.Left is MemberAccessExpressionSyntax componentMember&&ComponentField(componentMember) is IFieldSymbol componentField){
                if(a.Right.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()||componentMember.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())throw new NotSupportedException("컴포넌트 필드 대입의 호출은 지역 변수로 먼저 분리해 주세요.");
                var value=Expression(a.Right,context);var fieldType=ComponentFieldType(componentField);
                if(a.OperatorToken.Text!="="){
                    if(fieldType=="int"&&a.OperatorToken.Text is "+=" or "-=" or "*=")value="EM_Int"+(a.OperatorToken.Text=="+="?"Add":a.OperatorToken.Text=="-="?"Sub":"Mul")+"("+Expression(componentMember,context)+", "+value+")";
                    else if(fieldType=="int"&&BitOperation(a.OperatorToken.Text.TrimEnd('=')) is string bitOperation)value="EM_Int"+bitOperation+"("+Expression(componentMember,context)+", "+value+")";
                    else if(fieldType=="int"&&a.OperatorToken.Text is "/=" or "%=")value=SafeIntegerDivision(Expression(componentMember,context),a.OperatorToken.Text[0].ToString(),a.Right,context);
                    else if(fieldType is "float" or "double"&&a.OperatorToken.Text is "+=" or "-=" or "*=" or "/=")value="("+Expression(componentMember,context)+" "+a.OperatorToken.Text[0]+" "+value+")";
                    else throw new NotSupportedException("컴포넌트 복합 대입 연산 미지원");
                }
                return "EM_ComponentWrite<"+CppType(fieldType)+">("+NativeExpression(componentMember.Expression,context)+", TEXT("+JsonSerializer.Serialize(NativeField(componentField.Name))+"), "+value+");";
            }
            if(a.Left is MemberAccessExpressionSyntax scale&&scale.Expression.ToString()=="Time"&&scale.Name.ToString()=="timeScale"){
                if(a.OperatorToken.Text!="="||a.Right.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())throw new NotSupportedException("시간 배율은 상태 변경 호출을 분리한 단순 대입만 지원합니다.");
                return "EM_SetTimeScale("+Expression(a.Right,context)+");";
            }
            if(a.Left is MemberAccessExpressionSyntax fixedTime&&fixedTime.Expression.ToString()=="Time"&&fixedTime.Name.ToString()=="fixedDeltaTime")throw new NotSupportedException("런타임 Time.fixedDeltaTime 변경은 아직 미지원입니다. 프로젝트 고정 시간 설정을 사용해 주세요.");
            if(a.Left is MemberAccessExpressionSyntax name&&name.Name.ToString()=="name"&&semantic.GetSymbolInfo(name).Symbol?.ContainingType.ToDisplayString()=="UnityEngine.Object"){
                if(a.OperatorToken.Text!="="||a.Right is not LiteralExpressionSyntax{Token.Value:string label})throw new NotSupportedException("객체 이름 대입은 문자열 상수만 지원합니다.");
                return (useUiOwnerNames&&UiTypes.Contains(semantic.GetTypeInfo(name.Expression).Type?.Name??"")?"EM_UiSetObjectName(":"EM_SetObjectName(")+NativeExpression(name.Expression,context)+", TEXT("+CppString(label)+"));";
            }
            if(a.OperatorToken.Text!="="&&a.Right.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())throw new NotSupportedException("상태 변경 호출을 포함하는 복합 대입 평가 순서는 미지원입니다.");
            if(a.Left is MemberAccessExpressionSyntax position&&IsPosition(position))
            {
                if(a.OperatorToken.Text is not ("=" or "+=" or "-="))throw new NotSupportedException("Transform 위치 대입 연산 미지원");
                var value=Expression(a.Right,context);if(a.OperatorToken.Text!="=")value="("+Expression(position,context)+" "+a.OperatorToken.Text[0]+" "+value+")";
                return (position.Name.ToString()=="position"?"EM_SetPosition(":"EM_SetLocalPosition(")+value+");";
            }
            if(a.Left is MemberAccessExpressionSyntax transform&&semantic.GetTypeInfo(transform.Expression).Type?.ToDisplayString()=="UnityEngine.Transform"&&TransformProperty(transform.Name.ToString()) is string property){
                if(a.OperatorToken.Text!="="||a.Right.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()||transform.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())throw new NotSupportedException("Transform 대입은 상태 변경 호출을 지역 변수로 분리한 단순 대입만 지원합니다.");
                return property=="Parent"?"EM_SetParent("+NativeExpression(transform.Expression,context)+", "+NativeExpression(a.Right,context)+", true);":"EM_Write"+property+"("+NativeExpression(transform.Expression,context)+", "+Expression(a.Right,context)+");";
            }
            if(semantic.GetTypeInfo(a.Left).Type?.SpecialType==SpecialType.System_Int32&&a.OperatorToken.Text!="=")
            {
                if(semantic.GetTypeInfo(a.Right).Type?.SpecialType!=SpecialType.System_Int32||a.OperatorToken.Text is not ("+=" or "-=" or "*=" or "/=" or "%=" or "&=" or "|=" or "^=" or "<<=" or ">>="))throw new NotSupportedException("정수 복합 대입 형변환/연산 미지원");
                var left=Expression(a.Left,context);
                if(BitOperation(a.OperatorToken.Text.TrimEnd('=')) is string bitOperation)return left+" = EM_Int"+bitOperation+"("+left+", "+Expression(a.Right,context)+");";
                if(a.OperatorToken.Text is "/=" or "%=")return left+" = "+SafeIntegerDivision(left,a.OperatorToken.Text[0].ToString(),a.Right,context)+";";
                return left+" = EM_Int"+(a.OperatorToken.Text=="+="?"Add":a.OperatorToken.Text=="-="?"Sub":"Mul")+"("+left+", "+Expression(a.Right,context)+");";
            }
            return Expression(a.Left,context)+" "+a.OperatorToken.Text+" "+Expression(a.Right,context)+";";
        }
        string Increment(ExpressionSyntax operand,string op,string context)
        {
            if(semantic.GetTypeInfo(operand).Type?.SpecialType!=SpecialType.System_Int32||operand is not (IdentifierNameSyntax or MemberAccessExpressionSyntax))throw new NotSupportedException("정수 변수의 단독 증감만 지원합니다.");
            if(operand is MemberAccessExpressionSyntax member&&ComponentField(member) is IFieldSymbol field){
                if(member.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())throw new NotSupportedException("컴포넌트 증감의 수신자 호출은 지역 변수로 분리해 주세요.");
                return "EM_ComponentWrite<int32>("+NativeExpression(member.Expression,context)+", TEXT("+JsonSerializer.Serialize(NativeField(field.Name))+"), EM_Int"+(op=="++"?"Add":"Sub")+"("+Expression(operand,context)+", 1));";
            }
            var left=Expression(operand,context);return left+" = EM_Int"+(op=="++"?"Add":"Sub")+"("+left+", 1);";
        }
    }
    private static bool IsPosition(MemberAccessExpressionSyntax value)=>value.Name.ToString() is "position" or "localPosition"&&value.Expression.ToString() is "transform" or "this.transform";
    private static string? TransformProperty(string name)=>name switch{"position"=>"Position","localPosition"=>"LocalPosition","localScale"=>"LocalScale","rotation"=>"Rotation","localRotation"=>"LocalRotation","parent"=>"Parent",_=>null};
    private static string CppString(string value)
    {
        if(value.Contains('\0'))throw new NotSupportedException("런타임 객체 이름에 NUL 문자는 지원하지 않습니다.");
        try{_ = new System.Text.UTF8Encoding(false,true).GetByteCount(value);}catch(System.Text.EncoderFallbackException){throw new NotSupportedException("잘못된 UTF-16 객체 이름은 지원하지 않습니다.");}
        return JsonSerializer.Serialize(value,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
    }
    private static bool IsNull(ExpressionSyntax value)=>value.IsKind(SyntaxKind.NullLiteralExpression)||value is ParenthesizedExpressionSyntax parent&&IsNull(parent.Expression);
    private static string Access(SyntaxTokenList tokens)=>tokens.Any(SyntaxKind.PublicKeyword)?"public":tokens.Any(SyntaxKind.ProtectedKeyword)?"protected":"private";

    public static IReadOnlyList<ActorFile> ToUnity(IReadOnlyList<ActorFile> headers)=>ToUnity(headers,false);
    internal static IReadOnlyList<ActorFile> ToUnity(IReadOnlyList<ActorFile> headers,bool useWeakGameObjects)
    {
        var startQueueVersions=new HashSet<int>();
        var output=new List<ActorFile>();var weakClasses=new HashSet<string>();var weakComponentClasses=new HashSet<string>();var weakTransformClasses=new HashSet<string>();var weakUIClasses=new HashSet<string>();var uiOwnerNameClasses=new HashSet<string>();var fixedClockVersions=new HashSet<int>();var scaleClockVersions=new HashSet<int>();var lateClockVersions=new HashSet<int>();
        foreach(var file in headers)
        {
            var meta=Regex.Match(file.Code,@"^// EM_ACTOR ([^\r\n]+)");if(!meta.Success)throw new NotSupportedException("EngineMigrator Actor 헤더 형식만 지원합니다.");
            using var info=JsonDocument.Parse(meta.Groups[1].Value);var name=N(info.RootElement.GetProperty("name").GetString()!);var parent=info.RootElement.GetProperty("parent").GetString()!;
            if(info.RootElement.TryGetProperty("weakGameObjects",out var weakFlag)&&weakFlag.ValueKind==JsonValueKind.True)weakClasses.Add(name);
            var componentWeak=info.RootElement.TryGetProperty("weakComponents",out var componentFlag)&&componentFlag.ValueKind==JsonValueKind.True;
            var transformWeak=info.RootElement.TryGetProperty("weakTransforms",out var transformFlag)&&transformFlag.ValueKind==JsonValueKind.True;
            var uiWeak=info.RootElement.TryGetProperty("weakUI",out var uiFlag)&&uiFlag.ValueKind==JsonValueKind.True;
            var startQueue=info.RootElement.TryGetProperty("startQueue",out var queueFlag)?queueFlag.GetInt32():0;
            startQueueVersions.Add(startQueue);
            if(startQueue is not(0 or 1))throw new NotSupportedException("알 수 없는 시작 대기열 버전입니다.");
            if(file.Code.Contains("FEMStartRuntime",StringComparison.Ordinal)&&startQueue!=1)throw new NotSupportedException("시작 대기열 메타데이터가 누락되었습니다.");
            if(startQueue==1&&parent=="MonoBehaviour"){
                var normalized=file.Code.Replace("\r\n","\n").Replace("\r","\n");
                foreach(var required in new[]{StartQueuePorting.Includes,StartQueuePorting.Runtime,StartQueuePorting.Helpers,StartQueuePorting.DeferStart,StartQueuePorting.Startup,StartQueuePorting.TickPrefix,LateUpdatePorting.TickOrder})if(!normalized.Contains(required.Replace("\r\n","\n").Replace("\r","\n"),StringComparison.Ordinal))throw new NotSupportedException("시작 대기열 실행 코드가 변경되었습니다.");
            }
            var lateBackend=info.RootElement.TryGetProperty("lateUpdate",out var lateFlag)&&lateFlag.ValueKind==JsonValueKind.True;
            if(file.Code.Contains("EM_LateUpdate(",StringComparison.Ordinal)&&!lateBackend)throw new NotSupportedException("후기 업데이트 백엔드 메타데이터가 누락되었습니다.");
            var lateVersion=lateBackend?(info.RootElement.TryGetProperty("lateClock",out var lateVersionFlag)?lateVersionFlag.GetInt32():1):0;
            if(lateBackend){if(lateVersion is not(1 or 2 or 3))throw new NotSupportedException("알 수 없는 후기 실행 버전입니다.");lateClockVersions.Add(lateVersion);}
            if(lateBackend&&parent=="MonoBehaviour"){
                var normalized=file.Code.Replace("\r\n","\n").Replace("\r","\n");
                if(!normalized.Contains((lateVersion==1?LateUpdatePorting.LegacyRuntime:lateVersion==2?LateUpdatePorting.PreviousRuntime:LateUpdatePorting.Runtime).Replace("\r\n","\n").Replace("\r","\n"),StringComparison.Ordinal)||!normalized.Contains((lateVersion==1?LateUpdatePorting.LegacyHelpers:lateVersion==2?LateUpdatePorting.PreviousHelpers:LateUpdatePorting.Helpers).Replace("\r\n","\n").Replace("\r","\n"),StringComparison.Ordinal))throw new NotSupportedException("후기 업데이트 실행 코드가 변경되었습니다.");
                if(lateVersion==2&&(!normalized.Contains(LateUpdatePorting.BeforeStart,StringComparison.Ordinal)||!normalized.Contains(LateUpdatePorting.BeforeDestroy,StringComparison.Ordinal)))throw new NotSupportedException("후기 실행의 생성·삭제 예약 코드가 변경되었습니다.");
                if(lateVersion==3&&(!normalized.Contains(LateUpdatePorting.DeferStart,StringComparison.Ordinal)||!normalized.Contains(LateUpdatePorting.TickOrder,StringComparison.Ordinal)||!normalized.Contains(LateUpdatePorting.EnableStamp,StringComparison.Ordinal)||!normalized.Contains(LateUpdatePorting.BeforeDestroy,StringComparison.Ordinal)))throw new NotSupportedException("후기 실행의 시작 대기열·재활성·삭제 코드가 변경되었습니다.");
            }
            var fixedBackend=info.RootElement.TryGetProperty("fixedUpdate",out var fixedFlag)&&fixedFlag.ValueKind==JsonValueKind.True;
            if(file.Code.Contains("EM_FixedUpdate(",StringComparison.Ordinal)&&!fixedBackend)throw new NotSupportedException("고정 이벤트 백엔드 메타데이터가 누락되었습니다.");
            var fixedVersion=fixedBackend?(info.RootElement.TryGetProperty("fixedClock",out var versionFlag)?versionFlag.GetInt32():1):0;
            if(fixedBackend){if(fixedVersion is not (1 or 2 or 3 or 4 or 5))throw new NotSupportedException("알 수 없는 고정 시계 버전입니다.");fixedClockVersions.Add(fixedVersion);}
            var scaleBackend=info.RootElement.TryGetProperty("timeScale",out var scaleFlag)&&scaleFlag.ValueKind==JsonValueKind.True;
            if(file.Code.Contains("EM_TimeScale(",StringComparison.Ordinal)&&!scaleBackend)throw new NotSupportedException("시간 배율 백엔드 메타데이터가 누락되었습니다.");
            var scaleVersion=scaleBackend?(info.RootElement.TryGetProperty("timeScaleClock",out var scaleVersionFlag)?scaleVersionFlag.GetInt32():1):0;
            if(scaleBackend){if(scaleVersion is not(1 or 2))throw new NotSupportedException("알 수 없는 시간 배율 실행 버전입니다.");scaleClockVersions.Add(scaleVersion);}
            if(scaleBackend&&parent=="MonoBehaviour"){
                var normalized=file.Code.Replace("\r\n","\n").Replace("\r","\n");
                if(!normalized.Contains((scaleVersion==1?TimeScalePorting.LegacyRuntime:TimeScalePorting.Runtime).Replace("\r\n","\n").Replace("\r","\n"),StringComparison.Ordinal)||!normalized.Contains(TimeScalePorting.Helpers.Replace("\r\n","\n").Replace("\r","\n"),StringComparison.Ordinal))throw new NotSupportedException("시간 배율 실행 코드가 변경되었습니다.");
            }
            if(fixedVersion==5&&lateBackend&&lateVersion!=3)throw new NotSupportedException("고정 단계 시작 경계에는 현재 후기 실행 백엔드가 필요합니다.");
            if(fixedBackend&&parent=="MonoBehaviour"){
                var normalized=file.Code.Replace("\r\n","\n").Replace("\r","\n");
                if(!normalized.Contains((fixedVersion==1?FixedUpdatePorting.LegacyRuntime:fixedVersion==2?FixedUpdatePorting.PreviousRuntime:fixedVersion==3?FixedUpdatePorting.StartupRuntime:fixedVersion==4?FixedUpdatePorting.FourthRuntime:FixedUpdatePorting.Runtime).Replace("\r\n","\n").Replace("\r","\n"),StringComparison.Ordinal)||!normalized.Contains((fixedVersion==5?FixedUpdatePorting.CurrentHelpers(lateBackend):FixedUpdatePorting.Helpers).Replace("\r\n","\n").Replace("\r","\n"),StringComparison.Ordinal))throw new NotSupportedException("고정 이벤트 실행 코드가 누락되거나 변경되었습니다.");
            }

            if(info.RootElement.TryGetProperty("uiOwnerNames",out var nameFlag)&&nameFlag.ValueKind==JsonValueKind.True){uiOwnerNameClasses.Add(name);if(parent=="MonoBehaviour"&&!file.Code.Replace("\r\n","\n").Replace("\r","\n").Contains(UiNamePorting.Helpers.Replace("\r\n","\n").Replace("\r","\n"),StringComparison.Ordinal))throw new NotSupportedException("UI 이름의 소유 객체 대응 코드가 누락되었습니다.");}
            if(uiWeak){if(!weakClasses.Contains(name))throw new NotSupportedException("UI 약한 참조에는 GameObject 약한 참조 런타임이 필요합니다.");weakUIClasses.Add(name);}
            foreach(Match alias in Regex.Matches(file.Code,@"using EM_UIRef_(?:Canvas|Button)=(TWeakObjectPtr<AActor>|AActor\*);"))
                if(alias.Groups[1].Value!=(uiWeak?"TWeakObjectPtr<AActor>":"AActor*"))throw new NotSupportedException("UI 참조 별칭과 백엔드 메타데이터가 다릅니다.");
            if(transformWeak){if(!weakClasses.Contains(name))throw new NotSupportedException("Transform 약한 참조에는 GameObject 약한 참조 런타임이 필요합니다.");weakTransformClasses.Add(name);}
            foreach(Match alias in Regex.Matches(file.Code,@"using EM_TransformRef=(TWeakObjectPtr<AActor>|AActor\*);"))
                if(alias.Groups[1].Value!=(transformWeak?"TWeakObjectPtr<AActor>":"AActor*"))throw new NotSupportedException("Transform 참조 별칭과 백엔드 메타데이터가 다릅니다.");
            if(componentWeak){if(!weakClasses.Contains(name))throw new NotSupportedException("컴포넌트 약한 참조에는 GameObject 약한 참조 런타임이 필요합니다.");weakComponentClasses.Add(name);}
            foreach(Match alias in Regex.Matches(file.Code,@"using EM_ComponentRef_[A-Za-z][A-Za-z0-9_]*=(TWeakObjectPtr<AActor>|AActor\*);"))
                if(alias.Groups[1].Value!=(componentWeak?"TWeakObjectPtr<AActor>":"AActor*"))throw new NotSupportedException("컴포넌트 참조 별칭과 백엔드 메타데이터가 다릅니다.");
            var text="using UnityEngine;\npublic class "+name+" : "+N(parent)+"\n{\n";
            foreach(Match match in Regex.Matches(file.Code,@"// EM_FIELD ([^\r\n]+)\r?\n[^\n]+\nUPROPERTY\((?:EditAnywhere, Category=""EngineMigrator""(?:, meta=\(AllowPrivateAccess=""true""\))?)?\)\r?\n[^=\n]+ = ([^;]+);"))
            {
                var field=JsonSerializer.Deserialize<MemberInfo>(match.Groups[1].Value)!;if(field.Flags=="SerializeField")text+="[SerializeField]\n";else if(field.Flags!="")throw new NotSupportedException("알 수 없는 필드 직렬화 속성");text+=field.Access+" "+field.Type+" "+N(field.Name)+" = "+RestoreIntExpression(match.Groups[2].Value)+";\n";
                var declaration=match.Value.Split('\n').Last().Split('=')[0];if(Regex.Match(declaration,@"([A-Za-z_][A-Za-z0-9_]*)\s*$").Groups[1].Value!=NativeField(field.Name))throw new NotSupportedException("메타데이터와 다른 네이티브 필드 이름");
            }
            foreach(Match match in Regex.Matches(file.Code,@"// EM_METHOD ([^\r\n]+)\r?\n[^\n]+\n(?:UFUNCTION\((?:BlueprintCallable, Category=""EngineMigrator"")?\)\r?\n)?[^\n{]+(?<body>\{[\s\S]*?)\r?\n// EM_END_METHOD"))
            {
                var method=JsonSerializer.Deserialize<MemberInfo>(match.Groups[1].Value)!;var body=match.Groups["body"].Value.Replace("this->","this.").Replace("Super::EM_Update(DeltaSeconds)","base.Update()").Replace("Super::EM_FixedUpdate(DeltaSeconds)","base.FixedUpdate()").Replace("Super::EM_LateUpdate(DeltaSeconds)","base.LateUpdate()");
                if(weakClasses.Contains(name))
                {
                    var prefix=WeakGameObjectPorting.ParameterCopyPrefix(SyntaxFactory.ParseParameterList("("+method.Parameters+")").Parameters.Where(p=>p.Type?.ToString()=="GameObject"||(uiWeak&&UiTypes.Contains(p.Type?.ToString()??""))||(transformWeak&&p.Type?.ToString()=="Transform")||(componentWeak&&p.Type is not null&&!Types.Contains(p.Type.ToString()))).Select(p=>N(p.Identifier.ValueText,true)));
                    if(!body.StartsWith("{"+prefix,StringComparison.Ordinal))throw new NotSupportedException("GC 참조 인수의 값 복사 코드가 변경되어 역변환할 수 없습니다.");
                    body="{"+body[(1+prefix.Length)..];
                }
                var declaration=match.Value.Split('\n').First(l=>l.Contains('(')&&!l.TrimStart().StartsWith("//")&&!l.TrimStart().StartsWith("UFUNCTION("));
                if(Regex.Match(declaration,@"\b([A-Za-z_][A-Za-z0-9_]*)\s*\(").Groups[1].Value!=(Events.Contains(method.Name)?"EM_":"")+method.Name)throw new NotSupportedException("메타데이터와 다른 네이티브 메서드 이름");
                foreach(var ev in Events)body=body.Replace("Super::EM_"+ev+"(","base."+ev+"(");body=body.Replace("Super::","base.").Replace("DeltaSeconds","Time.deltaTime");
                body=RestoreIntExpression(body);
                text+=method.Access+" "+(method.Flags.Length>0?method.Flags+" ":"")+method.Type+" "+N(method.Name)+"("+method.Parameters+") "+body+"\n";
            }
            if(Regex.IsMatch(text,@"\bButton\b"))text="using UnityEngine.UI;\n"+text;
            output.Add(new(name+".cs",text+"}\n"));
        }
        if(scaleClockVersions.Count>1)throw new NotSupportedException("서로 다른 시간 배율 실행 버전의 헤더를 혼합할 수 없습니다.");
        if(startQueueVersions.Count>1)throw new NotSupportedException("서로 다른 시작 대기열 버전의 헤더를 혼합할 수 없습니다.");
        if(lateClockVersions.Count>1)throw new NotSupportedException("서로 다른 후기 실행 버전의 헤더를 혼합할 수 없습니다.");
        if(fixedClockVersions.Count>1)throw new NotSupportedException("서로 다른 고정 시계 버전의 헤더를 혼합할 수 없습니다.");
        var combined="using UnityEngine;\nusing UnityEngine.UI;\n"+string.Join("\n",output.Select(f=>f.Code.Replace("using UnityEngine;","").Replace("using UnityEngine.UI;","")));
        var recreated=FromUnityCore(combined,useWeakGameObjects,weakClasses.Count>0?weakClasses:null,false,false,weakComponentClasses.Count>0?weakComponentClasses:null,false,weakTransformClasses.Count>0?weakTransformClasses:null,false,weakUIClasses.Count>0?weakUIClasses:null,uiOwnerNameClasses);
        static string Canon(string value)
        {
            var lateClass=Regex.Match(value,@"// EM_ACTOR .*?""name"":""([^""]+)""").Groups[1].Value;
            if(lateClass.Length>0)value=value.CanonReplace(LateUpdatePorting.DeferStart,LateUpdatePorting.LegacyStartHelper(lateClass));
            if(lateClass.Length>0)value=value.CanonReplace(StartQueuePorting.DeferStart,LateUpdatePorting.LegacyStartHelper(lateClass));
            value=value.CanonReplace(StartQueuePorting.Runtime,"").CanonReplace(StartQueuePorting.Helpers,"").CanonReplace(StartQueuePorting.TickPrefix,"").CanonReplace(StartQueuePorting.Startup,StartQueuePorting.OldStartup).CanonReplace(StartQueuePorting.BeginSuffix,"");
            value=value.CanonReplace(LateUpdatePorting.TickOrder,LateUpdatePorting.PreviousTickOrder).CanonReplace(LateUpdatePorting.EnableStamp,"");
            value=value.CanonReplace(LateUpdatePorting.BeforeStart,"const bool EM_BeforeUpdate=!GetWorld()->bInTick;").CanonReplace(LateUpdatePorting.BeforeDestroy,"const bool EM_NextPre=EM_IsPostStartDispatch();");
            value=value.CanonReplace(LateUpdatePorting.LegacyRuntime,"").CanonReplace(LateUpdatePorting.LegacyHelpers,"").CanonReplace(LateUpdatePorting.PreviousRuntime,"").CanonReplace(LateUpdatePorting.PreviousHelpers,"").CanonReplace(LateUpdatePorting.Runtime,"").CanonReplace(LateUpdatePorting.Helpers,"").CanonReplace(LateUpdatePorting.BeginSuffix,"");
            value=value.CanonReplace(TimeScalePorting.Includes,"").CanonReplace(TimeScalePorting.LegacyRuntime,"").CanonReplace(TimeScalePorting.Runtime,"").CanonReplace(TimeScalePorting.Helpers,"").CanonReplace(TimeScalePorting.BeginSuffix,"");
            value=value.CanonReplace(FixedUpdatePorting.Includes,"").CanonReplace(FixedUpdatePorting.LegacyRuntime,"").CanonReplace(FixedUpdatePorting.PreviousRuntime,"").CanonReplace(FixedUpdatePorting.StartupRuntime,"").CanonReplace(FixedUpdatePorting.FourthRuntime,"").CanonReplace(FixedUpdatePorting.Runtime,"").CanonReplace(FixedUpdatePorting.CurrentHelpers(false),"").CanonReplace(FixedUpdatePorting.CurrentHelpers(true),"").CanonReplace(FixedUpdatePorting.Helpers,"").CanonReplace(FixedUpdatePorting.BeginSuffix,"");
            value=value.CanonReplace(UiNamePorting.Helpers,"");
            value=value.CanonReplace(DynamicUiPorting.RuntimeHelpers,DynamicUiPorting.PreviousHelpers);
            value=WeakUiPorting.RestoreRuntime(WeakUiPorting.Restore(value)).CanonReplace(WeakUiPorting.Helpers,"").CanonReplace(WeakUiPorting.LegacyHelpers,"");
            value=value.Replace("using EM_TransformRef=TWeakObjectPtr<AActor>;","using EM_TransformRef=AActor*;",StringComparison.Ordinal);
            value=WeakGameObjectPorting.RestoreRuntime(value).CanonReplace(WeakGameObjectPorting.Helpers,"");
            var className=Regex.Match(value,@"class A([A-Za-z][A-Za-z0-9_]*) :").Groups[1].Value;
            value=value.CanonReplace(RuntimeObjectPorting.SceneIndexHelper,"").CanonReplace(BitHelpers,"").CanonReplace(ReferenceValueHelper,"").CanonReplace(UiComponentPorting.RuntimeHelpers,"");
            value=value.CanonReplace(RuntimeUiPrefabPorting.RuntimeHelpers,"").CanonReplace(RuntimeUiPrefabPorting.Helpers(className).CanonReplace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()"),"").CanonReplace(RuntimeUiPrefabPorting.Helpers(className),"");
            value=value.CanonReplace(RuntimeAnimationPrefabPorting.RuntimeHelpers,"").CanonReplace(RuntimeAnimationPrefabPorting.Includes,"").CanonReplace(RuntimeAnimationPrefabPorting.Helpers(className).CanonReplace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()"),"").CanonReplace(RuntimeAnimationPrefabPorting.Helpers(className),"");
            value=value.CanonReplace("PrimaryActorTick.TickGroup = TG_PostPhysics; ","");
            value=value.CanonReplace(RuntimePhysicsPrefabPorting.RuntimeHelpers,"").CanonReplace(RuntimePhysicsPrefabPorting.Includes,"").CanonReplace(RuntimePhysicsPrefabPorting.Helpers(className).CanonReplace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()"),"").CanonReplace(RuntimePhysicsPrefabPorting.Helpers(className),"");
            value=value.CanonReplace(WholeObjectDestructionPorting.Helpers(className).CanonReplace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()"),"").CanonReplace(WholeObjectDestructionPorting.Helpers(className),"").CanonReplace(WholeObjectDestructionPorting.RuntimeHelpers,"");
            value=value.CanonReplace(StartupLifecyclePorting.Helpers(className).CanonReplace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()"),"").CanonReplace(StartupLifecyclePorting.Helpers(className),"").CanonReplace(StartupLifecyclePorting.RuntimeHelpers,"").CanonReplace(StartupLifecyclePorting.InitializeBody,"Super::PostInitializeComponents(); EM_Initialized = true; EM_Awake();").CanonReplace(StartupLifecyclePorting.BeginPlayBody,"Super::BeginPlay(); EM_Start();");
            value=value.CanonReplace(PhaseLifecyclePorting.Helpers(className).CanonReplace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()"),"").CanonReplace(PhaseLifecyclePorting.Helpers(className),"");
            value=value.CanonReplace(ObjectActivationPorting.Helpers(className).CanonReplace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()"),"").CanonReplace(ObjectActivationPorting.Helpers(className),"").CanonReplace(ObjectActivationPorting.CreationHelpers,"").CanonReplace(ObjectActivationPorting.ComponentHelpers,"").CanonReplace(ObjectActivationPorting.BeginPlayBody,"Super::BeginPlay(); EM_Start();").CanonReplace(ObjectActivationPorting.TickBody,"Super::Tick(DeltaSeconds); EM_Update(DeltaSeconds);");
            value=value.CanonReplace(BehaviourActivationPorting.Helpers(className).CanonReplace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()"),"");
            value=value.CanonReplace(BehaviourActivationPorting.Helpers(className),"").CanonReplace(BehaviourActivationPorting.InitializeBody,"Super::PostInitializeComponents(); EM_Initialized = true; EM_Awake();").CanonReplace(BehaviourActivationPorting.BeginPlayBody,"Super::BeginPlay(); EM_Start();").CanonReplace(BehaviourActivationPorting.TickBody,"Super::Tick(DeltaSeconds); EM_Update(DeltaSeconds);").CanonReplace(BehaviourActivationPorting.Finalize,FinalizeHelper);
            value=value.CanonReplace("virtual void EM_OnEnable() {}\n","").CanonReplace("virtual void EM_OnDisable() {}\n","");
            value=value.CanonReplace(RuntimeComponentPorting.Lifecycle(className),"").CanonReplace(RuntimeComponentPorting.BeginPlayBody,"Super::BeginPlay(); EM_Start();").CanonReplace(RuntimeComponentPorting.TickBody,"Super::Tick(DeltaSeconds); EM_Update(DeltaSeconds);");
            value=value.CanonReplace(ObjectHelper,"").CanonReplace(LegacyObjectHelper,"").CanonReplace(TransformHelpers,"").CanonReplace(LegacyTransformHelpers,"");
            value=value.CanonReplace(BehaviourActivationPorting.RuntimeHelpers,"").CanonReplace(RuntimeComponentPorting.Helpers,"").CanonReplace(RuntimeCreationPorting.Helpers,"").CanonReplace(RuntimeObjectPorting.Includes,"").CanonReplace(RuntimeObjectPorting.LegacyIncludes,"").CanonReplace(RuntimeComponentPorting.Includes,"");
            value=value.CanonReplace(TransformAdditions,"").CanonReplace(RuntimeObjectPorting.PlacementHelpers,"").CanonReplace(RuntimeObjectPorting.BaseHelpers,"").CanonReplace(RuntimeObjectPorting.Helpers,"").CanonReplace(RuntimeObjectPorting.PreviousHelpers,"").CanonReplace(RuntimeObjectPorting.LegacyHelpers,"");
            value=value.CanonReplace("if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))){EM_Initialized = true; EM_Awake();}","EM_Initialized = true; EM_Awake();").CanonReplace("if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))) EM_Start(); else SetActorTickEnabled(false);","EM_Start();").CanonReplace("if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))) EM_Start();","EM_Start();").CanonReplace("if(!Tags.Contains(TEXT(\"EMRuntimeTemplate\"))) EM_Update(DeltaSeconds);","EM_Update(DeltaSeconds);");
            // Accept earlier generated boilerplate without weakening validation of
            // user declarations, methods, fields, or arbitrary native statements.
            value=value.CanonReplace("#include \"Components/SceneComponent.h\"","").CanonReplace("RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT(\"EM_Root\"));","").CanonReplace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()");
            value=value.CanonReplace(IntHelpers,LegacyIntHelpers).CanonReplace(PreviousIntHelpers,LegacyIntHelpers).CanonReplace(TransformHelpers,LegacyTransformHelpers).CanonReplace(ObjectHelper,LegacyObjectHelper);
            value=value.CanonReplace(FinalizeHelper,"").CanonReplace(EndPlayWrapper,"").CanonReplace(LegacyEndPlayWrapper,"").CanonReplace("EM_Initialized = true; EM_Awake();","EM_Awake();").CanonReplace("EM_FinalizeDestroy(); Super::Destroyed();","EM_OnDestroy(); Super::Destroyed();");
            value=Regex.Replace(value,@"using EM_ComponentRef_[A-Za-z][A-Za-z0-9_]*=(?:AActor\*|TWeakObjectPtr<AActor>);","");
            value=Regex.Replace(value,@"\bEM_(?:arg|local)_([A-Za-z][A-Za-z0-9_]*)\b","$1");
            value=Regex.Replace(Regex.Replace(value,@"//[^\r\n]*",""),@"\s+","");
            foreach(var property in new[]{"Position","LocalPosition","Rotation","LocalRotation","Parent"})value=value.CanonReplace("EM_Read"+property+"(EM_SelfTransform())","EM_Get"+property+"()");
            foreach(var property in new[]{"Position","LocalPosition"})value=value.CanonReplace("EM_Write"+property+"(EM_SelfTransform(),","EM_Set"+property+"(");
            return value.CanonReplace("EM_DestroyGameObject(","EM_Destroy(");
        }
        foreach(var input in headers){var actual=recreated.SingleOrDefault(f=>f.Name==input.Name);if(actual is null||Canon(actual.Code)!=Canon(input.Code))throw new NotSupportedException("지원 문법 외 Actor 코드/변경된 선언을 발견했습니다: "+input.Name);}
        return output;
    }
    // Generated helpers from older/newer source files can have different line endings.
    // Normalize both operands before matching; method/body validation remains exact
    // under the canonical whitespace rules below.
    private static string CanonReplace(this string text,string from,string to)=>text.Replace("\r\n","\n").Replace("\r","\n").Replace(from.Replace("\r\n","\n").Replace("\r","\n"),to.Replace("\r\n","\n").Replace("\r","\n"));
    private static string RestoreIntExpression(string code)
    {
        code=Regex.Replace(code,@"\bEM_Field_Owner\b","Owner");
        code=Regex.Replace(code,@"\bTWeakObjectPtr\s*<\s*AActor\s*>","GameObject");
        code=Regex.Replace(code,@"\bstatic_cast<(float|double|EM_TransformRef)>\s*", "($1)");
        code=Regex.Replace(Regex.Replace(Regex.Replace(code,@"\bint32\b","int"),@"\bAActor\s*\*","GameObject "),@"\bnullptr\b","null");
        code=Regex.Replace(code,@"\bEM_(?:arg|local)_([A-Za-z][A-Za-z0-9_]*)\b","$1");
        code=Regex.Replace(code,"EM_LoadScene\\(TEXT\\(\"([^\"]*)\"\\)\\)",m=>"UnityEngine.SceneManagement.SceneManager.LoadScene("+(m.Groups[1].Value.StartsWith('#')?m.Groups[1].Value[1..]:"\""+m.Groups[1].Value+"\"")+")");
        code=code.Replace("EM_DestroyCanvas(","Destroy(").Replace("EM_DestroyButton(","Destroy(").Replace("EM_Instantiate(","Instantiate(").Replace("EM_DestroyGameObject(","Destroy(").Replace("EM_Destroy(","Destroy(").Replace("EM_TransformOwner()","gameObject");
        code=code.Replace("FVector3f::ZeroVector","Vector3.zero");code=Regex.Replace(code,@"\bFVector3f\s*\(","new Vector3(");code=Regex.Replace(code,@"\bFVector3f\b","Vector3");
        code=Regex.Replace(code,@"\bEM_UIRef_(Canvas|Button)\b","$1");
        code=Regex.Replace(code,@"\bEM_ComponentRef_([A-Za-z][A-Za-z0-9_]*)\b","$1");code=Regex.Replace(code,@"\bEM_TransformRef\b","Transform");code=Regex.Replace(code,@"\bFVector4f\s*\(","new Quaternion(");code=Regex.Replace(code,@"\bFVector4f\b","Quaternion");
        var syntax=CSharpSyntaxTree.ParseText(code).GetRoot();return new IntRestore().Visit(syntax)!.ToFullString();
    }
    private sealed class IntRestore:CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var rewritten=(InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;var name=rewritten.Expression.ToString();var args=rewritten.ArgumentList.Arguments;
            if(name is "EM_UiGetCanvas" or "EM_UiGetButton"&&args.Count==1){var type=name=="EM_UiGetCanvas"?"Canvas":"Button";return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.GenericName(SyntaxFactory.Identifier("GetComponent"),SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList<TypeSyntax>(SyntaxFactory.IdentifierName(type))))),SyntaxFactory.ArgumentList());}
            if(name=="EM_ReferenceValue"&&args.Count==1)return args[0].Expression;
            if(name=="EM_TimeScale"&&args.Count==0)return SyntaxFactory.ParseExpression("Time.timeScale");
            if(name=="EM_SetTimeScale"&&args.Count==1)return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,SyntaxFactory.ParseExpression("Time.timeScale"),args[0].Expression);
            if(name=="EM_FixedStep"&&args.Count==0)return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,SyntaxFactory.IdentifierName("Time"),SyntaxFactory.IdentifierName("fixedDeltaTime"));
            if(name=="EM_NativeGameObject"&&args.Count==1)return args[0].Expression;
            if(name=="EM_GameObjectEqual"&&args.Count==2)return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression,args[0].Expression,args[1].Expression));
            if(name is "EM_UiAddCanvas" or "EM_UiAddButton"&&args.Count==1)return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.GenericName(SyntaxFactory.Identifier("AddComponent"),SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList<TypeSyntax>(SyntaxFactory.IdentifierName(name=="EM_UiAddCanvas"?"Canvas":"Button"))))),SyntaxFactory.ArgumentList());
            if(name=="EM_UiOwner"&&args.Count==1)return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName("gameObject"));
            if(name=="EM_UIValid"&&args.Count==2)return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression,args[0].Expression,SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)));
            if(name=="EM_UIEqual"&&args.Count==4)return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression,args[0].Expression,args[2].Expression));
            if(name.StartsWith("EM_UiRead")||name.StartsWith("EM_UiWrite")){var uiName=name.EndsWith("Interactable")?"interactable":"enabled";var member=SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName(uiName));return name.StartsWith("EM_UiRead")?member:SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,member,args[1].Expression);}
            if(name=="EM_ReadBehaviourEnabled"&&args.Count==1)return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName("enabled"));
            if(name is "EM_ObjectActiveSelf" or "EM_ObjectActiveInHierarchy"&&args.Count==1)return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName(name=="EM_ObjectActiveSelf"?"activeSelf":"activeInHierarchy"));
            if(name=="EM_SetObjectActive"&&args.Count==2)return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName("SetActive")),SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(args[1])));
            if(name=="EM_WriteBehaviourEnabled"&&args.Count==2)return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName("enabled")),args[1].Expression);
            if(rewritten.Expression is GenericNameSyntax generic&&generic.Identifier.ValueText is "EM_ComponentRead" or "EM_ComponentWrite"&&args.Count>=2&&args[1].Expression is LiteralExpressionSyntax{Token.Value:string fieldName}){
                var member=SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName(fieldName));
                return generic.Identifier.ValueText=="EM_ComponentRead"?member:SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,member,args[2].Expression);
            }
            if(rewritten.Expression is GenericNameSyntax callGeneric&&callGeneric.Identifier.ValueText=="EM_ComponentCall"&&args.Count>=2&&args[1].Expression is LiteralExpressionSyntax{Token.Value:string methodName})return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName(methodName)),SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args.Skip(2))));
            if(name is "EM_AddComponent" or "EM_GetComponent"&&args.Count==2&&args[1].Expression is LiteralExpressionSyntax{Token.Value:string classPath}){
                var componentName=classPath.Split('.').Last();var method=SyntaxFactory.GenericName(SyntaxFactory.Identifier(name=="EM_AddComponent"?"AddComponent":"GetComponent"),SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList<TypeSyntax>(SyntaxFactory.IdentifierName(componentName))));
                return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,method),SyntaxFactory.ArgumentList());
            }
            if(name=="EM_ComponentOwner"&&args.Count==1)return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName("gameObject"));
            if(name=="EM_ComponentEqual"&&args.Count==2)return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression,args[0].Expression,args[1].Expression));
            if(name=="TEXT"&&args.Count==1&&args[0].Expression is LiteralExpressionSyntax{Token.Value:string})return args[0].Expression;
            if(name=="EM_NewGameObject"&&args.Count==1)return SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName("GameObject")).WithArgumentList(rewritten.ArgumentList).WithNewKeyword(SyntaxFactory.Token(SyntaxKind.NewKeyword).WithTrailingTrivia(SyntaxFactory.Space));
            if(name is "EM_SetObjectName" or "EM_UiSetObjectName"&&args.Count==2)return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName("name")),args[1].Expression);
            if(name=="EM_SetParent"&&args.Count==3)return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName("SetParent")),SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args.Skip(1))));
            var property=name switch{"EM_ReadPosition" or "EM_WritePosition"=>"position","EM_ReadLocalPosition" or "EM_WriteLocalPosition"=>"localPosition","EM_ReadLocalScale" or "EM_WriteLocalScale"=>"localScale","EM_ReadRotation" or "EM_WriteRotation"=>"rotation","EM_ReadLocalRotation" or "EM_WriteLocalRotation"=>"localRotation","EM_ReadParent"=>"parent",_=>null};
            if(property is not null&&args.Count==(name.StartsWith("EM_Read",StringComparison.Ordinal)?1:2)){
                var member=SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName(property));
                return args.Count==1?member:SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,member,args[1].Expression);
            }
            if(name is "EM_InstantiateParent" or "EM_InstantiateParentWorld" or "EM_InstantiatePose" or "EM_InstantiatePoseParent")return rewritten.WithExpression(SyntaxFactory.IdentifierName("Instantiate"));
            if(name=="EM_SelfTransform"&&args.Count==0)return SyntaxFactory.IdentifierName("transform");
            if(name=="EM_GetParent"&&args.Count==0)return SyntaxFactory.ParseExpression("transform.parent");
            if(name=="EM_ObjectTransform"&&args.Count==1)return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,args[0].Expression,SyntaxFactory.IdentifierName("transform"));
            if(name is "EM_GetRotation" or "EM_GetLocalRotation"&&args.Count==0)return SyntaxFactory.ParseExpression(name=="EM_GetRotation"?"transform.rotation":"transform.localRotation");
            if(name is "EM_GetPosition" or "EM_GetLocalPosition"&&args.Count==0)return SyntaxFactory.ParseExpression(name=="EM_GetPosition"?"transform.position":"transform.localPosition");
            if(name is "EM_SetPosition" or "EM_SetLocalPosition"&&args.Count==1)return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,SyntaxFactory.ParseExpression(name=="EM_SetPosition"?"transform.position":"transform.localPosition"),args[0].Expression);
            if(name=="EM_ObjectValid"&&args.Count==1)return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression,args[0].Expression,SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)));
            if(name=="EM_LoadSceneIndex"&&args.Count==1)return rewritten.WithExpression(SyntaxFactory.ParseExpression("UnityEngine.SceneManagement.SceneManager.LoadScene"));
            if(name=="EM_IntNot"&&args.Count==1)return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.PrefixUnaryExpression(SyntaxKind.BitwiseNotExpression,args[0].Expression));
            if(name=="EM_IntNeg"&&args.Count==1)return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,args[0].Expression));
            var kind=name switch{"EM_IntAdd"=>SyntaxKind.AddExpression,"EM_IntSub"=>SyntaxKind.SubtractExpression,"EM_IntMul"=>SyntaxKind.MultiplyExpression,"EM_IntAnd"=>SyntaxKind.BitwiseAndExpression,"EM_IntOr"=>SyntaxKind.BitwiseOrExpression,"EM_IntXor"=>SyntaxKind.ExclusiveOrExpression,"EM_IntShiftLeft"=>SyntaxKind.LeftShiftExpression,"EM_IntShiftRight"=>SyntaxKind.RightShiftExpression,_=>SyntaxKind.None};
            return kind!=SyntaxKind.None&&args.Count==2?SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(kind,args[0].Expression,args[1].Expression)):rewritten;
        }
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var value=(MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;return value.Expression is ParenthesizedExpressionSyntax&&value.Name.ToString() is "X" or "Y" or "Z" or "W"?value.WithName(SyntaxFactory.IdentifierName(value.Name.ToString().ToLowerInvariant())):value;
        }
    }
}






