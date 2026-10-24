using System.Text.Json;
using System.Text.Json.Nodes;

namespace EngineMigrator.Core;

public static class ActorProjectPorting
{
    public static async Task<string> RunAsync(string source,string unrealProject,string output,string editor,CancellationToken token=default)
    {
        EngineExecutionPolicy.AssertAllowed(output,unrealProject);
        var files=ActorScripts.FromUnity(source);unrealProject=Path.GetFullPath(unrealProject);output=Path.GetFullPath(output);
        ResourcePorting.GuardPath(unrealProject);ResourcePorting.GuardPath(output);ResourcePorting.ValidateUnrealVersion(unrealProject,editor);
        if(output.Equals(unrealProject,StringComparison.OrdinalIgnoreCase)||output.StartsWith(Path.TrimEndingDirectorySeparator(unrealProject)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("원본 프로젝트 외부로 출력해 주세요.");
        var run=Path.Combine(output,"ActorPort-"+Guid.NewGuid().ToString("N"));var project=Path.Combine(run,"Project");
        using var cacheCleanup = BuildArtifactCleanup.Begin(run);
        await Task.Run(()=>ResourcePorting.CopyProject(unrealProject,project,token),token);
        UnrealRuntimeHost.Ensure(project);
        var plugin=Path.Combine(project,"Plugins","EMActorPort");if(Directory.Exists(plugin))throw new InvalidOperationException("동일 이름의 Actor 포팅 플러그인이 있습니다. 덮어쓰지 않습니다.");
        var module=Path.Combine(plugin,"Source","EMActorPort");var headers=Path.Combine(module,"Public");Directory.CreateDirectory(headers);Directory.CreateDirectory(Path.Combine(module,"Private"));
        File.WriteAllText(Path.Combine(plugin,"EMActorPort.uplugin"),"{\"FileVersion\":3,\"Version\":1,\"FriendlyName\":\"EngineMigrator Actor Port\",\"CanContainContent\":false,\"Modules\":[{\"Name\":\"EMActorPort\",\"Type\":\"Runtime\",\"LoadingPhase\":\"Default\"}]}");
        File.WriteAllText(Path.Combine(module,"EMActorPort.Build.cs"),"using UnrealBuildTool; public class EMActorPort : ModuleRules { public EMActorPort(ReadOnlyTargetRules Target) : base(Target) { PCHUsage=PCHUsageMode.UseExplicitOrSharedPCHs; FPSemantics=FPSemanticsMode.Precise; PublicDependencyModuleNames.AddRange(new[]{\"Core\",\"CoreUObject\",\"Engine\",\"LevelSequence\",\"MovieScene\"}); } }");
        File.WriteAllText(Path.Combine(module,"Private","EMActorPort.cpp"),"#include \"Modules/ModuleManager.h\"\nIMPLEMENT_MODULE(FDefaultModuleImpl, EMActorPort)\n");
        foreach(var file in files)File.WriteAllText(Path.Combine(headers,file.Name),file.Code);
        var descriptor=Directory.GetFiles(project,"*.uproject").Single();var settings=JsonNode.Parse(File.ReadAllText(descriptor))!;
        var plugins=settings["Plugins"] as JsonArray??new JsonArray();if(settings["Plugins"] is null)settings["Plugins"]=plugins;
        plugins.Add(new JsonObject{["Name"]="EMActorPort",["Enabled"]=true});File.WriteAllText(descriptor,settings.ToJsonString());
        var engine=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(editor)!,"..",".."));
        var dotnet=Directory.GetFiles(Path.Combine(engine,"Binaries","ThirdParty","DotNet"),"dotnet.exe",SearchOption.AllDirectories).Single();
        var ubt=Path.Combine(engine,"Binaries","DotNET","UnrealBuildTool","UnrealBuildTool.dll");
        await ResourcePorting.RunEditor(dotnet,[ubt,UnrealRuntimeHost.EditorTarget(project),"Win64","Development","-Project="+descriptor,"-plugin="+Path.Combine(plugin,"EMActorPort.uplugin"),"-NoHotReload","-MaxParallelActions=1","-log="+Path.Combine(run,"actor-build.log")],run,token);
        var back=ActorScripts.ToUnity(files.Select(f=>new ActorFile(f.Name,File.ReadAllText(Path.Combine(headers,f.Name)))).ToArray());var restored=Path.Combine(run,"Restored");Directory.CreateDirectory(restored);foreach(var file in back)File.WriteAllText(Path.Combine(restored,file.Name),file.Code);
        var mapping=files.Select(f=>new{sourceClass=Path.GetFileNameWithoutExtension(f.Name),targetClass="A"+Path.GetFileNameWithoutExtension(f.Name),nativeClassPath="/Script/EMActorPort."+Path.GetFileNameWithoutExtension(f.Name),header=Path.Combine(headers,f.Name)}).ToArray();
        var result=JsonSerializer.Serialize(new{project,compiled=true,classes=mapping,sceneAttachmentVerified=false,projectPortComplete=false},new JsonSerializerOptions{WriteIndented=true});File.WriteAllText(Path.Combine(run,"actor-port-result.json"),result);
        File.WriteAllText(Path.Combine(run,"actor-port.html"),"<!doctype html><meta charset=utf-8><style>body{background:#211e1a;color:#f5efe5;font:16px/1.8 system-ui;padding:30px}pre{white-space:pre-wrap;overflow-wrap:anywhere}</style><h1>MonoBehaviour → Actor 클래스 변환</h1><p>실제 Unreal 클래스 컴파일 결과입니다. 장면 부착·외부 객체 참조·전체 프로젝트 포팅은 미완료입니다. Awake → PostInitializeComponents, Start → BeginPlay, Update → Tick, OnDestroy → Destroyed의 공통 래퍼는 상속 루트에 한 번만 생성합니다. 오버라이드의 부모 호출은 코드에 명시된 경우에만 실행합니다.</p><pre>"+System.Net.WebUtility.HtmlEncode(result)+"</pre>");
        return run;
    }
}
