using System.Text.Json;
using System.Text.Json.Nodes;

namespace EngineMigrator.Core;

public static class BlueprintNativePorting
{
    public static void InstallForEngine(string project,string editor,string adapters)=>InstallInto(project,ResolvePluginForEngine(Path.Combine(adapters,"unreal","EMBlueprintBridge"),editor));
    public static string ResolvePluginForEngine(string root,string editor)
    {
        root=Path.GetFullPath(root);ResourcePorting.GuardPath(root);
        using var engine=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(editor)!,"UnrealEditor.modules")));
        var id=engine.RootElement.GetProperty("BuildId").GetString()!;
        if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[A-Za-z0-9_-]+$"))throw new InvalidOperationException("잘못된 엔진 빌드 ID");
        foreach(var candidate in new[]{Path.Combine(Path.GetDirectoryName(root)!,"native",id,"EMBlueprintBridge"),root,Path.GetFullPath(Path.Combine(root,"..","..","..","artifacts","blueprint-versions",id,"EMBlueprintBridge"))})
        {
            ResourcePorting.GuardPath(candidate);var manifest=Path.Combine(candidate,"Binaries","Win64","UnrealEditor.modules");
            if(!File.Exists(manifest)||!File.Exists(Path.Combine(candidate,"EMBlueprintBridge.uplugin")))continue;
            using var module=JsonDocument.Parse(File.ReadAllText(manifest));if(module.RootElement.GetProperty("BuildId").GetString()==id)return candidate;
        }
        throw new InvalidOperationException("선택한 Unreal 빌드("+id+")용 Blueprint 브리지가 없습니다. tools/build-blueprint-bridge.ps1로 해당 엔진용 브리지를 빌드해 주세요.");
    }
    internal static void InstallInto(string project,string plugin)
    {
        var target=Path.Combine(project,"Plugins","EMBlueprintBridge");var sourceDescriptor=Path.Combine(plugin,"EMBlueprintBridge.uplugin");var descriptor=Path.Combine(target,"EMBlueprintBridge.uplugin");
        if(Directory.Exists(target)&&(!File.Exists(descriptor)||!File.ReadAllBytes(descriptor).SequenceEqual(File.ReadAllBytes(sourceDescriptor))))throw new InvalidOperationException("같은 이름의 다른 네이티브 브리지를 덮어쓰지 않습니다.");
        Directory.CreateDirectory(target);if(!File.Exists(descriptor))File.Copy(sourceDescriptor,descriptor);
        var sourceRoot=Path.Combine(plugin,"Source");
        if(Directory.Exists(sourceRoot))
        {
            var manifest=Path.Combine(target,".EngineMigratorBridgeSource.json");var previous=File.Exists(manifest)?JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(manifest))!:new();var next=new Dictionary<string,string>();
            static string Hash(string file)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
            var files=Directory.GetFiles(sourceRoot,"*",SearchOption.AllDirectories);
            foreach(var file in files){ResourcePorting.GuardPath(Path.GetDirectoryName(file)!);if(File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))throw new InvalidOperationException("링크 브리지 소스 미지원");var relative=Path.GetRelativePath(sourceRoot,file);var output=Path.Combine(target,"Source",relative);ResourcePorting.GuardPath(output);next[relative]=Hash(file);if(File.Exists(output)&&(File.GetAttributes(output).HasFlag(FileAttributes.ReparsePoint)||Hash(output)!=next[relative]&&(!previous.TryGetValue(relative,out var old)||Hash(output)!=old)))throw new InvalidOperationException("사용자 수정 브리지 소스를 덮어쓰지 않습니다: "+relative);}
            foreach(var file in files){var output=Path.Combine(target,"Source",Path.GetRelativePath(sourceRoot,file));Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.Copy(file,output,true);}File.WriteAllText(manifest,JsonSerializer.Serialize(next));
        }
        foreach(var file in Directory.EnumerateFiles(Path.Combine(plugin,"Binaries"),"*",SearchOption.AllDirectories))
        {
            ResourcePorting.GuardPath(Path.GetDirectoryName(file)!);if(File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))throw new InvalidOperationException("링크 네이티브 파일 미지원");
            var output=Path.Combine(target,"Binaries",Path.GetRelativePath(Path.Combine(plugin,"Binaries"),file));Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.Copy(file,output,true);
        }
        var projectFile=Directory.GetFiles(project,"*.uproject").Single();var json=JsonNode.Parse(File.ReadAllText(projectFile))!;var plugins=(JsonArray)json["Plugins"]!;var current=plugins.FirstOrDefault(p=>p?["Name"]?.GetValue<string>()=="EMBlueprintBridge");
        if(current is null)plugins.Add(new JsonObject{["Name"]="EMBlueprintBridge",["Enabled"]=true});else current["Enabled"]=true;File.WriteAllText(projectFile,json.ToJsonString());
    }
    public static async Task<string> RunAsync(string mode,string project,string input,string output,string editor,string plugin,string script,CancellationToken token=default)
    {
        EngineExecutionPolicy.AssertAllowed(output,project);
        if(mode is not ("import" or "export")) throw new ArgumentException("import/export 모드가 필요합니다.");
        project=Path.GetFullPath(project);output=Path.GetFullPath(output);plugin=Path.GetFullPath(plugin);
        ResourcePorting.GuardPath(project);ResourcePorting.GuardPath(output);ResourcePorting.GuardPath(plugin);
        plugin=ResolvePluginForEngine(plugin,editor);
        if(output.Equals(project,StringComparison.OrdinalIgnoreCase)||output.StartsWith(Path.TrimEndingDirectorySeparator(project)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("원본 내부에 출력할 수 없습니다.");
        ResourcePorting.ValidateUnrealVersion(project,editor);
        var pluginModules=Path.Combine(plugin,"Binaries","Win64","UnrealEditor.modules");
        var engineModules=Path.Combine(Path.GetDirectoryName(editor)!,"UnrealEditor.modules");
        using(var p=JsonDocument.Parse(File.ReadAllText(pluginModules)))using(var e=JsonDocument.Parse(File.ReadAllText(engineModules)))
            if(p.RootElement.GetProperty("BuildId").GetString()!=e.RootElement.GetProperty("BuildId").GetString()) throw new InvalidOperationException("Blueprint 플러그인을 선택한 엔진 빌드에 맞게 컴파일해야 합니다.");
        JsonNode data;
        if(mode=="import")
        {
            data=JsonNode.Parse(File.ReadAllText(input))!;
            var valid=BlueprintPorting.ToCSharp(data.ToJsonString());if(!valid.Generated) throw new NotSupportedException(string.Join("; ",valid.Diagnostics));
            data.AsObject().Remove("existingAsset");
        }
        else
        {
            if(!input.StartsWith("/Game/",StringComparison.Ordinal)||input.Contains("..")) throw new ArgumentException("/Game/ 하위 Blueprint 에셋 경로가 필요합니다.");
            data=new JsonObject { ["schema"]=1,["parent"]="BlueprintFunctionLibrary",["className"]="ExportedBlueprint",["existingAsset"]=input };
        }
        var run=Path.Combine(output,"BlueprintPort-"+Guid.NewGuid().ToString("N"));var copy=Path.Combine(run,"Project");var results=Path.Combine(run,"Results");Directory.CreateDirectory(results);
        using var cacheCleanup = BuildArtifactCleanup.Begin(run);
        await Task.Run(()=>ResourcePorting.CopyProject(project,copy,token),token);
        var targetPlugin=Path.Combine(copy,"Plugins","EMBlueprintBridge");var pluginDescriptor=Path.Combine(plugin,"EMBlueprintBridge.uplugin");var copiedDescriptor=Path.Combine(targetPlugin,"EMBlueprintBridge.uplugin");
        if(Directory.Exists(targetPlugin) && (!File.Exists(copiedDescriptor)||!File.ReadAllBytes(copiedDescriptor).SequenceEqual(File.ReadAllBytes(pluginDescriptor)))) throw new InvalidOperationException("동일 이름의 다른 플러그인이 있습니다. 덮어쓰지 않습니다.");
        Directory.CreateDirectory(targetPlugin);if(!File.Exists(copiedDescriptor))File.Copy(pluginDescriptor,copiedDescriptor);
        var nativeRoot=Path.Combine(plugin,"Binaries");foreach(var nativeFile in Directory.EnumerateFiles(nativeRoot,"*",SearchOption.AllDirectories)){token.ThrowIfCancellationRequested();ResourcePorting.GuardPath(Path.GetDirectoryName(nativeFile)!);if(File.GetAttributes(nativeFile).HasFlag(FileAttributes.ReparsePoint))throw new InvalidOperationException("링크 바이너리는 설치하지 않습니다.");var targetFile=Path.Combine(targetPlugin,"Binaries",Path.GetRelativePath(nativeRoot,nativeFile));Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);File.Copy(nativeFile,targetFile,true);}
        var descriptor=ResourcePorting.ConfigureUnreal(copy);var config=JsonNode.Parse(File.ReadAllText(descriptor))!;
        var plugins=(JsonArray)config["Plugins"]!;var installed=plugins.FirstOrDefault(p=>p?["Name"]?.GetValue<string>()=="EMBlueprintBridge");if(installed is null) plugins.Add(new JsonObject { ["Name"]="EMBlueprintBridge",["Enabled"]=true });else installed["Enabled"]=true;File.WriteAllText(descriptor,config.ToJsonString());
        var inputFile=Path.Combine(run,"input.json");File.WriteAllText(inputFile,data.ToJsonString());
        File.WriteAllText(Path.Combine(copy,"BlueprintRun.json"),JsonSerializer.Serialize(new { input=inputFile,output=results }));
        await ResourcePorting.RunEditor(editor,[descriptor,"-run=pythonscript","-script="+Path.GetFullPath(script),"-nullrhi","-unattended","-nop4","-abslog="+Path.Combine(run,"blueprint.log")],run,token);
        var graph=File.ReadAllText(Path.Combine(results,"blueprint-graph.json"));var cs=BlueprintPorting.ToCSharp(graph);if(!cs.Generated) throw new InvalidOperationException(string.Join("; ",cs.Diagnostics));
        File.WriteAllText(Path.Combine(results,cs.FileName!),cs.Code);
        static string E(string text)=>System.Net.WebUtility.HtmlEncode(text);
        File.WriteAllText(Path.Combine(run,"blueprint-port.html"),"<!doctype html><html lang=ko><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\"><title>Blueprint 함수 변환</title><style>body{background:#211e1a;color:#f5efe5;font:15px/1.8 system-ui;max-width:1000px;margin:40px auto;padding:20px;overflow-wrap:anywhere}h1,h2{color:#f3ab73}pre{white-space:pre-wrap;background:#302b25;padding:20px}</style><h1>Blueprint 함수 변환</h1><p>제한된 순수 함수 그래프 변환 결과입니다. 일반 Actor·이벤트·상태·부모 호출 및 전체 프로젝트 포팅은 미완료입니다. 함수 라이브러리는 Actor/GameObject에 부착하는 컴포넌트가 아닙니다.</p><p>실제 작업 프로젝트: "+E(copy)+"<br>생성 C#: "+E(Path.Combine(results,cs.FileName!))+"</p><h2>실제 그래프의 입력·분기·반환·호출 표현</h2><pre>"+E(graph)+"</pre><h2>대응 C# 함수</h2><pre>"+E(cs.Code!)+"</pre></html>");
        File.WriteAllText(Path.Combine(run,"result.json"),JsonSerializer.Serialize(new { project=copy,generatedCode=Path.Combine(results,cs.FileName!),graph=Path.Combine(results,"blueprint-graph.json"),verification=Path.Combine(results,"blueprint-execution.json"),projectPortComplete=false },new JsonSerializerOptions { WriteIndented=true }));
        return run;
    }
}
