using System.Globalization;
using System.Reflection;
using EngineMigrator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class ConversionChecks
{
    internal static void Run(Action<bool, string> check, string root)
    {
        const string original = """
public static class CombatMath
{
    public static double Damage(double attack, double armor)
    {
        double result = attack - armor;
        if (result < 0.0) { return 0.0; }
        return result;
    }
    public static double Scale(double value, double factor) => value * factor;
    public static bool Alive(double health) => health > 0.0;
    public static float Blend(float a, float b, float t) => a + (b - a) * t;
    public static double Combined(double a, double b) => Scale(Damage(a, b), 2.0);
}
""";
        var cpp = PortableScriptConverter.Convert(original, Engine.Unity);
        check(cpp.Generated && cpp.Code!.Contains("class CombatMath"), "C# static gameplay math generates C++ header");
        var reverse = PortableScriptConverter.Convert(cpp.Code!, Engine.Unreal);
        check(reverse.Generated && reverse.FileName == "CombatMath.cs", "C++ static header generates C#");
        check(cpp.Mappings.Count == 6 && reverse.Mappings.Count == 6, "Class and all five methods have source/target mappings");
        foreach (var conversion in new[] { cpp, reverse })
        {
            var input = conversion == cpp ? original : cpp.Code!;
            var inputLines = input.Replace("\r\n", "\n").Split('\n');
            var outputLines = conversion.Code!.Split('\n');
            check(conversion.Mappings.Where(m => m.Kind == "메서드").All(m =>
                inputLines[m.SourceLine - 1].Contains(m.SourceSymbol.Split('.').Last().Split('(')[0].Split(' ').Last())
                && outputLines[m.TargetLine - 1].Contains(m.TargetSnippet.Split('\n')[0].Trim())), "Mapping line numbers match actual source and generated methods");
        }
        check(cpp.Mappings.Any(m => m.Usage == "CombatMath::Damage(attack, armor)") && reverse.Mappings.Any(m => m.Usage == "CombatMath.Damage(attack, armor)"), "Engine-direction call syntax documented");
        var before = Compile(original); var after = Compile(reverse.Code!);
        var expected = new List<string>();
        foreach (var (method, arguments) in new (string, object[])[]
        {
            ("Damage", [10.0, 3.0]), ("Damage", [2.0, 5.0]), ("Scale", [-2.5, 4.0]),
            ("Alive", [0.0]), ("Alive", [1.0]), ("Blend", [1.0f, 5.0f, 0.25f]), ("Combined", [10.0, 3.0])
        })
        {
            var a = before.GetMethod(method)!.Invoke(null, arguments);
            var b = after.GetMethod(method)!.Invoke(null, arguments);
            check(Equals(a, b), "C# original/roundtrip execution matches: " + method);
            expected.Add(a is bool boolean ? (boolean ? "1" : "0") : System.Convert.ToString(a, CultureInfo.InvariantCulture)!);
        }
        foreach (var unsupported in new[]
        {
            "using UnityEngine; public class Mover : MonoBehaviour { void Update() {} }",
            "public static class Logic { public static int Add(int a, int b) => a + b; }",
            "public static class Logic { static double Hidden(double a) => a; }",
            "public static class Logic { public static double Value = 1.0; }",
            "public static class Logic { public static double Tick(double x) { do {} while(true); } }",
            "public static class Logic { public static double Tick(double x) => System.Math.Abs(x); }",
            "#if NEVER\nclass Hidden {}\n#endif\npublic static class Logic { public static double X(double x) => x; }"
        }) check(!PortableScriptConverter.Convert(unsupported, Engine.Unity).Generated, "Unsupported source blocked without fake output");
        check(!PortableScriptConverter.Convert("#include <cmath>\nclass X { public: static double F(double a) { return a; } };", Engine.Unreal).Generated, "C++ engine/preprocessor dependency blocked");
        const string loopSource="public static class CombatMath { public static double Sum(double count) { double i=0.0; double total=0.0; while(i<count) { i += 1.0; if(i==2.0) { continue; } total = total+i; if(i>=4.0) { break; } } return total; } public static double Ratio() => 2d/3d; }";
        var loopCpp=PortableScriptConverter.Convert(loopSource,Engine.Unity);
        var loopCs=PortableScriptConverter.Convert(loopCpp.Code!,Engine.Unreal);
        check(loopCpp.Generated&&loopCs.Generated,"Assignments/while/break/continue generate both languages");
        foreach(var count in new[]{0.0,1.0,3.0,8.0}) check(Equals(Compile(loopSource).GetMethod("Sum")!.Invoke(null,[count]),Compile(loopCs.Code!).GetMethod("Sum")!.Invoke(null,[count])),"Loop original/roundtrip executes equally: "+count);
        check(Equals(Compile(loopSource).GetMethod("Ratio")!.Invoke(null,null),Compile(loopCs.Code!).GetMethod("Ratio")!.Invoke(null,null)),"Double suffix literals preserve floating division");
        const string bpSource="public static class CombatMath { public static double Damage(double attack,double armor) { double result=attack-armor; if(result<0.0) { return 0.0; } return result; } public static double Scale(double value,double factor) => value*factor; }";
        var bpJson=BlueprintPorting.FromCSharp(bpSource);var bpCs=BlueprintPorting.ToCSharp(bpJson);
        check(bpCs.Generated,"C# function flow lowers to Blueprint IR and back");
        foreach(var values in new[]{new object[]{10.0,3.0},new object[]{2.0,5.0},new object[]{0.0,0.0}}) check(Equals(Compile(bpSource).GetMethod("Damage")!.Invoke(null,values),Compile(bpCs.Code!).GetMethod("Damage")!.Invoke(null,values)),"Blueprint IR equivalent C# branch execution matches");
        check(!BlueprintPorting.ToCSharp(bpJson.Replace("BlueprintFunctionLibrary","Actor")).Generated,"Blueprint unsupported inheritance is rejected");
        check(!BlueprintPorting.ToCSharp(bpJson.Replace("\"binary\"","\"unknownNode\"")).Generated,"Blueprint unknown data nodes are rejected");
        var bpArtifacts=Path.GetFullPath("artifacts/blueprint-check");Directory.CreateDirectory(bpArtifacts);
        File.WriteAllText(Path.Combine(bpArtifacts,"CombatMath.original.cs"),bpSource);
        var bpInput=System.Text.Json.Nodes.JsonNode.Parse(bpJson)!;
        bpInput["cases"]=System.Text.Json.Nodes.JsonNode.Parse("[{\"function\":\"Damage\",\"args\":[10,3]},{\"function\":\"Damage\",\"args\":[2,5]},{\"function\":\"Damage\",\"args\":[0,0]},{\"function\":\"Scale\",\"args\":[-2.5,4]}]");
        File.WriteAllText(Path.Combine(bpArtifacts,"input.json"),bpInput.ToJsonString());
        File.WriteAllText(Path.Combine(bpArtifacts,"Loop.h"),loopCpp.Code);
        File.WriteAllLines(Path.Combine(bpArtifacts,"loop-expected.txt"),new[]{0.0,1.0,3.0,8.0}.Select(count=>((double)Compile(loopSource).GetMethod("Sum")!.Invoke(null,[count])!).ToString("G17",CultureInfo.InvariantCulture)).Append(((double)Compile(loopSource).GetMethod("Ratio")!.Invoke(null,null)!).ToString("G17",CultureInfo.InvariantCulture)));
        File.WriteAllText(Path.Combine(bpArtifacts,"loop-main.cpp"),"#include <cstdio>\n#include <initializer_list>\n#include \"Loop.h\"\nint main() { for(double count : {0.0,1.0,3.0,8.0}) { printf(\"%.17g\\n\",CombatMath::Sum(count)); } printf(\"%.17g\\n\",CombatMath::Ratio()); }\n");
        var fixture = Path.Combine(root, "portable-fixture");
        var unity = Path.Combine(fixture, "Unity"); var unreal = Path.Combine(fixture, "Unreal");
        Directory.CreateDirectory(Path.Combine(unity, "Assets")); Directory.CreateDirectory(Path.Combine(unity, "ProjectSettings"));
        Directory.CreateDirectory(Path.Combine(unreal, "Source"));
        File.WriteAllText(Path.Combine(unity, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.0.83f1");
        File.WriteAllText(Path.Combine(unity, "Assets", "CombatMath.cs"), original);
        File.WriteAllText(Path.Combine(unity, "Assets", "Unsupported.cs"), "class Mover {} ");
        File.WriteAllText(Path.Combine(unity, "Assets", "Material.mat"), "fixture-only");
        File.WriteAllText(Path.Combine(unity, "Assets", "Material.mat.meta"), "guid: fixture-only");
        File.WriteAllText(Path.Combine(unreal, "Demo.uproject"), "{\"EngineAssociation\":\"5.5\"}");
        var report = ProjectInspector.Inspect(unity, unreal, Engine.Unity);
        var bundle = ConversionBundle.Export(report, Path.Combine(fixture, "exports"));
        check(bundle.Files.Count == 2 && bundle.Files.Count(f => f.Generated) == 1 && !bundle.ProjectPortComplete, "Bundle records generated and blocked scripts separately");
        check(File.Exists(Path.Combine(bundle.Directory, "conversion-manifest.json")), "Bundle manifest written");
        check(bundle.Mapping.Count == report.Assets.Count && bundle.Mapping.Any(m => m.SourcePath.EndsWith(".meta")), "Every inspected file including resource metadata is mapped");
        check(bundle.Mapping.Where(m => m.SourcePath.EndsWith(".mat") || m.SourcePath.EndsWith(".meta")).All(m => m.ActualOutputPath is null && m.Status.StartsWith("미변환")), "Unconverted resources have no invented output bindings");
        var generated = bundle.Mapping.Single(m => m.ActualOutputPath is not null);
        check(File.Exists(Path.Combine(bundle.Directory, generated.ActualOutputPath!)) && generated.CodeMappings.Count == 6, "Mapped output and symbol details point to actual generated file");
        var mapHtmlPath = Path.Combine(bundle.Directory, "porting-map.html");
        check(File.Exists(mapHtmlPath) && File.ReadAllText(mapHtmlPath).Contains("CombatMath::Damage"), "HTML mapping report contains actual invocation guidance");
        var unsafeEntry = generated with { SourcePath = "<img src=x onerror=alert(1)>" };
        var safeHtml = PortingMap.ToHtml(report, bundle with { Mapping = [unsafeEntry] });
        check(!safeHtml.Contains("<img src=x") && safeHtml.Contains("&lt;img"), "Mapping HTML escapes untrusted source paths");
        var sampleDirectory = Path.GetFullPath("artifacts/mapping-sample");
        foreach (var file in Directory.EnumerateFiles(bundle.Directory, "*", SearchOption.AllDirectories))
        {
            var output = Path.Combine(sampleDirectory, Path.GetRelativePath(bundle.Directory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.Copy(file, output, true);
        }
        var sampleBundle = bundle with { Directory = sampleDirectory };
        File.WriteAllText(Path.Combine(sampleDirectory, "porting-map.html"), PortingMap.ToHtml(report, sampleBundle)
            .Replace("<h1>무엇이 어디로 옮겨졌나요?</h1>", "<h1>무엇이 어디로 옮겨졌나요?</h1><p>검증용 임시 프로젝트 예제입니다. 실제 사용자 프로젝트의 포팅 결과가 아닙니다.</p>", StringComparison.Ordinal));
        var second = ConversionBundle.Export(report, Path.Combine(fixture, "exports"));
        check(second.Directory != bundle.Directory && File.Exists(Path.Combine(bundle.Directory, bundle.Files.Single(f => f.Generated).OutputPath!)), "Repeat export preserves previous bundle");
        var sourceWriteBlocked = false;
        try { ConversionBundle.Export(report, unity); } catch (InvalidOperationException) { sourceWriteBlocked = true; }
        check(sourceWriteBlocked, "Bundle output inside source blocked");
        File.AppendAllText(Path.Combine(unity, "Assets", "CombatMath.cs"), "\n// changed");
        var staleBlocked = false;
        try { ConversionBundle.Export(report, Path.Combine(fixture, "exports")); } catch (InvalidOperationException) { staleBlocked = true; }
        check(staleBlocked, "Changed source requires reinspection");
        File.WriteAllText(Path.Combine(unreal, "Source", "CombatMath.h"), cpp.Code);
        var reverseBundle = ConversionBundle.Export(ProjectInspector.Inspect(unreal, unity, Engine.Unreal), Path.Combine(fixture, "reverse"));
        check(reverseBundle.Files.Single().Generated, "Reverse project script bundle generated");
        var artifacts = Path.GetFullPath("artifacts/converter-check"); Directory.CreateDirectory(artifacts);
        File.WriteAllText(Path.Combine(artifacts, "CombatMath.h"), cpp.Code);
        File.WriteAllText(Path.Combine(artifacts, "CombatMath.original.cs"), original);
        File.WriteAllText(Path.Combine(artifacts, "CombatMath.roundtrip.cs"), reverse.Code);
        File.WriteAllLines(Path.Combine(artifacts, "expected.txt"), expected);
        File.WriteAllText(Path.Combine(artifacts, "main.cpp"), """
#include <cstdio>
#include "CombatMath.h"
int main() {
    printf("%.17g\n", CombatMath::Damage(10.0, 3.0));
    printf("%.17g\n", CombatMath::Damage(2.0, 5.0));
    printf("%.17g\n", CombatMath::Scale(-2.5, 4.0));
    printf("%d\n", CombatMath::Alive(0.0));
    printf("%d\n", CombatMath::Alive(1.0));
    printf("%.17g\n", static_cast<double>(CombatMath::Blend(1.0f, 5.0f, 0.25f)));
    printf("%.17g\n", CombatMath::Combined(10.0, 3.0));
    return 0;
}
""");
    }

    private static Type Compile(string source)
    {
        var compilation = CSharpCompilation.Create("Converted_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        return Assembly.Load(stream.ToArray()).GetType("CombatMath")!;
    }
}
