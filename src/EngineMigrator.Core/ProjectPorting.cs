using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
namespace EngineMigrator.Core;

/// <summary>Project-scoped transactions: diagnose every unit before importing any target map.</summary>
public static class ProjectPorting
{
    public static bool NeedsProjectMode(InspectionReport report)=>report.SourceEngine==Engine.Unity
        ?File.Exists(Path.Combine(report.Source,".EngineMigratorProject.json"))||report.Assets.Count(a=>a.Path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase))>1||report.Assets.Any(a=>a.Path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
        :File.Exists(Path.Combine(report.Source,".EngineMigratorProject.json"));
    public static int ValidatePrepared(string root){var units=ValidatePackages(root);PrepareScripts(units);return units.Count;}
    internal static IReadOnlyList<(JsonObject Unit,string Folder,JsonObject Data)> ValidatePackages(string root)
    {
        var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"project-package.json")))!.AsObject();
        if(manifest["schema"]?.GetValue<int>()!=1||manifest["blockers"]!.AsArray().Count>0)throw new InvalidOperationException("프로젝트 사전 검사 차단: "+manifest["blockers"]!.ToJsonString());
        var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var resources=new Dictionary<string,(JsonNode Data,string? Hash)>();var units=new List<(JsonObject,string,JsonObject)>();
        foreach(var entry in manifest["units"]!.AsArray())
        {
            var unit=entry!.AsObject();var id=unit["id"]!.GetValue<string>();var relative=unit["package"]!.GetValue<string>();
            if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^[A-Za-z0-9_]+$")||!ids.Add(id)||relative!="units/"+id||unit["kind"]!.GetValue<string>() is not("scene" or "prefab"))throw new InvalidOperationException("프로젝트 단위 ID/경로/유형 오류: "+id);
            var folder=Path.Combine(root,"units",id);ResourcePorting.GuardPath(folder);ResourcePorting.RequirePackage(folder);var data=JsonNode.Parse(File.ReadAllText(Path.Combine(folder,"package.json")))!.AsObject();
            foreach(var kind in new[]{"textures","materials","meshes"})foreach(var resource in data[kind]!.AsArray())
            {
                var key=kind+":"+resource!["id"]!.GetValue<string>();string? hash=null;
                if(kind=="textures"){var file=Path.GetFullPath(Path.Combine(folder,resource["file"]!.GetValue<string>()));if(!file.StartsWith(Path.GetFullPath(folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("공유 텍스처 파일 경로 오류");ResourcePorting.GuardPath(file);hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));}
                if(resources.TryGetValue(key,out var previous)){if(!JsonNode.DeepEquals(previous.Data,resource)||previous.Hash!=hash)throw new InvalidOperationException("서로 다른 자산이 같은 ID를 사용합니다: "+key+" / "+unit["path"]);}
                else resources.Add(key,(resource.DeepClone(),hash));
            }
            unit["nodeCount"]=data["nodes"]!.AsArray().Count;units.Add((unit,folder,data));
        }
        if(!units.Any(u=>u.Item1["kind"]!.GetValue<string>()=="scene"&&u.Item1["enabled"]!.GetValue<bool>()))throw new InvalidOperationException("활성 빌드 장면이 없습니다.");
        var definitions=units.Where(u=>u.Item1["kind"]!.GetValue<string>()=="prefab").Select(u=>u.Item1["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        foreach(var unit in units)foreach(var node in unit.Item3["nodes"]!.AsArray())if(node!["behaviours"] is JsonArray behaviours)foreach(var behaviour in behaviours)foreach(var field in behaviour!["fields"]!.AsArray())if(field!["kind"]!.GetValue<string>()=="prefab"&&!string.IsNullOrEmpty(field["value"]?.GetValue<string>())){
            var id=field["value"]!.GetValue<string>();if(!definitions.Contains(id))throw new InvalidOperationException("런타임 프리팹 정의가 없습니다: "+id);
            var definition=units.Single(u=>u.Item1["id"]!.GetValue<string>()==id);foreach(var member in definition.Item3["nodes"]!.AsArray())if(new[]{"hasCamera","hasLight","hasVolume"}.Any(k=>member![k]?.GetValue<bool>()==true))throw new NotSupportedException("런타임 프리팹의 카메라/라이트/Volume 구성은 아직 미지원입니다: "+id+" / "+member!["name"]);
        }
        var nearClips=new List<double>();
        foreach(var unit in units){var nodes=unit.Item3["nodes"]!.AsArray();var nodeIds=new HashSet<string>();foreach(var node in nodes){var id=node!["id"]!.GetValue<string>();if(!nodeIds.Add(id))throw new InvalidOperationException("장면 안의 객체 ID 중복: "+unit.Item1["path"]+" / "+id);}
            var components=nodes.SelectMany(n=>(n!["behaviours"] as JsonArray??new JsonArray())).Select(b=>b?["id"]?.GetValue<string>()).Where(id=>id is not null).ToHashSet();
            foreach(var node in nodes)foreach(var behaviour in node!["behaviours"] as JsonArray??new JsonArray())foreach(var field in behaviour!["fields"]!.AsArray())if(field!["kind"]!.GetValue<string>()=="component"&&!string.IsNullOrEmpty(field["value"]?.GetValue<string>())&&!components.Contains(field["value"]!.GetValue<string>()))throw new InvalidOperationException("장면 외부 컴포넌트 참조는 아직 미지원입니다: "+field["name"]);
            foreach(var node in nodes){
                if(node!["prefab"] is JsonObject prefab&&!string.IsNullOrEmpty(prefab["asset"]?.GetValue<string>())&&(!definitions.Contains(prefab["asset"]!.GetValue<string>())||!nodeIds.Contains(prefab["root"]!.GetValue<string>())))throw new InvalidOperationException("프리팹 정의/루트 참조가 없습니다: "+unit.Item1["path"]+" / "+node["name"]);
                if(node["behaviours"] is JsonArray behaviours)foreach(var behaviour in behaviours)foreach(var field in behaviour!["fields"]!.AsArray())if(field!["kind"]!.GetValue<string>() is "node" or "transform"&&field["value"]!.GetValue<string>() is string value&&value.Length>0&&!nodeIds.Contains(value))throw new InvalidOperationException("장면 외부 객체 참조는 아직 미지원입니다: "+unit.Item1["path"]+" / "+node["name"]+"."+field["name"]);
                if(node["hasCamera"]?.GetValue<bool>()==true&&node["camera"]!["orthographic"]?.GetValue<bool>()==false)nearClips.Add(node["camera"]!["near"]!.GetValue<double>());
            }
        }
        if(nearClips.Count>0&&nearClips.Any(n=>Math.Abs(n-nearClips[0])>1e-6))throw new NotSupportedException("서로 다른 장면의 원근 Near Clip 값은 Unreal 전역 설정으로 동시에 보존할 수 없습니다. 카메라 근거리 클립을 통일한 뒤 다시 검사해 주세요.");
        File.WriteAllText(Path.Combine(root,"project-package.json"),manifest.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));return units;
    }
    internal static string? PrepareScripts(IReadOnlyList<(JsonObject Unit,string Folder,JsonObject Data)> units)
    {
        var scripts=new Dictionary<string,JsonNode>();
        foreach(var unit in units)if(unit.Data["scripts"] is JsonArray entries)foreach(var script in entries){var name=script!["name"]!.GetValue<string>();if(scripts.TryGetValue(name,out var old)&&!JsonNode.DeepEquals(old,script))throw new InvalidOperationException("장면 사이 스크립트 정의가 다릅니다: "+name);scripts[name]=script.DeepClone();}
        if(scripts.Count==0)return null;
        var scenes=units.Where(u=>u.Unit["kind"]!.GetValue<string>()=="scene"&&u.Unit["enabled"]!.GetValue<bool>()).ToArray();
        var merged=new JsonObject{["scripts"]=new JsonArray(scripts.Values.Select(s=>s.DeepClone()).ToArray())};var temp=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(units[0].Folder))!,"script-preflight");Directory.CreateDirectory(temp);File.WriteAllText(Path.Combine(temp,"package.json"),merged.ToJsonString());
        try{var source=SceneScriptPorting.ReadSource(temp);if(source is not null)ValidateSceneCalls(source,scenes.Select(s=>s.Unit).ToArray());return source;}catch(Exception ex) when(ex is NotSupportedException or InvalidOperationException){File.WriteAllText(Path.Combine(temp,"diagnostics.json"),JsonSerializer.Serialize(new{status="BLOCKED",stage="before-target-import",files=scripts.Keys,message=ex.Message,suggestion="지원 밖 문법·엔진 API를 포팅 지원 코드로 분리한 뒤 다시 검사해 주세요."}));throw new NotSupportedException("장면 대상 생성 전 스크립트 사전 검사 차단: "+string.Join(", ",scripts.Keys)+" — "+ex.Message,ex);}
    }
    internal static void ValidateSceneCalls(string source,IReadOnlyList<JsonObject> scenes)
    {
        var tree=Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source);
        var compilation=Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("ProjectSceneValidation",[tree,Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(ActorScripts.ValidationStub)],[Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        var semantic=compilation.GetSemanticModel(tree);
        foreach(var call in tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>())
        {
            if(semantic.GetSymbolInfo(call).Symbol is not Microsoft.CodeAnalysis.IMethodSymbol method||method.Name!="LoadScene"||method.ContainingType.ToDisplayString()!="UnityEngine.SceneManagement.SceneManager")continue;
            if(call.ArgumentList.Arguments.Count!=1)throw new NotSupportedException("LoadScene은 단일 장면 이름 또는 int 빌드 인덱스만 지원합니다.");
            var argument=call.ArgumentList.Arguments[0].Expression;var value=semantic.GetConstantValue(argument);
            if(!value.HasValue){if(semantic.GetTypeInfo(argument).Type?.SpecialType==Microsoft.CodeAnalysis.SpecialType.System_Int32)continue;throw new NotSupportedException("동적 LoadScene 인수는 int 빌드 인덱스여야 합니다.");}
            var valid=value.Value is int index?index>=0&&index<scenes.Count:value.Value is string name&&scenes.Count(u=>string.Equals(u["name"]!.GetValue<string>(),name,StringComparison.OrdinalIgnoreCase)||Path.ChangeExtension(u["path"]!.GetValue<string>(),null)==name||Path.ChangeExtension(u["actualPath"]?.GetValue<string>()??"",null)==name)==1;
            if(!valid)throw new NotSupportedException("LoadScene 대상이 활성 빌드 장면에 없거나 이름이 중복됩니다: "+argument);
        }
    }
    internal static async Task<ResourcePortResult> RunAsync(InspectionReport report,string outputRoot,EngineExecutables engines,string adapters,IProgress<string>? progress,CancellationToken token)
    {
        EngineExecutionPolicy.AssertAllowed(outputRoot);
        var run=Path.Combine(Path.GetFullPath(outputRoot),"PROJECT-"+Guid.NewGuid().ToString("N")[..12]);var source=Path.Combine(run,"Source");var target=Path.Combine(run,"Target");var transfer=Path.Combine(run,"Transfer");Directory.CreateDirectory(transfer);progress?.Report("프로젝트 작업 폴더: "+run);
        using var cacheCleanup = BuildArtifactCleanup.Begin(run);
        await Task.Run(()=>{ResourcePorting.CopyProject(report.Source,source,token);ResourcePorting.CopyProject(report.Target,target,token);},token);
        var unity=report.SourceEngine==Engine.Unity?source:target;var unreal=report.SourceEngine==Engine.Unreal?source:target;var unityOriginal=report.SourceEngine==Engine.Unity?report.Source:report.Target;
        var cache=Path.Combine(unityOriginal,"Library","PackageCache");if(Directory.Exists(cache))await Task.Run(()=>ResourcePorting.SeedPackageCache(cache,Path.Combine(unity,"Library","PackageCache"),token),token);
        ResourcePorting.InstallUnityAdapter(unity,adapters);var descriptor=ResourcePorting.ConfigureUnreal(unreal);
        if(report.SourceEngine==Engine.Unity)
        {
            progress?.Report("모든 장면·프리팹 정의와 끊어진 GUID/객체 참조를 대상 생성 전에 검사합니다.");
            try{await ResourcePorting.RunEditor(engines.Unity,ResourcePorting.UnityArgs(unity,transfer,"export-project",Path.Combine(run,"unity-project-export.log")),run,token);}
            catch(InvalidOperationException) when(File.Exists(Path.Combine(transfer,"preflight.json"))){var diagnostics=JsonNode.Parse(File.ReadAllText(Path.Combine(transfer,"preflight.json")))!;throw new InvalidOperationException("프로젝트 사전 검사 실패 — "+Path.Combine(transfer,"preflight.json")+"\n"+diagnostics["blockers"]!.ToJsonString());}
            var units=ValidatePackages(transfer);var code=PrepareScripts(units);string? graph=null;var timingRuntime=units.Any(u=>TimeScalePorting.NeedsRuntime(u.Data));
            if(code is not null){try{if(!timingRuntime&&!units.Any(u=>(u.Data["scripts"] as JsonArray??new JsonArray()).Any(s=>(s!["executionOrder"]?.GetValue<int>()??0)!=0))&&!units.Any(u=>u.Data["nodes"]!.AsArray().Any(n=>(n!["behaviours"] as JsonArray??new JsonArray()).Any(b=>b!["enabled"]?.GetValue<bool>()==false||n["effectiveActive"]?.GetValue<bool>()==false||n["active"]?.GetValue<bool>()==false))))graph=ActorBlueprintGraphs.FromUnity(code);}catch(NotSupportedException){}if(graph is null)await SceneScriptPorting.CompileIntoAsync(code,unreal,engines.Unreal,Path.Combine(run,"actor-build.log"),token);}
            if(code is null&&timingRuntime)await SceneScriptPorting.CompileIntoAsync(null,unreal,engines.Unreal,Path.Combine(run,"actor-build.log"),token);
            foreach(var unit in units){if(graph is not null)unit.Data["actorGraphs"]=JsonNode.Parse(graph);File.WriteAllText(Path.Combine(unit.Folder,"package.json"),unit.Data.ToJsonString());ResourcePorting.PrepareUnrealSceneSettings(unit.Folder,unreal);}
            if(units.Any(u=>u.Data["nodes"]!.AsArray().Any(n=>n!["hasUI"]?.GetValue<bool>()==true)))await UiRuntimePorting.CompileAsync(unreal,engines.Unreal,Path.Combine(run,"ui-build.log"),token);
            BlueprintNativePorting.InstallInto(unreal,BlueprintNativePorting.ResolvePluginForEngine(Path.Combine(adapters,"unreal","EMBlueprintBridge"),engines.Unreal));
            ResourcePorting.WriteConfig(unreal,new{mode="import",projectPackage=transfer,assetRoot="/Game/EngineMigratorProject_"+Guid.NewGuid().ToString("N")[..8]});progress?.Report("공유 자산을 재사용하며 각 장면·프리팹 정의 맵을 생성·저장·재열기합니다.");
            await ResourcePorting.RunEditor(engines.Unreal,ResourcePorting.UnrealArgs(descriptor,Path.Combine(adapters,"unreal","project_bridge.py"),Path.Combine(run,"unreal-project-import.log")),run,token);
            using var result=JsonDocument.Parse(File.ReadAllText(Path.Combine(transfer,"unreal-import-result.json")));var map=result.RootElement.GetProperty("savedMap").GetString();File.AppendAllText(Path.Combine(unreal,"Config","DefaultEngine.ini"),"\n[/Script/EngineSettings.GameMapsSettings]\nEditorStartupMap="+map+"\nGameDefaultMap="+map+"\n");
            var cookMaps=result.RootElement.GetProperty("maps").EnumerateArray().Where(u=>u.GetProperty("kind").GetString()=="scene"&&u.GetProperty("enabled").GetBoolean()).Select(u=>u.GetProperty("map").GetString()).ToArray();
            File.AppendAllText(Path.Combine(unreal,"Config","DefaultGame.ini"),"\n[/Script/UnrealEd.ProjectPackagingSettings]\n"+string.Join("\n",cookMaps.Select(m=>"+MapsToCook=(FilePath=\""+m+"\")"))+"\n");
        }
        else
        {
            UnrealRuntimeHost.VerifySource(unreal);UiRuntimePorting.VerifySource(unreal);BlueprintNativePorting.InstallInto(unreal,BlueprintNativePorting.ResolvePluginForEngine(Path.Combine(adapters,"unreal","EMBlueprintBridge"),engines.Unreal));
            ResourcePorting.WriteConfig(unreal,new{mode="export",projectPackage=transfer});progress?.Report("추적된 모든 맵의 실제 객체·공유 자산·프리팹 인스턴스 연결을 추출합니다.");
            await ResourcePorting.RunEditor(engines.Unreal,ResourcePorting.UnrealArgs(descriptor,Path.Combine(adapters,"unreal","project_bridge.py"),Path.Combine(run,"unreal-project-export.log")),run,token);
            var units=ValidatePackages(transfer);PrepareScripts(units);
            var projectScripts=new JsonArray(units.SelectMany(u=>(u.Data["scripts"] as JsonArray??new JsonArray()).Select(s=>s!)).GroupBy(s=>s["name"]!.GetValue<string>()).Select(g=>g.First().DeepClone()).ToArray());
            SceneScriptPorting.RestoreInto(units.FirstOrDefault(u=>u.Data["actorGraphs"] is not null).Folder??units[0].Folder,unreal,unity,projectScripts);
            await ResourcePorting.RunEditor(engines.Unity,ResourcePorting.UnityArgs(unity,transfer,"import-project",Path.Combine(run,"unity-project-import.log")),run,token);
        }
        var verification=Path.Combine(transfer,report.SourceEngine==Engine.Unity?"unreal-import-result.json":"unity-import-result.json");using var verified=JsonDocument.Parse(File.ReadAllText(verification));if(!verified.RootElement.GetProperty("savedAndReloaded").GetBoolean()||!verified.RootElement.GetProperty("referencesVerified").GetBoolean())throw new InvalidOperationException("프로젝트 참조/저장 검사 실패");
        var output=new ResourcePortResult(run,source,target,verification);File.WriteAllText(Path.Combine(run,"resource-port-result.json"),JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(run,"resource-port.html"),ResourcePorting.ResourceHtml(output,verified.RootElement));return output;
    }
}




