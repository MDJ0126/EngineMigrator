using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineMigrator.Core;

public sealed record ScriptUnit(string Path, string Language, string Sha256, string Status,
    IReadOnlyList<string> Types, IReadOnlyList<string> Methods, IReadOnlyList<string> Calls,
    IReadOnlyList<string> Diagnostics);

public static class ScriptPorting
{
    public static bool IsCode(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".cs" or ".cpp" or ".cc" or ".cxx" or ".h" or ".hpp" or ".inl" or ".c";

    public static ScriptUnit Analyze(string root, string relativePath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var fullPath = Path.Combine(root, relativePath);
        var bytes = File.ReadAllBytes(fullPath);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return new(relativePath, "C/C++", hash, "차단: C++ 구문·타입 분석 및 변환 미구현", [], [], [],
                ["Unreal 리플렉션 매크로와 빌드 설정을 포함한 분석이 필요합니다."]);

        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var tree = CSharpSyntaxTree.ParseText(reader.ReadToEnd(), path: relativePath, cancellationToken: token);
        var syntax = tree.GetRoot(token);
        var types = syntax.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Select(t => $"{t.Kind()} {t.Identifier.ValueText}{t.BaseList?.ToString()}").ToArray();
        var methods = syntax.DescendantNodes().OfType<BaseMethodDeclarationSyntax>()
            .Select(m => string.Join(" ", m.Modifiers.Select(x => x.Text)) + " " + (m switch
            {
                MethodDeclarationSyntax method => $"{method.ReturnType} {method.Identifier}{method.ParameterList}",
                ConstructorDeclarationSyntax constructor => $"{constructor.Identifier}{constructor.ParameterList}",
                _ => m.Kind() + " " + m.ParameterList
            })).ToArray();
        var calls = syntax.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(call => $"L{call.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {call}").ToArray();
        var diagnostics = tree.GetDiagnostics(token).Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString()).ToList();
        diagnostics.Add("구문 분석만 수행했습니다. 호출 대상 해석·조건부 컴파일 전체 분기·상속 의미·엔진 API 호환성은 미검증입니다.");
        return new(relativePath, "C#", hash, "차단: 프로젝트 포팅 미완료. 부분 코드 생성 가능 여부는 별도 생성 단계에서 검사",
            types, methods, calls, diagnostics);
    }

    public static string ToHtml(InspectionReport report)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "미확인");
        static string List(IEnumerable<string> values) => "<ul>" + string.Concat(values.Select(v => "<li><code>" + E(v) + "</code></li>")) + "</ul>";
        var html = new StringBuilder("<!doctype html><html lang=\"ko\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>EngineMigrator 포팅 보고서</title><style>body{font:16px/1.7 system-ui;max-width:1100px;margin:40px auto;padding:0 24px;background:#211e1a;color:#f5efe5}h1,h2{color:#f3ab73}article{border:1px solid #514638;padding:20px;margin:20px 0;border-radius:12px}code{white-space:pre-wrap;overflow-wrap:anywhere}.blocked{color:#ffce86}a{color:#f3ab73}</style><h1>프로젝트 포팅 조사 보고서</h1><p class=\"blocked\">포팅 미완료 · 실제 코드 생성/엔진 컴파일/실행 검증은 수행되지 않았습니다.</p>");
        html.Append($"<p>{E(report.SourceEngine.ToString())} → {E(report.TargetEngine.ToString())}<br>원본: {E(report.Source)}<br>대상: {E(report.Target)}<br>원본 버전: {E(report.SourceVersion)}<br>검사 시각: {E(report.InspectedAt.ToString("O"))}</p>");
        html.Append("<h2>검사 범위와 제한</h2>" + List(report.Findings.Select(f => $"[{f.Severity}] {f.Message}")));
        html.Append($"<h2>스크립트 {report.Scripts.Count}개 — 모두 변환 대기</h2><p>C# 호출식은 구문상의 텍스트이며 검증된 호출 관계가 아닙니다. .uasset 내부 Blueprint 여부와 실행 흐름은 아직 추출하지 않았습니다.</p>");
        foreach (var script in report.Scripts)
        {
            html.Append($"<article><h3>{E(script.Path)}</h3><p class=\"blocked\">{E(script.Status)}</p><p>언어: {E(script.Language)}<br>SHA-256: <code>{E(script.Sha256)}</code></p>");
            html.Append("<h4>타입·상속 구문</h4>" + List(script.Types));
            html.Append("<h4>메서드·생성자 선언</h4>" + List(script.Methods));
            html.Append("<h4>호출식 (대상 미해석)</h4>" + List(script.Calls));
            html.Append("<h4>진단</h4>" + List(script.Diagnostics) + "</article>");
        }
        html.Append("<h2>파일 목록</h2>" + List(report.Assets.Select(a => $"{a.Path} — {a.Kind} ({a.Bytes} bytes)")));
        return html.Append("</html>").ToString();
    }
}
