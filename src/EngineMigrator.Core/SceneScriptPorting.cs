using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp;

namespace EngineMigrator.Core;

public static class SceneScriptPorting
{
    public static string? ReadSource(string package)
    {
        var json=JsonNode.Parse(File.ReadAllText(Path.Combine(package,"package.json")))!;
        if(json["scripts"] is not JsonArray scripts||scripts.Count==0)return null;
        var classes=new List<string>();var sceneUsing=false;
        foreach(var script in scripts)
        {
            var root=CSharpSyntaxTree.ParseText(script!["source"]!.GetValue<string>()).GetCompilationUnitRoot();
            if(root.ContainsDirectives||root.Usings.Any(u=>u.ToString() is not ("using UnityEngine;" or "using UnityEngine.SceneManagement;" or "using UnityEngine.UI;")))throw new NotSupportedException("장면 스크립트 using/전처리 미지원");
            sceneUsing|=root.Usings.Any(u=>u.ToString()=="using UnityEngine.SceneManagement;");
            classes.AddRange(root.Members.Select(m=>m.ToFullString()));
        }
        var source="using UnityEngine;\nusing UnityEngine.UI;\n"+(sceneUsing?"using UnityEngine.SceneManagement;\n":"")+string.Join("\n",classes);
        ActorScripts.FromUnity(source);return source;
    }
    internal static async Task CompileIntoAsync(string? source,string project,string editor,string log,CancellationToken token)
    {
        UnrealRuntimeHost.Ensure(project);
        var files=source is null?Array.Empty<ActorFile>():ActorScripts.FromUnity(source);var plugin=Path.Combine(project,"Plugins","EMActorPort");
        if(Directory.Exists(plugin))throw new InvalidOperationException("장면 스크립트 플러그인 이름 충돌: EMActorPort");
        var module=Path.Combine(plugin,"Source","EMActorPort");var headers=Path.Combine(module,"Public");Directory.CreateDirectory(headers);Directory.CreateDirectory(Path.Combine(module,"Private"));
        File.WriteAllText(Path.Combine(plugin,"EMActorPort.uplugin"),"{\"FileVersion\":3,\"Version\":1,\"FriendlyName\":\"EngineMigrator Scene Scripts\",\"CanContainContent\":false,\"Modules\":[{\"Name\":\"EMActorPort\",\"Type\":\"Runtime\",\"LoadingPhase\":\"Default\"}]}");
        File.WriteAllText(Path.Combine(module,"EMActorPort.Build.cs"),"using UnrealBuildTool; public class EMActorPort : ModuleRules { public EMActorPort(ReadOnlyTargetRules Target) : base(Target) { PCHUsage=PCHUsageMode.UseExplicitOrSharedPCHs; FPSemantics=FPSemanticsMode.Precise; PublicDependencyModuleNames.AddRange(new[]{\"Core\",\"CoreUObject\",\"Engine\",\"LevelSequence\",\"MovieScene\"}); } }");
        File.WriteAllText(Path.Combine(module,"Private","EMActorPort.cpp"),"#include \"Modules/ModuleManager.h\"\nIMPLEMENT_MODULE(FDefaultModuleImpl, EMActorPort)\n"+TimeScalePorting.ModuleCode);
        foreach(var file in files)File.WriteAllText(Path.Combine(headers,file.Name),file.Code);
        var descriptor=Directory.GetFiles(project,"*.uproject").Single();var settings=JsonNode.Parse(File.ReadAllText(descriptor))!;var plugins=(JsonArray)settings["Plugins"]!;
        var existing=plugins.FirstOrDefault(p=>p?["Name"]?.GetValue<string>()=="EMActorPort");if(existing is null)plugins.Add(new JsonObject{["Name"]="EMActorPort",["Enabled"]=true});else existing["Enabled"]=true;
        File.WriteAllText(descriptor,settings.ToJsonString());
        var engine=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(editor)!,"..",".."));var dotnet=Directory.GetFiles(Path.Combine(engine,"Binaries","ThirdParty","DotNet"),"dotnet.exe",SearchOption.AllDirectories).Single();
        await ResourcePorting.RunEditor(dotnet,[Path.Combine(engine,"Binaries","DotNET","UnrealBuildTool","UnrealBuildTool.dll"),UnrealRuntimeHost.EditorTarget(project),"Win64","Development","-Project="+descriptor,"-NoHotReload","-MaxParallelActions=1","-log="+log],project,token);
    }
    internal static void RestoreInto(string package,string unrealProject,string unityProject,JsonArray? projectScripts=null)
    {
        var data=JsonNode.Parse(File.ReadAllText(Path.Combine(package,"package.json")))!;
        if(projectScripts is not null)data["scripts"]=projectScripts.DeepClone();
        if(data["actorGraphs"] is JsonObject graph){var code=ActorBlueprintGraphs.ToUnity(graph.ToJsonString());var graphRoot=Path.Combine(unityProject,"Assets","EngineMigratorScripts");Directory.CreateDirectory(graphRoot);foreach(var cls in CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot().Members){var name=((Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax)cls).Identifier.ValueText+".cs";if(Directory.GetFiles(Path.Combine(unityProject,"Assets"),name,SearchOption.AllDirectories).Length>0)throw new InvalidOperationException("복원 스크립트 이름 충돌: "+name);File.WriteAllText(Path.Combine(graphRoot,name),"using UnityEngine;\n"+cls.ToFullString());ScriptIdentityPorting.Restore(data,Path.Combine(graphRoot,name),unityProject);}return;}
        // The first project unit may have no scripts; the merged project catalog is authoritative.
        if(projectScripts is not null){if(projectScripts.Count==0)return;}else if(ReadSource(package)is null)return;
        var headers=Path.Combine(unrealProject,"Plugins","EMActorPort","Source","EMActorPort","Public");
        var files=ActorScripts.ToUnity(Directory.GetFiles(headers,"*.h").Select(p=>new ActorFile(Path.GetFileName(p),File.ReadAllText(p))).ToArray());
        var root=Path.Combine(unityProject,"Assets","EngineMigratorScripts");Directory.CreateDirectory(root);
        foreach(var file in files)
        {
            // Refuse duplicate classes: callers must select an empty target or remove
            // only the corresponding original class files in their private work copy.
            if(Directory.GetFiles(Path.Combine(unityProject,"Assets"),file.Name,SearchOption.AllDirectories).Length>0)throw new InvalidOperationException("복원 스크립트 이름 충돌: "+file.Name);
            File.WriteAllText(Path.Combine(root,file.Name),file.Code);
            ScriptIdentityPorting.Restore(data,Path.Combine(root,file.Name),unityProject);
        }
    }
}
