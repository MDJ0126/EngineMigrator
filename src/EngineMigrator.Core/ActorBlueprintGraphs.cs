using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineMigrator.Core;

/// <summary>Actor event graphs represented as explicit input/state/branch/parent-call statements.</summary>
public static class ActorBlueprintGraphs
{
    public static string FromUnity(string source)
    {
        ActorScripts.FromUnity(source); // semantic checking precedes graph construction
        var classes = new JsonArray();
        var tree=CSharpSyntaxTree.ParseText(source);
        if(tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Any(m=>m.Identifier.ValueText is "FixedUpdate" or "LateUpdate"))throw new NotSupportedException("FixedUpdate는 고정 시계를 사용하는 네이티브 Actor 백엔드에서 처리합니다.");
        if(tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Any(m=>m.Identifier.ValueText is "OnEnable" or "OnDisable")||tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Any(i=>i.Identifier.ValueText is "enabled" or "SetActive" or "activeSelf" or "activeInHierarchy"))throw new NotSupportedException("활성 생명주기는 네이티브 Actor 백엔드에서 처리합니다.");
        var semantic=CSharpCompilation.Create("ActorGraphPrecision",[tree,CSharpSyntaxTree.ParseText(ActorScripts.ValidationStub)],[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).GetSemanticModel(tree);
        var declarations=tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
        // This graph backend uses double math. Preserve C# float intermediate rounding
        // by selecting the native Actor backend until typed float operators are available.
        if(declarations.SelectMany(c=>c.Members.OfType<MethodDeclarationSyntax>()).SelectMany(m=>m.DescendantNodes().OfType<ExpressionSyntax>()).Any(e=>
           (e is BinaryExpressionSyntax or PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax || e is AssignmentExpressionSyntax a&&!a.IsKind(SyntaxKind.SimpleAssignmentExpression))&&semantic.GetTypeInfo(e).Type?.SpecialType==SpecialType.System_Single))
            throw new NotSupportedException("Float intermediate arithmetic requires the native Actor backend.");
        int Depth(ClassDeclarationSyntax c){var name=c.BaseList!.Types.Single().Type.ToString();return name=="MonoBehaviour"?0:1+Depth(declarations.Single(d=>d.Identifier.ValueText==name));}
        foreach(var cls in declarations.OrderBy(Depth))
        {
            var fields=new JsonArray(); var events=new JsonArray();
            JsonObject Expr(ExpressionSyntax e)=>e switch {
                ParenthesizedExpressionSyntax p=>Expr(p.Expression),
                CastExpressionSyntax c when c.Type.ToString()=="double"=>Expr(c.Expression),
                LiteralExpressionSyntax l when l.Token.Value is bool b=>new(){["kind"]="const",["value"]=b},
                LiteralExpressionSyntax l when l.Token.Value is double or float or int=>new(){["kind"]="const",["value"]=Convert.ToDouble(l.Token.Value,CultureInfo.InvariantCulture)},
                IdentifierNameSyntax i=>new(){["kind"]="field",["name"]=i.Identifier.ValueText},
                MemberAccessExpressionSyntax m when m.Expression.ToString()=="this"=>new(){["kind"]="field",["name"]=m.Name.Identifier.ValueText},
                MemberAccessExpressionSyntax m when m.ToString()=="Time.deltaTime"=>new(){["kind"]="param",["name"]="DeltaSeconds"},
                PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.UnaryMinusExpression)&&u.Operand is LiteralExpressionSyntax l&&l.Token.Value is double or float or int=>new(){["kind"]="const",["value"]=-Convert.ToDouble(l.Token.Value,CultureInfo.InvariantCulture)},
                PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.UnaryMinusExpression)=>new(){["kind"]="binary",["op"]="-",["left"]=new JsonObject{["kind"]="const",["value"]=0d},["right"]=Expr(u.Operand)},
                BinaryExpressionSyntax b when new[]{"+","-","*",">","<","=="}.Contains(b.OperatorToken.Text)=>new(){["kind"]="binary",["op"]=b.OperatorToken.Text,["left"]=Expr(b.Left),["right"]=Expr(b.Right)},
                _=>throw new NotSupportedException("Actor Blueprint expression: "+e)
            };
            JsonArray Block(IEnumerable<StatementSyntax> statements)
            {
                var result=new JsonArray(); foreach(var s in statements)
                {
                    if(s is BlockSyntax block){foreach(var n in Block(block.Statements))result.Add(n!.DeepClone());continue;}
                    if(s is IfStatementSyntax branch){IEnumerable<StatementSyntax> Expand(StatementSyntax x)=>x is BlockSyntax b?b.Statements:[x];result.Add(new JsonObject{["kind"]="if",["condition"]=Expr(branch.Condition),["then"]=Block(Expand(branch.Statement)),["else"]=branch.Else is null?new JsonArray():Block(Expand(branch.Else.Statement))});continue;}
                    if(s is ExpressionStatementSyntax es)
                    {
                        if(es.Expression is InvocationExpressionSyntax call&&call.Expression is MemberAccessExpressionSyntax m&&m.Expression is BaseExpressionSyntax&&call.ArgumentList.Arguments.Count==0){if(m.Name.Identifier.ValueText is not ("Start" or "Update" or "OnDestroy"))throw new NotSupportedException("Custom parent callback calls are unsupported");result.Add(new JsonObject{["kind"]="parent",["event"]=m.Name.Identifier.ValueText});continue;}
                        ExpressionSyntax target,value;
                        if(es.Expression is AssignmentExpressionSyntax assign){target=assign.Left;value=assign.Right;if(!assign.IsKind(SyntaxKind.SimpleAssignmentExpression)){var op=assign.OperatorToken.Text.TrimEnd('=');var left=Expr(target);result.Add(new JsonObject{["kind"]="set",["name"]=Field(target),["value"]=new JsonObject{["kind"]="binary",["op"]=op,["left"]=left,["right"]=Expr(value)}});continue;}}
                        else if(es.Expression is PostfixUnaryExpressionSyntax post&&(post.IsKind(SyntaxKind.PostIncrementExpression)||post.IsKind(SyntaxKind.PostDecrementExpression))){target=post.Operand;result.Add(new JsonObject{["kind"]="set",["name"]=Field(target),["value"]=new JsonObject{["kind"]="binary",["op"]=post.IsKind(SyntaxKind.PostIncrementExpression)?"+":"-",["left"]=Expr(target),["right"]=new JsonObject{["kind"]="const",["value"]=1d}}});continue;}
                        else throw new NotSupportedException("Actor Blueprint statement: "+es);
                        result.Add(new JsonObject{["kind"]="set",["name"]=Field(target),["value"]=Expr(value)});continue;
                    }
                    throw new NotSupportedException("Actor Blueprint statement: "+s);
                }return result;
            }
            foreach(var member in cls.Members)
            {
                if(member is FieldDeclarationSyntax f)
                {
                    var type=f.Declaration.Type.ToString();if(type is not ("float" or "double" or "bool"))throw new NotSupportedException("Actor Blueprint field type: "+type);
                    foreach(var v in f.Declaration.Variables){var value=v.Initializer is null?(type=="bool"?(JsonNode)JsonValue.Create(false)!:JsonValue.Create(0d)!):Expr(v.Initializer.Value)["value"]?.DeepClone()??throw new NotSupportedException("Constant field initializer required");fields.Add(new JsonObject{["name"]=v.Identifier.ValueText,["type"]=type,["value"]=value,["access"]=f.Modifiers.FirstOrDefault(x=>x.Text is "public" or "private" or "protected").Text??"private",["serialized"]=f.AttributeLists.Count>0});}
                }
                else if(member is MethodDeclarationSyntax method)
                {
                    var name=method.Identifier.ValueText;var custom=name is not ("Start" or "Update" or "OnDestroy");if(name=="Awake"||method.Body is null||method.ReturnType.ToString()!="void"||method.ParameterList.Parameters.Count!=0||(custom&&(!method.Modifiers.Any(SyntaxKind.PublicKeyword)||method.Modifiers.Any(m=>m.IsKind(SyntaxKind.VirtualKeyword)||m.IsKind(SyntaxKind.OverrideKeyword)))))throw new NotSupportedException("Actor Blueprint event/callback: "+name);
                    events.Add(new JsonObject{["name"]=name,["custom"]=custom,["body"]=Block(method.Body.Statements)});
                }
                else throw new NotSupportedException("Actor Blueprint member: "+member.Kind());
            }
            classes.Add(new JsonObject{["name"]=cls.Identifier.ValueText,["parent"]=cls.BaseList!.Types.Single().Type.ToString(),["fields"]=fields,["events"]=events});
        }
        var json=new JsonObject{["schema"]=1,["parent"]="Actor",["classes"]=classes}.ToJsonString(new JsonSerializerOptions{WriteIndented=true});ToUnity(json);return json;
    }
    private static string Field(ExpressionSyntax e)=>e is IdentifierNameSyntax i?i.Identifier.ValueText:e is MemberAccessExpressionSyntax m&&m.Expression is ThisExpressionSyntax?m.Name.Identifier.ValueText:throw new NotSupportedException("Only self fields can be assigned");
    public static string ToUnity(string json)
    {
        var root=JsonNode.Parse(json)!;if(root["schema"]!.GetValue<int>()!=1||root["parent"]!.GetValue<string>()!="Actor")throw new NotSupportedException("Invalid Actor graph schema");
        (long Cost,int Path) Shape(JsonArray body){long cost=0;int path=0;for(int i=body.Count-1;i>=0;i--){var statement=body[i]!;if(statement["kind"]!.GetValue<string>()=="if"){var a=Shape(statement["then"]!.AsArray());var b=Shape(statement["else"]!.AsArray());cost=1+a.Cost+b.Cost+2*cost;path=1+Math.Max(a.Path,b.Path)+path;}else{cost++;path++;}if(cost>2048||path>90)throw new NotSupportedException("Actor graph exceeds bounded execution-shape budget");}return(cost,path);}
        string Name(JsonNode? n){var s=n!.GetValue<string>();if(!SyntaxFacts.IsValidIdentifier(s)||SyntaxFacts.GetKeywordKind(s)!=SyntaxKind.None)throw new NotSupportedException("Invalid graph identifier");return s;}
        string Expr(JsonNode n,int depth=0){if(depth>128)throw new NotSupportedException("Graph depth exceeded");return n["kind"]!.GetValue<string>() switch{
            "field"=>"this."+Name(n["name"]),"param" when Name(n["name"])=="DeltaSeconds"=>"Time.deltaTime",
            "const"=>n["value"]!.ToJsonString() is var s&&s is "true" or "false"?s:double.IsFinite(n["value"]!.GetValue<double>())?n["value"]!.GetValue<double>().ToString("R",CultureInfo.InvariantCulture)+"d":throw new NotSupportedException("Nonfinite literal"),
            "binary" when new[]{"+","-","*",">","<","=="}.Contains(n["op"]!.GetValue<string>())=>"("+Expr(n["left"]!,depth+1)+" "+n["op"]!.GetValue<string>()+" "+Expr(n["right"]!,depth+1)+")",
            _=>throw new NotSupportedException("Unsupported Actor graph expression")};}
        var text=new System.Text.StringBuilder("using UnityEngine;\n");
        foreach(var cls in root["classes"]!.AsArray())
        {
            var parent=Name(cls!["parent"]);text.Append("public class "+Name(cls["name"])+" : "+parent+" {\n");
            var types=cls["fields"]!.AsArray().ToDictionary(f=>Name(f!["name"]),f=>f!["type"]!.GetValue<string>());
            for(var ancestor=parent;ancestor!="MonoBehaviour";){var declaration=root["classes"]!.AsArray().Single(c=>Name(c!["name"])==ancestor)!;foreach(var f in declaration["fields"]!.AsArray())types.TryAdd(Name(f!["name"]),f!["type"]!.GetValue<string>());ancestor=Name(declaration["parent"]);}
            foreach(var f in cls["fields"]!.AsArray()){var type=f!["type"]!.GetValue<string>();if(type is not ("double" or "float" or "bool"))throw new NotSupportedException("Unsupported field type");var access=f["access"]?.GetValue<string>()??"public";if(access is not ("public" or "protected" or "private"))access="private";text.Append((f["serialized"]?.GetValue<bool>()==true?"[SerializeField] ":"")+access+" "+type+" "+Name(f["name"])+" = "+(type=="bool"?f["value"]!.ToJsonString():"("+type+")"+f["value"]!.GetValue<double>().ToString("R",CultureInfo.InvariantCulture)+"d")+";\n");}
            string Body(JsonArray statements,string ev,int depth=0){if(depth>128)throw new NotSupportedException("Graph depth exceeded");return string.Join("\n",statements.Select(s=>s!["kind"]!.GetValue<string>() switch{
                "set"=>"this."+Name(s["name"])+" = "+(types.GetValueOrDefault(Name(s["name"]))=="float"?"(float)":"")+Expr(s["value"]!)+";",
                "if"=>"if ("+Expr(s["condition"]!)+") { "+Body(s["then"]!.AsArray(),ev,depth+1)+" } else { "+Body(s["else"]!.AsArray(),ev,depth+1)+" }",
                "parent" when Name(s["event"])==ev&&parent!="MonoBehaviour"=>"base."+ev+"();",
                _=>throw new NotSupportedException("Unsupported Actor graph execution node") }));}
            foreach(var ev in cls["events"]!.AsArray()){Shape(ev!["body"]!.AsArray());var name=Name(ev["name"]);if(name is "FixedUpdate" or "LateUpdate")throw new NotSupportedException("일반 Blueprint 콜백을 Unity 고정 이벤트로 암묵 변환할 수 없습니다.");var custom=ev["custom"]?.GetValue<bool>()==true;if(!custom&&name is not ("Start" or "Update" or "OnDestroy"))throw new NotSupportedException("Unsupported Actor event");text.Append((custom?"public":"protected "+(parent=="MonoBehaviour"?"virtual":"override"))+" void "+name+"() { "+Body(ev["body"]!.AsArray(),name)+" }\n");}
            if(parent=="MonoBehaviour")foreach(var name in new[]{"Start","Update","OnDestroy"}.Except(cls["events"]!.AsArray().Select(e=>Name(e!["name"]))))text.Append("protected virtual void "+name+"() {}\n");
            text.Append("}\n");
        }
        ActorScripts.FromUnity(text.ToString());return text.ToString();
    }
}
