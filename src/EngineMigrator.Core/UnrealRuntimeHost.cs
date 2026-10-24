using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp;

namespace EngineMigrator.Core;

/// <summary>Explicit game targets ensure UAT builds generated runtime modules instead of staging a generic executable.</summary>
public static class UnrealRuntimeHost
{
    const string Manifest=".EngineMigratorRuntimeHost.json";
    public static void Ensure(string project)
    {
        ResourcePorting.GuardPath(project);VerifySource(project);
        var descriptor=Directory.GetFiles(project,"*.uproject").Single();var data=JsonNode.Parse(File.ReadAllText(descriptor))!;
        if(data["Modules"] is JsonArray modules&&modules.Count>0)return;
        var name=Path.GetFileNameWithoutExtension(descriptor);
        if(!SyntaxFacts.IsValidIdentifier(name)||SyntaxFacts.GetKeywordKind(name)!=Microsoft.CodeAnalysis.CSharp.SyntaxKind.None)throw new NotSupportedException("게임 타깃을 생성할 수 없는 프로젝트 이름: "+name);
        var files=new Dictionary<string,string>{
            ["Source/EMPortHost/EMPortHost.Build.cs"]="using UnrealBuildTool; public class EMPortHost : ModuleRules { public EMPortHost(ReadOnlyTargetRules Target) : base(Target) { PCHUsage=PCHUsageMode.UseExplicitOrSharedPCHs; PublicDependencyModuleNames.AddRange(new[]{\"Core\",\"CoreUObject\",\"Engine\"}); } }\n",
            ["Source/EMPortHost/EMPortHost.cpp"]="#include \"Modules/ModuleManager.h\"\nIMPLEMENT_PRIMARY_GAME_MODULE(FDefaultGameModuleImpl, EMPortHost, \""+name+"\");\n"
        };
        foreach(var suffix in new[]{"","Editor"})files["Source/"+name+suffix+".Target.cs"]="using UnrealBuildTool; public class "+name+suffix+"Target : TargetRules { public "+name+suffix+"Target(TargetInfo Target) : base(Target) { Type=TargetType."+(suffix.Length==0?"Game":"Editor")+"; DefaultBuildSettings=BuildSettingsVersion.Latest; IncludeOrderVersion=EngineIncludeOrderVersion.Latest; ExtraModuleNames.Add(\"EMPortHost\"); } }\n";
        foreach(var file in files){var path=Path.Combine(project,file.Key);ResourcePorting.GuardPath(path);if(File.Exists(path)&&(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)||File.ReadAllText(path)!=file.Value))throw new InvalidOperationException("다른 게임 타깃/호스트 파일을 덮어쓰지 않습니다: "+file.Key);}
        foreach(var file in files){var path=Path.Combine(project,file.Key);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,file.Value);}
        data["Modules"]=new JsonArray(new JsonObject{["Name"]="EMPortHost",["Type"]="Runtime",["LoadingPhase"]="Default"});File.WriteAllText(descriptor,data.ToJsonString());
        File.WriteAllText(Path.Combine(project,Manifest),JsonSerializer.Serialize(files.ToDictionary(f=>f.Key,f=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(f.Value))))));
    }
    public static void VerifySource(string project)
    {
        var path=Path.Combine(project,Manifest);if(!File.Exists(path))return;
        var descriptor=JsonNode.Parse(File.ReadAllText(Directory.GetFiles(project,"*.uproject").Single()))!;
        if(descriptor["Modules"] is not JsonArray modules||modules.Count!=1||modules[0]?["Name"]?.GetValue<string>()!="EMPortHost"||modules[0]?["Type"]?.GetValue<string>()!="Runtime")throw new NotSupportedException("수정된 게임 호스트 모듈 구성은 자동 역변환할 수 없습니다.");
        foreach(var file in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))!){var absolute=Path.GetFullPath(Path.Combine(project,file.Key));var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(project))+Path.DirectorySeparatorChar;if(!absolute.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("게임 호스트 매니페스트 경로 오류");ResourcePorting.GuardPath(absolute);if(!File.Exists(absolute)||File.GetAttributes(absolute).HasFlag(FileAttributes.ReparsePoint)||Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(absolute)))!=file.Value)throw new NotSupportedException("수정된 게임 호스트 코드는 자동 역변환할 수 없습니다: "+file.Key);}
    }
    internal static string EditorTarget(string project)
    {
        var name=Path.GetFileNameWithoutExtension(Directory.GetFiles(project,"*.uproject").Single())+"Editor";
        return File.Exists(Path.Combine(project,"Source",name+".Target.cs"))?name:"UnrealEditor";
    }
}
