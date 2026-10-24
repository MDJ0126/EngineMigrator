using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineMigrator.Core;

public sealed record CodeMapping(string Kind, string SourceSymbol, string TargetSymbol, int SourceLine,
    int TargetLine, string Rule, string SourceSnippet, string TargetSnippet, string Usage);
public sealed record CodeConversion(bool Generated, string? FileName, string? Code, IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<CodeMapping> Mappings { get; init; } = [];
}

/// <summary>Engine-independent static float/double/bool logic only. Does not claim engine port completion.</summary>
public static class PortableScriptConverter
{
    private static readonly HashSet<string> Types = ["float", "double", "bool"];

    public static CodeConversion Convert(string source, Engine from)
    {
        try
        {
            var normalized = from == Engine.Unity ? source : ReadCpp(source);
            var tree = CSharpSyntaxTree.ParseText(normalized);
            var root = tree.GetCompilationUnitRoot();
            if (root.ContainsDirectives) throw new NotSupportedException("조건부 컴파일/전처리 지시문은 아직 지원하지 않습니다.");
            var syntaxErrors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (syntaxErrors.Length > 0) throw new NotSupportedException(string.Join("\n", syntaxErrors.Select(e => e.ToString())));
            if (root.Usings.Count > 0 || root.AttributeLists.Count > 0 || root.Members.Count != 1 || root.Members[0] is not ClassDeclarationSyntax cls)
                throw new NotSupportedException("현재는 using/namespace/속성 없이 단일 static class만 지원합니다.");
            if (!cls.Modifiers.Any(SyntaxKind.PublicKeyword) || !cls.Modifiers.Any(SyntaxKind.StaticKeyword) || cls.BaseList is not null || cls.TypeParameterList is not null || cls.AttributeLists.Count > 0 || cls.Modifiers.Any(m => !m.IsKind(SyntaxKind.PublicKeyword) && !m.IsKind(SyntaxKind.StaticKeyword)))
                throw new NotSupportedException("상속·제네릭·속성이 없는 public static class가 필요합니다.");
            if (cls.Members.Count == 0 || cls.Members.Any(m => m is not MethodDeclarationSyntax))
                throw new NotSupportedException("필드·생성자·프로퍼티·중첩 타입은 아직 지원하지 않습니다.");
            var methods = cls.Members.Cast<MethodDeclarationSyntax>().ToArray();
            if (methods.Select(m => m.Identifier.ValueText).Distinct().Count() != methods.Length)
                throw new NotSupportedException("메서드 오버로드는 아직 지원하지 않습니다.");
            foreach (var method in methods)
            {
                if (!method.Modifiers.Any(SyntaxKind.PublicKeyword) || !method.Modifiers.Any(SyntaxKind.StaticKeyword) || method.Modifiers.Any(m => !m.IsKind(SyntaxKind.PublicKeyword) && !m.IsKind(SyntaxKind.StaticKeyword)) || method.AttributeLists.Count > 0 || method.TypeParameterList is not null)
                    throw new NotSupportedException("public static 메서드만 지원합니다.");
                ValidateType(method.ReturnType);
                foreach (var p in method.ParameterList.Parameters)
                {
                    if (p.Type is null || p.Default is not null || p.Modifiers.Count > 0 || p.AttributeLists.Count > 0) throw new NotSupportedException("기본 인수·ref/out·속성은 지원하지 않습니다.");
                    ValidateType(p.Type);
                }
            }
            var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
            var compilation = CSharpCompilation.Create("PortingValidation", [tree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0) throw new NotSupportedException("원본 의미 검사 실패: " + string.Join("\n", errors.Select(e => e.ToString())));
            var semantic = compilation.GetSemanticModel(tree);
            foreach (var expression in root.DescendantNodes().OfType<ExpressionSyntax>())
            {
                var type = semantic.GetTypeInfo(expression).Type;
                // Integer constants are allowed only when explicitly used in floating-point contexts below.
                if (type?.SpecialType == SpecialType.System_Int32 && expression is not LiteralExpressionSyntax)
                    throw new NotSupportedException("정수 연산의 C#/C++ 의미 차이를 처리하기 전까지 정수 표현식은 차단합니다.");
            }
            var names = methods.Select(m => m.Identifier.ValueText).ToHashSet();
            string Expr(ExpressionSyntax expression) => expression switch
            {
                LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.TrueLiteralExpression) || literal.IsKind(SyntaxKind.FalseLiteralExpression) => literal.Token.ValueText,
                LiteralExpressionSyntax literal when literal.Token.Value is float or double => Number(literal.Token.Value),
                LiteralExpressionSyntax literal when literal.Token.Value is int => throw new NotSupportedException("수치 리터럴은 1.0 또는 1.0f처럼 실수로 작성해 주세요."),
                IdentifierNameSyntax identifier => SafeName(identifier.Identifier),
                ParenthesizedExpressionSyntax parenthesized => "(" + Expr(parenthesized.Expression) + ")",
                BinaryExpressionSyntax binary when AllowedBinary(binary.Kind()) => "(" + Expr(binary.Left) + " " + binary.OperatorToken.Text + " " + Expr(binary.Right) + ")",
                PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression) || unary.IsKind(SyntaxKind.UnaryPlusExpression) || unary.IsKind(SyntaxKind.LogicalNotExpression) => "(" + unary.OperatorToken.Text + Expr(unary.Operand) + ")",
                ConditionalExpressionSyntax conditional => "(" + Expr(conditional.Condition) + " ? " + Expr(conditional.WhenTrue) + " : " + Expr(conditional.WhenFalse) + ")",
                InvocationExpressionSyntax call when call.Expression is IdentifierNameSyntax id && names.Contains(id.Identifier.ValueText) && call.ArgumentList.Arguments.All(a => a.RefKindKeyword.IsKind(SyntaxKind.None) && a.NameColon is null)
                    => SafeName(id.Identifier) + "(" + string.Join(", ", call.ArgumentList.Arguments.Select(a => Expr(a.Expression))) + ")",
                _ => throw new NotSupportedException($"미지원 표현식: {expression.Kind()} (L{expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1})")
            };
            string Statement(StatementSyntax statement) => statement switch
            {
                BlockSyntax block => "{\n" + string.Join("\n", block.Statements.Select(Statement)) + "\n}",
                ReturnStatementSyntax ret when ret.Expression is not null => "return " + Expr(ret.Expression) + ";",
                IfStatementSyntax branch => "if (" + Expr(branch.Condition) + ") " + Statement(branch.Statement) + (branch.Else is null ? "" : " else " + Statement(branch.Else.Statement)),
                LocalDeclarationStatementSyntax local when local.Modifiers.Count == 0 => Local(local),
                ExpressionStatementSyntax expression when expression.Expression is AssignmentExpressionSyntax assignment => Assign(assignment) + ";",
                WhileStatementSyntax loop => "while (" + Expr(loop.Condition) + ") " + Statement(loop.Statement),
                BreakStatementSyntax => "break;",
                ContinueStatementSyntax => "continue;",
                _ => throw new NotSupportedException($"미지원 문장: {statement.Kind()}")
            };
            string Assign(AssignmentExpressionSyntax assignment)
            {
                if (assignment.Left is not IdentifierNameSyntax id || assignment.Kind() is not (SyntaxKind.SimpleAssignmentExpression or SyntaxKind.AddAssignmentExpression or SyntaxKind.SubtractAssignmentExpression or SyntaxKind.MultiplyAssignmentExpression or SyntaxKind.DivideAssignmentExpression))
                    throw new NotSupportedException("지역 값/매개변수의 단순 대입만 지원합니다.");
                return SafeName(id.Identifier) + " " + assignment.OperatorToken.Text + " " + Expr(assignment.Right);
            }
            string Local(LocalDeclarationStatementSyntax local)
            {
                ValidateType(local.Declaration.Type);
                if (local.Declaration.Variables.Any(v => v.Initializer is null)) throw new NotSupportedException("지역 변수는 초기값이 필요합니다.");
                return local.Declaration.Type + " " + string.Join(", ", local.Declaration.Variables.Select(v => SafeName(v.Identifier) + " = " + Expr(v.Initializer!.Value))) + ";";
            }
            var className = SafeName(cls.Identifier);
            var rendered = methods.Select(method =>
            {
                var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => p.Type + " " + SafeName(p.Identifier)));
                var body = method.Body is not null ? Statement(method.Body) : method.ExpressionBody is not null ? "{ return " + Expr(method.ExpressionBody.Expression) + "; }" : throw new NotSupportedException("메서드 본문이 없습니다.");
                return "    public static " + method.ReturnType + " " + SafeName(method.Identifier) + "(" + parameters + ") " + body;
            }).ToArray();
            IReadOnlyList<CodeMapping> Map(string code, string[] targetMethods)
            {
                static int LineAt(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;
                string OriginalLines(SyntaxNode node)
                {
                    var range = node.GetLocation().GetLineSpan();
                    return string.Join("\n", source.Replace("\r\n", "\n").Split('\n').Skip(range.StartLinePosition.Line).Take(range.EndLinePosition.Line - range.StartLinePosition.Line + 1));
                }
                var classTarget = "class " + className;
                var result = new List<CodeMapping>
                {
                    new("클래스", className, className, cls.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        LineAt(code, code.IndexOf(classTarget, StringComparison.Ordinal)),
                        from == Engine.Unity ? "C# public static class → C++ class + public static 멤버. 엔진 객체 상속은 생성하지 않습니다." : "C++ public static 함수 컨테이너 → C# public static class. MonoBehaviour를 생성하지 않습니다.",
                        OriginalLines(cls), from == Engine.Unity ? "class " + className : "public static class " + className,
                        "정적 유틸리티 클래스입니다. GameObject/Actor에 부착하는 컴포넌트가 아닙니다.")
                };
                for (var i = 0; i < methods.Length; i++)
                {
                    var method = methods[i]; var name = method.Identifier.ValueText;
                    var signature = method.ReturnType + " " + name + method.ParameterList;
                    var types = string.Join(", ", method.ParameterList.Parameters.Select(p => p.Type!.ToString()).Prepend(method.ReturnType.ToString()).Distinct());
                    var usage = className + (from == Engine.Unity ? "::" : ".") + name + "(" + string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier.ValueText)) + ")";
                    result.Add(new("메서드", className + "." + signature, className + (from == Engine.Unity ? "::" : ".") + signature,
                        method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        LineAt(code, code.IndexOf(targetMethods[i], StringComparison.Ordinal)),
                        "반환형·매개변수 순서·이름 유지. 타입 동일 대응: " + types + ". 수식 괄호 보존; if/else·return 유지; 식 본문은 return 블록으로 생성. 대상 엔진 동작은 미검증.",
                        OriginalLines(method), targetMethods[i], usage));
                }
                return result;
            }
            if (from == Engine.Unity)
            {
                var targetMethods = rendered.Select(m => m.Replace("public static ", "static ", StringComparison.Ordinal)).ToArray();
                var cpp = "// EngineMigrator: generated portable logic; engine integration not verified.\n#pragma once\nclass " + className + "\n{\npublic:\n" + string.Join("\n", targetMethods) + "\n};\n";
                return new(true, className + ".h", cpp, ["코드 생성됨. 대상 엔진 통합·컴파일·플레이 검증은 별도로 필요합니다."]) { Mappings = Map(cpp, targetMethods) };
            }
            var cs = "// EngineMigrator: generated portable logic; engine integration not verified.\npublic static class " + className + "\n{\n" + string.Join("\n", rendered) + "\n}\n";
            return new(true, className + ".cs", cs, ["코드 생성됨. Unity 프로젝트 컴파일·플레이 검증은 별도로 필요합니다."]) { Mappings = Map(cs, rendered) };
        }
        catch (NotSupportedException ex) { return new(false, null, null, [ex.Message]); }
    }

    private static void ValidateType(TypeSyntax type)
    { if (!Types.Contains(type.ToString())) throw new NotSupportedException("현재 지원 타입: float, double, bool. 요청 타입: " + type); }

    private static string Number(object value)
    {
        var number=value is float single ? single.ToString("R",System.Globalization.CultureInfo.InvariantCulture) : ((double)value).ToString("R",System.Globalization.CultureInfo.InvariantCulture);
        if(!number.Contains('.')&&!number.Contains('E')&&!number.Contains('e'))number+=".0";
        return number+(value is float?"f":"");
    }

    private static string SafeName(SyntaxToken token)
    {
        var name = token.Text;
        var cppKeywords = new[] { "auto", "template", "delete", "new", "operator", "virtual", "friend", "typename", "union", "unsigned", "signed", "long", "short", "sizeof", "alignof", "register", "volatile", "mutable", "extern", "const", "nullptr", "this", "and", "or", "not", "xor", "compl", "bitand", "bitor", "and_eq", "or_eq", "xor_eq", "not_eq", "concept", "requires", "co_await", "co_return", "co_yield", "char", "wchar_t", "char8_t", "char16_t", "char32_t", "thread_local", "static_assert", "consteval", "constexpr", "constinit", "typeid", "dynamic_cast", "reinterpret_cast", "static_cast", "const_cast", "noexcept", "asm", "export", "explicit", "using", "namespace", "protected", "private" };
        if (!Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]*$") || cppKeywords.Contains(name)) throw new NotSupportedException("대상 언어에서 지원하지 않는 식별자: " + name);
        return name;
    }
    private static bool AllowedBinary(SyntaxKind kind) => kind is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression or SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression;

    private static string ReadCpp(string source)
    {
        // Only this closed header grammar is accepted; no broad textual engine/API substitution.
        static string Blank(Match match) => Regex.Replace(match.Value, @"[^\r\n]", " ");
        source = Regex.Replace(source, @"//[^\r\n]*|/\*[\s\S]*?\*/", Blank);
        source = Regex.Replace(source, @"(?m)^\s*#pragma\s+once\s*$", Blank);
        if (source.Contains('#')) throw new NotSupportedException("C++ include/매크로/전처리 지시문은 아직 지원하지 않습니다.");
        var match = Regex.Match(source, @"\A\s*class\s+(?<name>[A-Za-z][A-Za-z0-9_]*)\s*\{\s*public\s*:\s*(?<body>[\s\S]*)\}\s*;\s*\z");
        if (!match.Success) throw new NotSupportedException("현재 C++ 입력은 단일 class의 public static 실수·불리언 함수 헤더만 지원합니다.");
        if (Regex.IsMatch(match.Groups["body"].Value, @"\b(public|private|protected|internal)\b"))
            throw new NotSupportedException("C++ 헤더 본문에 추가 접근 지정자 또는 C# 접근 키워드가 있습니다.");
        // C# semantic/syntax checks below reject unsupported C++ constructs and member forms.
        // Preserve original line numbers so the mapping report can point back to the actual header.
        source = Regex.Replace(source, @"\bclass\s+" + Regex.Escape(match.Groups["name"].Value), "public static $0");
        source = Regex.Replace(source, @"\bpublic\s*:", Blank);
        source = Regex.Replace(source, @"\bstatic\s+(float|double|bool)\s+", "public $0");
        return Regex.Replace(source, @";\s*\z", Blank);
    }
}
