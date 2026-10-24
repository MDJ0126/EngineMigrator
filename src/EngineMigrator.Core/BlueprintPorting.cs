using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineMigrator.Core;

/// <summary>Closed, executable function-library subset. Engine object/event graphs are rejected.</summary>
public static class BlueprintPorting
{
    public static string FromCSharp(string source)
    {
        var checkedCode = PortableScriptConverter.Convert(source, Engine.Unity);
        if (!checkedCode.Generated) throw new NotSupportedException(string.Join("; ",checkedCode.Diagnostics));
        var cls = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        JsonObject Expr(ExpressionSyntax e, Dictionary<string,JsonObject> locals) => e switch
        {
            ParenthesizedExpressionSyntax p => Expr(p.Expression,locals),
            IdentifierNameSyntax i => locals.TryGetValue(i.Identifier.ValueText,out var value) ? (JsonObject)value.DeepClone() : new() { ["kind"]="param",["name"]=i.Identifier.ValueText },
            LiteralExpressionSyntax l when l.Token.Value is double or float => new() { ["kind"]="const",["value"]=System.Convert.ToDouble(l.Token.Value,System.Globalization.CultureInfo.InvariantCulture) },
            PrefixUnaryExpressionSyntax u when u.IsKind(SyntaxKind.UnaryMinusExpression) => new() { ["kind"]="binary",["op"]="-",["left"]=new JsonObject { ["kind"]="const",["value"]=0.0 },["right"]=Expr(u.Operand,locals) },
            BinaryExpressionSyntax b when new[]{"+","-","*",">","<","=="}.Contains(b.OperatorToken.Text) => new() { ["kind"]="binary",["op"]=b.OperatorToken.Text,["left"]=Expr(b.Left,locals),["right"]=Expr(b.Right,locals) },
            _ => throw new NotSupportedException("Blueprint 미지원 표현식: "+e)
        };
        JsonObject Statements(IReadOnlyList<StatementSyntax> statements, Dictionary<string,JsonObject> locals)
        {
            if (statements.Count==0) throw new NotSupportedException("모든 Blueprint 실행 경로에 반환이 필요합니다.");
            var first=statements[0]; var remaining=statements.Skip(1).ToArray();
            if (first is ReturnStatementSyntax r && r.Expression is not null) return new() { ["kind"]="return",["value"]=Expr(r.Expression,locals) };
            if (first is LocalDeclarationStatementSyntax local)
            {
                if (local.Declaration.Type.ToString()!="double") throw new NotSupportedException("Blueprint 지역 값은 double만 지원합니다.");
                var next=new Dictionary<string,JsonObject>(locals);
                foreach(var v in local.Declaration.Variables) next.Add(v.Identifier.ValueText,Expr(v.Initializer!.Value,next));
                return Statements(remaining,next);
            }
            IReadOnlyList<StatementSyntax> Expand(StatementSyntax s) => s is BlockSyntax block ? block.Statements.Concat(remaining).ToArray() : new[]{s}.Concat(remaining).ToArray();
            if (first is IfStatementSyntax branch) return new() { ["kind"]="if",["condition"]=Expr(branch.Condition,locals),["then"]=Statements(Expand(branch.Statement),new(locals)),["else"]=Statements(branch.Else is null ? remaining : Expand(branch.Else.Statement),new(locals)) };
            if (first is BlockSyntax nested) return Statements(nested.Statements.Concat(remaining).ToArray(),new(locals));
            throw new NotSupportedException("Blueprint 미지원 실행 문장: "+first.Kind());
        }
        var functions=new JsonArray();
        foreach(var method in cls.Members.Cast<MethodDeclarationSyntax>())
        {
            if(method.ReturnType.ToString()!="double" || method.ParameterList.Parameters.Any(p=>p.Type!.ToString()!="double")) throw new NotSupportedException("Blueprint 함수 입출력은 현재 double만 지원합니다.");
            var body=method.Body is not null ? Statements(method.Body.Statements.ToArray(),new()) : new JsonObject { ["kind"]="return",["value"]=Expr(method.ExpressionBody!.Expression,new()) };
            functions.Add(new JsonObject { ["name"]=method.Identifier.ValueText,["args"]=new JsonArray(method.ParameterList.Parameters.Select(p=>(JsonNode?)JsonValue.Create(p.Identifier.ValueText)).ToArray()),["body"]=body });
        }
        return new JsonObject { ["schema"]=1,["className"]=cls.Identifier.ValueText,["parent"]="BlueprintFunctionLibrary",["functions"]=functions }.ToJsonString(new JsonSerializerOptions { WriteIndented=true });
    }

    public static CodeConversion ToCSharp(string json)
    {
        try
        {
            using var document=JsonDocument.Parse(json); var root=document.RootElement;
            if(root.GetProperty("schema").GetInt32()!=1 || root.GetProperty("parent").GetString()!="BlueprintFunctionLibrary") throw new NotSupportedException("Blueprint 상속/객체 그래프는 미지원입니다.");
            string Name(string? value) => value is not null && System.Text.RegularExpressions.Regex.IsMatch(value,"^[A-Za-z][A-Za-z0-9_]*$") ? value : throw new NotSupportedException("잘못된 식별자");
            string Expr(JsonElement e,HashSet<string> args,int depth=0)
            {
                if(depth>128) throw new NotSupportedException("그래프 깊이 제한 초과");
                switch(e.GetProperty("kind").GetString())
                {
                    case "param": var name=Name(e.GetProperty("name").GetString()); return args.Contains(name) ? name : throw new NotSupportedException("알 수 없는 입력");
                    case "const": var v=e.GetProperty("value").GetDouble(); return double.IsFinite(v) ? v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"d" : throw new NotSupportedException("비유한 상수");
                    case "binary": var op=e.GetProperty("op").GetString(); if(!new[]{"+","-","*",">","<","=="}.Contains(op)) throw new NotSupportedException("미지원 연산"); return "("+Expr(e.GetProperty("left"),args,depth+1)+" "+op+" "+Expr(e.GetProperty("right"),args,depth+1)+")";
                    default: throw new NotSupportedException("미지원 데이터 노드");
                }
            }
            string Body(JsonElement b,HashSet<string> args,int depth=0)
            {
                if(depth>128) throw new NotSupportedException("실행 그래프 깊이 제한 초과");
                return b.GetProperty("kind").GetString() switch {
                    "return" => "return "+Expr(b.GetProperty("value"),args)+";",
                    "if" => "if ("+Expr(b.GetProperty("condition"),args)+") { "+Body(b.GetProperty("then"),args,depth+1)+" } else { "+Body(b.GetProperty("else"),args,depth+1)+" }",
                    _=>throw new NotSupportedException("미지원 실행 노드") };
            }
            var className=Name(root.GetProperty("className").GetString());
            var methods=root.GetProperty("functions").EnumerateArray().Select(f=> { var args=f.GetProperty("args").EnumerateArray().Select(a=>Name(a.GetString())).ToArray(); return "public static double "+Name(f.GetProperty("name").GetString())+"("+string.Join(", ",args.Select(a=>"double "+a))+") { "+Body(f.GetProperty("body"),args.ToHashSet())+" }"; }).ToArray();
            var code="public static class "+className+" {\n"+string.Join("\n",methods)+"\n}\n";
            var validation=PortableScriptConverter.Convert(code,Engine.Unity);
            if(!validation.Generated) throw new NotSupportedException(string.Join("; ",validation.Diagnostics));
            return new(true,className+".cs",code,["실제 그래프에서 추출한 제한된 함수 라이브러리입니다. 이벤트·부모 호출·객체 상태는 지원하지 않습니다."]);
        }
        catch(Exception ex) when(ex is NotSupportedException or JsonException or KeyNotFoundException or InvalidOperationException) { return new(false,null,null,[ex.Message]); }
    }
}
