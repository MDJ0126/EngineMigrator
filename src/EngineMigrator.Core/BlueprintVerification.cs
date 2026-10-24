using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EngineMigrator.Core;

public static class BlueprintVerification
{
    public static int Verify(string original,string generated,string inputJson,string executionJson)
    {
        Type Compile(string source,string name)
        {
            if(!PortableScriptConverter.Convert(source,Engine.Unity).Generated) throw new InvalidOperationException("제한된 순수 함수만 실행 검증할 수 있습니다.");
            var compilation=CSharpCompilation.Create("BlueprintCheck_"+Guid.NewGuid().ToString("N"),[CSharpSyntaxTree.ParseText(source)],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream=new MemoryStream();var result=compilation.Emit(stream);
            if(!result.Success) throw new InvalidOperationException(string.Join("\n",result.Diagnostics));
            return Assembly.Load(stream.ToArray()).GetType(name)??throw new InvalidOperationException("검증 클래스 없음");
        }
        using var input=JsonDocument.Parse(inputJson);using var output=JsonDocument.Parse(executionJson);
        if(!output.RootElement.GetProperty("compiled").GetBoolean()) throw new InvalidOperationException("Blueprint 컴파일 미통과");
        var name=input.RootElement.GetProperty("className").GetString()!;
        var first=Compile(original,name);var last=Compile(generated,name);
        var cases=input.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        var actual=output.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        if(cases.Length==0||cases.Length!=actual.Length) throw new InvalidOperationException("실행 사례 누락");
        for(var i=0;i<cases.Length;i++)
        {
            var method=cases[i].GetProperty("function").GetString()!;
            if(actual[i].GetProperty("function").GetString()!=method) throw new InvalidOperationException("호출 함수 불일치");
            var args=cases[i].GetProperty("args").EnumerateArray().Select(a=>(object)a.GetDouble()).ToArray();
            var expected=(double)first.GetMethod(method)!.Invoke(null,args)!;
            var restored=(double)last.GetMethod(method)!.Invoke(null,args)!;
            var native=actual[i].GetProperty("result").GetDouble();
            if(expected!=native||expected!=restored) throw new InvalidOperationException($"실행 결과 불일치 {method}: C#={expected:R}, Blueprint={native:R}, 왕복={restored:R}");
        }
        return cases.Length;
    }
}
