using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EngineMigrator.Core;

public sealed record EngineExecutables(string Unity, string Unreal);
public sealed record ResourcePortResult(string RunDirectory, string SourceCopy, string TargetCopy, string VerificationFile)
{
    public bool ProjectPortComplete => false;
}

/// <summary>Runs native editor adapters on copies only. Does not label a resource pass as full script/project port completion.</summary>
public static class ResourcePorting
{
    public static async Task<ResourcePortResult> RunAsync(InspectionReport report, string outputRoot,
        EngineExecutables engines, string adapterRoot, IProgress<string>? progress = null, CancellationToken token = default)
    {
        adapterRoot=Path.GetFullPath(adapterRoot);
        if (report.Findings.Any(f => f.Severity == "오류")) throw new InvalidOperationException("프로젝트 검사 오류를 해결해 주세요.");
        var broken=report.Findings.Where(f=>f.Code is "DUPLICATE_GUID" or "MISSING_LOCAL_REFERENCE").ToArray();
        if(broken.Length>0)throw new InvalidOperationException("대상 생성 전 참조 검사 차단:\n"+string.Join("\n",broken.Select(f=>f.Message)));
        foreach (var executable in new[] { engines.Unity, engines.Unreal })
            if (!File.Exists(executable)) throw new FileNotFoundException("엔진 실행 파일을 찾을 수 없습니다.", executable);
        GuardPath(report.Source); GuardPath(report.Target); GuardPath(outputRoot);
        var output = Path.GetFullPath(outputRoot);
        foreach (var original in new[] { report.Source, report.Target })
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(original));
            if (output.Equals(root, StringComparison.OrdinalIgnoreCase) || output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("원본/대상 프로젝트 내부에는 작업 결과를 저장할 수 없습니다.");
        }
        EngineExecutionPolicy.AssertAllowed(outputRoot);
        ValidateUnityVersion(report.SourceEngine == Engine.Unity ? report.Source : report.Target, engines.Unity);
        ValidateUnrealVersion(report.SourceEngine == Engine.Unreal ? report.Source : report.Target, engines.Unreal);
        if(ProjectPorting.NeedsProjectMode(report))return await ProjectPorting.RunAsync(report,outputRoot,engines,adapterRoot,progress,token);
        var run = Path.Combine(output, "RP-" + Guid.NewGuid().ToString("N")[..12]);
        using var cacheCleanup = BuildArtifactCleanup.Begin(run);
        if(Directory.Exists(run)) throw new IOException("작업 경로 충돌입니다. 다시 시도해 주세요.");
        var sourceCopy = Path.Combine(run, "Source"); var targetCopy = Path.Combine(run, "Target"); var package = Path.Combine(run, "Transfer");
        Directory.CreateDirectory(package);
        progress?.Report("작업 폴더: " + run);
        progress?.Report("원본을 보존하고 독립 작업 복사본을 만드는 중…");
        await Task.Run(() => { CopyProject(report.Source, sourceCopy, token); CopyProject(report.Target, targetCopy, token); }, token);
        var unityCopy = report.SourceEngine == Engine.Unity ? sourceCopy : targetCopy;
        var unityOriginal = report.SourceEngine == Engine.Unity ? report.Source : report.Target;
        var packageCache=Path.Combine(unityOriginal,"Library","PackageCache");
        if(Directory.Exists(packageCache))
        {
            progress?.Report("Unity 패키지 캐시를 독립 복사본에 읽기 전용 원본에서 복제하는 중…");var cacheWatch=Stopwatch.StartNew();
            await Task.Run(()=>SeedPackageCache(packageCache,Path.Combine(unityCopy,"Library","PackageCache"),token),token);
            File.WriteAllText(Path.Combine(run,"cache-copy.json"),JsonSerializer.Serialize(new{elapsedSeconds=cacheWatch.Elapsed.TotalSeconds,parallelPackages=4,sourcePreserved=true}));
        }
        InstallUnityAdapter(unityCopy, adapterRoot);
        var unrealCopy = report.SourceEngine == Engine.Unreal ? sourceCopy : targetCopy;
        var unrealProject = ConfigureUnreal(unrealCopy);
        if (report.SourceEngine == Engine.Unity)
        {
            progress?.Report("Unity에서 실제 장면·에셋·참조를 추출하는 중…");
            await RunEditor(engines.Unity, UnityArgs(sourceCopy, package, "export", Path.Combine(run, "unity-export.log")), run, token);
            RequirePackage(package);
            using(var uiData=JsonDocument.Parse(File.ReadAllText(Path.Combine(package,"package.json"))))
                if(uiData.RootElement.GetProperty("nodes").EnumerateArray().Any(n=>n.TryGetProperty("hasUI",out var enabled)&&enabled.GetBoolean()))
                    await UiRuntimePorting.CompileAsync(unrealCopy,engines.Unreal,Path.Combine(run,"ui-runtime-build.log"),token);
            var sceneSource=SceneScriptPorting.ReadSource(package);var timingRuntime=TimeScalePorting.NeedsRuntime(JsonNode.Parse(File.ReadAllText(Path.Combine(package,"package.json"))));
            if(sceneSource is not null){
                string? graph=null;try{using var lifecycleData=JsonDocument.Parse(File.ReadAllText(Path.Combine(package,"package.json")));if(!timingRuntime&&!lifecycleData.RootElement.GetProperty("scripts").EnumerateArray().Any(s=>s.TryGetProperty("executionOrder",out var order)&&order.GetInt32()!=0)&&!lifecycleData.RootElement.GetProperty("nodes").EnumerateArray().Any(n=>n.TryGetProperty("behaviours",out var list)&&list.EnumerateArray().Any(b=>!b.GetProperty("enabled").GetBoolean()||!n.GetProperty("active").GetBoolean()||(n.TryGetProperty("effectiveActive",out var active)&&!active.GetBoolean()))))graph=ActorBlueprintGraphs.FromUnity(sceneSource);}catch(NotSupportedException){}
                if(graph is not null){var path=Path.Combine(package,"package.json");var data=JsonNode.Parse(File.ReadAllText(path))!;data["actorGraphs"]=JsonNode.Parse(graph);File.WriteAllText(path,data.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));progress?.Report("MonoBehaviour의 상태·분기·상속을 실제 Actor Blueprint 이벤트 그래프로 생성합니다.");}
                else {progress?.Report("장면 MonoBehaviour를 Runtime Actor 플러그인으로 컴파일하는 중…");await SceneScriptPorting.CompileIntoAsync(sceneSource,unrealCopy,engines.Unreal,Path.Combine(run,"scene-script-build.log"),token);}
            }
            if(sceneSource is null&&timingRuntime)await SceneScriptPorting.CompileIntoAsync(null,unrealCopy,engines.Unreal,Path.Combine(run,"scene-script-build.log"),token);
            PrepareUnrealSceneSettings(package,unrealCopy);

            // All Unity scenes receive a verified no-implicit-Pawn map policy.
            BlueprintNativePorting.InstallInto(unrealCopy,BlueprintNativePorting.ResolvePluginForEngine(Path.Combine(adapterRoot,"unreal","EMBlueprintBridge"),engines.Unreal));
            WriteConfig(unrealCopy, new { package, assetRoot = "/Game/EngineMigrator_" + Guid.NewGuid().ToString("N")[..8] });
            progress?.Report("Unreal 에셋 생성·장면 참조 연결·저장 후 재열기 검증 중…");
            await RunEditor(engines.Unreal, UnrealArgs(unrealProject, Path.Combine(adapterRoot, "unreal", "resource_bridge.py"), Path.Combine(run, "unreal-import.log")), run, token);
        }
        else
        {
            UnrealRuntimeHost.VerifySource(unrealCopy);
            UiRuntimePorting.VerifySource(unrealCopy);
            string? bridge=null;
            try{bridge=BlueprintNativePorting.ResolvePluginForEngine(Path.Combine(adapterRoot,"unreal","EMBlueprintBridge"),engines.Unreal);}catch(InvalidOperationException){}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
            if(bridge is not null){BlueprintNativePorting.InstallInto(unrealCopy,bridge);progress?.Report("선택한 엔진 빌드의 네이티브 머티리얼 검사 모듈을 작업 복사본에 연결했습니다.");}
            var defaultEngine = Path.Combine(sourceCopy, "Config", "DefaultEngine.ini");
            var map = ReadStartupMap(defaultEngine);
            WriteConfig(unrealCopy, new { reversePackage = package, map });
            progress?.Report("Unreal 시작 맵의 실제 에셋·머티리얼 그래프·계층을 추출하는 중…");
            await RunEditor(engines.Unreal, UnrealArgs(unrealProject, Path.Combine(adapterRoot, "unreal", "export_resources.py"), Path.Combine(run, "unreal-export.log")), run, token);
            RequirePackage(package);
            SceneScriptPorting.RestoreInto(package,unrealCopy,unityCopy);
            progress?.Report("Unity 에셋 생성·장면 참조 연결·저장 후 재열기 검증 중…");
            await RunEditor(engines.Unity, UnityArgs(targetCopy, package, "import", Path.Combine(run, "unity-import.log")), run, token);
        }
        var verification = Path.Combine(package, report.TargetEngine == Engine.Unreal ? "unreal-import-result.json" : "unity-import-result.json");
        using var result = JsonDocument.Parse(File.ReadAllText(verification));
        if (!result.RootElement.GetProperty("referencesVerified").GetBoolean() || !result.RootElement.GetProperty("savedAndReloaded").GetBoolean())
            throw new InvalidOperationException("대상 엔진 저장/참조 검증이 통과하지 못했습니다.");
        if (report.TargetEngine == Engine.Unreal)
        {
            var map = result.RootElement.GetProperty("savedMap").GetString();
            if (map is null || !map.StartsWith("/Game/") || map.Contains('\n') || map.Contains('\r')) throw new InvalidOperationException("잘못된 생성 맵 경로");
            var configPath = Path.Combine(targetCopy, "Config", "DefaultEngine.ini"); Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.AppendAllText(configPath, "\n[/Script/EngineSettings.GameMapsSettings]\nEditorStartupMap=" + map + "\nGameDefaultMap=" + map + "\n");
        }
        var nativeResult = new ResourcePortResult(run, sourceCopy, targetCopy, verification);
        File.WriteAllText(Path.Combine(run, "resource-port-result.json"), JsonSerializer.Serialize(nativeResult, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(run, "resource-port.html"), ResourceHtml(nativeResult, result.RootElement));
        File.WriteAllText(Path.Combine(run, "README.txt"), "실제 엔진 리소스 생성·참조 연결 검증 결과입니다. 전체 프로젝트 포팅 완료가 아닙니다.\n" +
            "지원 범위와 검증 항목은 Transfer의 실제 결과 JSON 및 resource-port.html을 확인하세요. 카메라/방향광/일부 후처리·환경, 제한된 MonoBehaviour 상속/상태/장면 부착을 포함합니다.\n일반 스크립트·애니메이션·물리·UI·렌더링 외형·패키징은 별도 구현 및 검증이 필요합니다.\n" +
            "원본/선택한 대상은 수정하지 않았습니다. Target 복사본을 대상 엔진에서 여세요.\n검증 결과: " + verification);
        return nativeResult;
    }

    public static string ResourceHtml(ResourcePortResult result, JsonElement verification)
    {
        static string E(string? value) => System.Net.WebUtility.HtmlEncode(value ?? "미확인");
        var html = new System.Text.StringBuilder("<!doctype html><html lang=\"ko\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>실제 엔진 리소스 포팅 결과</title><style>body{background:#211e1a;color:#f5efe5;font:15px/1.8 system-ui;max-width:1100px;margin:40px auto;padding:0 24px;overflow-wrap:anywhere}h1,h2,th{color:#f3ab73}table{width:100%;border-collapse:collapse}td,th{padding:12px;text-align:left;border-bottom:1px solid #514638}pre{white-space:pre-wrap;background:#302b25;padding:20px}.table{overflow:auto}</style><h1>실제 엔진 리소스 포팅 결과</h1><p>대상 에셋과 장면 저장·재열기 및 참조 검증이 통과했습니다. 전체 프로젝트 포팅은 미완료입니다. 렌더링 외형·게임플레이·일반 스크립트·패키징 검증과 구분합니다.</p>");
        html.Append("<p>원본 보존용 작업 복사본: " + E(result.SourceCopy) + "<br>실제 결과 프로젝트: " + E(result.TargetCopy) + "<br>검증 JSON: " + E(result.VerificationFile) + "</p>");
        if(verification.TryGetProperty("warnings",out var warnings)&&warnings.ValueKind==JsonValueKind.Array&&warnings.GetArrayLength()>0){html.Append("<h2>지원 범위와 주의 항목</h2><ul>");foreach(var warning in warnings.EnumerateArray())html.Append("<li>"+E(warning.GetString())+"</li>");html.Append("</ul>");}
        if(verification.TryGetProperty("maps",out var maps))
        {
            html.Append("<h2>프로젝트 장면·프리팹 범위</h2><p>프리팹 정의는 Unreal 맵으로 보관하며 배치 객체는 펼쳐서 변환합니다. Unity 복원 시 실제 프리팹 연결과 지원 속성 오버라이드를 복원합니다. Unreal 정의 수정의 자동 전파·중첩/Variant는 미지원입니다. 런타임 객체 생성·삭제와 단일 장면 전환은 지원 API 범위에서만 처리합니다.</p><div class=\"table\"><table><tr><th>유형</th><th>원본</th><th>활성 빌드</th><th>실제 대상</th></tr>");
            foreach(var map in maps.EnumerateArray())html.Append("<tr><td>"+E(map.GetProperty("kind").GetString())+"</td><td>"+E(map.GetProperty("path").GetString())+"</td><td>"+E(map.GetProperty("enabled").ToString())+"</td><td>"+E(map.TryGetProperty("actualPath",out var actual)&&!string.IsNullOrEmpty(actual.GetString())?actual.GetString():map.TryGetProperty("map",out var native)?native.GetString():null)+"</td></tr>");
            html.Append("</table></div>");
        }
        html.Append("<h2>원본 ID → 대상 리소스·객체 → 참조 ID</h2><div class=\"table\"><table><tr><th>원본 ID</th><th>대상</th><th>유형</th><th>연결된 참조 ID</th></tr>");
        if (verification.TryGetProperty("bindings", out var bindings))
            foreach (var binding in bindings.EnumerateArray())
                html.Append("<tr><td>" + E(binding.GetProperty("sourceId").GetString()) + "</td><td>" + E(binding.GetProperty("target").GetString()) + "</td><td>" + E(binding.GetProperty("kind").GetString()) + "</td><td>" + E(string.Join(", ", binding.GetProperty("references").EnumerateArray().Select(r => r.GetString()))) + "</td></tr>");
        return html.Append("</table></div><h2>검증 원본</h2><pre>" + E(verification.GetRawText()) + "</pre></html>").ToString();
    }

    public static void GuardPath(string path)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(path)); current is not null; current = current.Parent)
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("링크 경로는 지원하지 않습니다: " + current.FullName);
    }
    internal static void CopyProject(string source, string target, CancellationToken token)
    {
        GuardPath(source);GuardPath(target);
        var original=Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));var destination=Path.GetFullPath(target);
        if(destination.Equals(original,StringComparison.OrdinalIgnoreCase)||destination.StartsWith(original+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("원본 내부에는 프로젝트를 복제할 수 없습니다.");
        token.ThrowIfCancellationRequested(); Directory.CreateDirectory(target);
        var unityRoot=Directory.Exists(Path.Combine(source,"Assets"))&&Directory.Exists(Path.Combine(source,"ProjectSettings"));
        var unrealRoot=Directory.EnumerateFiles(source,"*.uproject").Any()||Directory.EnumerateFiles(source,"*.uplugin").Any();
        var dotnetRoot=Directory.EnumerateFiles(source,"*.csproj").Any();
        foreach (var file in Directory.EnumerateFiles(source))
        {
            token.ThrowIfCancellationRequested();
            if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("링크 파일은 복제하지 않습니다: " + file);
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var output = new FileStream(Path.Combine(target, Path.GetFileName(file)), FileMode.CreateNew);
            input.CopyTo(output);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name=Path.GetFileName(directory);
            if(new[]{".git",".vs"}.Contains(name,StringComparer.OrdinalIgnoreCase))continue;
            if(unityRoot&&new[]{"Library","Temp","Logs"}.Contains(name,StringComparer.OrdinalIgnoreCase))continue;
            if(unrealRoot&&new[]{"Intermediate","Saved","DerivedDataCache"}.Contains(name,StringComparer.OrdinalIgnoreCase))continue;
            if(dotnetRoot&&new[]{"obj","bin"}.Contains(name,StringComparer.OrdinalIgnoreCase))continue;
            GuardPath(directory); CopyProject(directory, Path.Combine(target, Path.GetFileName(directory)), token);
        }
    }
    internal static void SeedPackageCache(string source,string target,CancellationToken token)
    {
        GuardPath(source);GuardPath(target);var original=Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));var destination=Path.GetFullPath(target);
        if(destination.Equals(original,StringComparison.OrdinalIgnoreCase)||destination.StartsWith(original+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("원본 캐시 내부로 복제할 수 없습니다.");
        Directory.CreateDirectory(target);
        foreach(var file in Directory.EnumerateFiles(source))
        {
            token.ThrowIfCancellationRequested();if(File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))throw new InvalidOperationException("캐시 링크 파일 미지원: "+file);
            File.Copy(file,Path.Combine(target,Path.GetFileName(file)),false);
        }
        Parallel.ForEach(Directory.EnumerateDirectories(source),new ParallelOptions{MaxDegreeOfParallelism=4,CancellationToken=token},directory=>CopyProject(directory,Path.Combine(target,Path.GetFileName(directory)),token));
    }
    internal static void InstallUnityAdapter(string project, string adapters)
    {
        var folder=Path.Combine(project,"Assets","Editor");
        var manifest=Path.Combine(folder,".EngineMigratorAdapterHashes.json");
        var previous=File.Exists(manifest)?JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(manifest))!:new Dictionary<string,string>();
        var next=new Dictionary<string,string>();
        static string Hash(string path)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        var inputs=Directory.GetFiles(Path.Combine(adapters,"unity"),"*.cs");
        foreach(var input in inputs)
        {
            var name=Path.GetFileName(input);var output=Path.Combine(folder,name);next[name]=Hash(input);
            if(File.Exists(output)&&Hash(output)!=next[name]&&!(previous.TryGetValue(name,out var owned)&&Hash(output)==owned)&&!(previous.Count==0&&File.ReadAllText(output).StartsWith("// Editor adapter. Install into a working copy's Assets/Editor")))
                throw new InvalidOperationException("작업 복사본에 동명의 다른 어댑터가 있습니다: "+name);
        }
        Directory.CreateDirectory(folder);foreach(var input in inputs)File.Copy(input,Path.Combine(folder,Path.GetFileName(input)),true);
        File.WriteAllText(manifest,JsonSerializer.Serialize(next));
    }
    internal static string ConfigureUnreal(string project)
    {
        var path = Directory.GetFiles(project, "*.uproject").Single();
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var plugins = json["Plugins"] as JsonArray ?? new JsonArray();
        if (json["Plugins"] is null) json["Plugins"] = plugins;
        foreach (var name in new[] { "PythonScriptPlugin", "EditorScriptingUtilities", "SequencerScripting" })
        {
            var existing = plugins.FirstOrDefault(p => p?["Name"]?.GetValue<string>() == name);
            if (existing is not null) existing["Enabled"] = true;
            else plugins.Add(new JsonObject { ["Name"] = name, ["Enabled"] = true });
        }
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true })); return path;
    }
    internal static void ValidateUnityVersion(string project, string executable)
    {
        var version = File.ReadLines(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt")).FirstOrDefault(l => l.StartsWith("m_EditorVersion:"))?.Split(':', 2)[1].Trim();
        var installed = Directory.GetParent(Path.GetDirectoryName(executable)!)?.Name;
        if (version != installed) throw new InvalidOperationException($"Unity 프로젝트 버전({version})과 선택한 에디터 경로 버전({installed})이 다릅니다. 자동 업그레이드하지 않습니다.");
    }
    internal static void ValidateUnrealVersion(string project, string executable)
    {
        using var descriptor = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(project, "*.uproject").Single()));
        var association = descriptor.RootElement.TryGetProperty("EngineAssociation", out var value) ? value.GetString() : null;
        var buildPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(executable)!, "..", "..", "Build", "Build.version"));
        if (!File.Exists(buildPath)) throw new InvalidOperationException("선택한 Unreal 엔진의 Build.version을 확인할 수 없습니다.");
        using var build = JsonDocument.Parse(File.ReadAllText(buildPath));
        var family = build.RootElement.GetProperty("MajorVersion").GetInt32() + "." + build.RootElement.GetProperty("MinorVersion").GetInt32();
        var exact = family + "." + build.RootElement.GetProperty("PatchVersion").GetInt32();
        if (association != family && association != exact) throw new InvalidOperationException($"Unreal 프로젝트 연결 버전({association})과 선택한 엔진({exact})이 다르거나 사용자 정의 연결입니다. 엔진을 자동 변경하지 않습니다.");
    }
    public static string ReadStartupMap(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException("Unreal 시작 맵 설정이 필요합니다: Config/DefaultEngine.ini");
        var section = false; string? editor = null, game = null;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) section = line == "[/Script/EngineSettings.GameMapsSettings]";
            else if (section && (line.StartsWith("EditorStartupMap=") || line.StartsWith("GameDefaultMap=")))
            {
                var map = line.Split('=', 2)[1].Trim(' ', '"');
                if (map.StartsWith("/Game/", StringComparison.Ordinal) && !map.Contains(".."))
                { if (line.StartsWith("EditorStartupMap=")) editor = map; else game = map; }
            }
        }
        return editor ?? game ?? throw new InvalidOperationException("/Game/ 하위의 시작 맵을 프로젝트 설정에 지정해 주세요.");
    }
    public static void PrepareUnrealSceneSettings(string package,string project)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(package,"package.json")));
        var uiConfig=Path.Combine(project,"Config","DefaultEngine.ini");Directory.CreateDirectory(Path.GetDirectoryName(uiConfig)!);
        if(document.RootElement.GetProperty("nodes").EnumerateArray().Any(n=>n.TryGetProperty("hasUI",out var ui)&&ui.GetBoolean()))
            File.AppendAllText(uiConfig,"\n[/Script/Engine.UserInterfaceSettings]\nApplicationScale=1.0\nUIScaleRule=ShortestSide\nUIScaleCurve=(EditorCurveData=(Keys=((Time=0.0,Value=1.0),(Time=100000.0,Value=1.0))),ExternalCurve=None)\n");
        var clips=new List<double>();
        foreach(var node in document.RootElement.GetProperty("nodes").EnumerateArray())
        {
            if(!node.TryGetProperty("hasCamera",out var flag)||!flag.GetBoolean())continue;
            var camera=node.GetProperty("camera");if(camera.GetProperty("orthographic").GetBoolean())continue;
            var near=camera.GetProperty("near").GetDouble();if(!double.IsFinite(near)||near<=0)throw new InvalidOperationException("유효하지 않은 카메라 근거리 클립");clips.Add(near);
        }
        if(clips.Count==0)return;
        if(clips.Any(value=>Math.Abs(value-clips[0])>0.000001))throw new NotSupportedException("원근 카메라별 서로 다른 Near Clip은 아직 지원하지 않습니다.");
        var config=Path.Combine(project,"Config","DefaultEngine.ini");Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.AppendAllText(config,"\n[/Script/Engine.Engine]\nNearClipPlane="+(clips[0]*100).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\n");
    }
    internal static void WriteConfig(string project, object config) => File.WriteAllText(Path.Combine(project, "EngineMigratorRun.json"), JsonSerializer.Serialize(config));
    internal static void RequirePackage(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "package.json")));
        if (document.RootElement.GetProperty("schema").GetInt32() != 1 || document.RootElement.GetProperty("blockers").GetArrayLength() != 0)
            throw new InvalidOperationException("미지원 항목이 있는 리소스 패키지입니다.");
    }
    internal static string[] UnityArgs(string project, string package, string mode, string log) =>
        ["-batchmode", "-nographics", "-quit", "-projectPath", project, "-executeMethod", "EngineMigratorBridge.Run", "-emMode", mode, "-emPackage", package, "-logFile", log];
    internal static string[] UnrealArgs(string project, string script, string log) =>
        [project, "-run=pythonscript", "-script=" + script, "-nullrhi", "-unattended", "-nop4", "-nosplash", "-notraceserver", "-NoZenAutoLaunch", "-ddc=InstalledNoZenLocalFallback", "-UDPMESSAGING_TRANSPORT_ENABLE=0", "-abslog=" + log];
    internal static async Task RunEditor(string executable, IEnumerable<string> args, string workingDirectory, CancellationToken token)
    {
        EngineExecutionPolicy.AssertAllowed(workingDirectory);
        var plan = EngineLaunchSafety.Prepare(executable, args);
        using var lease = EngineExecutionPolicy.AcquirePrivateLease(workingDirectory, plan.AuditedEditor, plan.Kind == "Compiler");
        EngineLaunchSafety.VerifyProjectOwnership(plan, workingDirectory);
        EngineLaunchSafety.CheckDisk(workingDirectory);
        var before = await EngineLaunchSafety.AuditAsync(plan.AuditedEditor, token);
        var startedUtc = DateTimeOffset.UtcNow;
        var invocation = new { executable = plan.Executable, arguments = plan.Arguments, workingDirectory = Path.GetFullPath(workingDirectory), startedUtc };
        var receipt = Path.Combine(workingDirectory, "engine-preflight-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new { status = "PREFLIGHT_PASS", plan.Kind, before, invocation, engineStarted = false, policyChanged = false }), token);
        var start = new ProcessStartInfo(plan.Executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = workingDirectory };
        foreach (var argument in plan.Arguments) start.ArgumentList.Add(argument);
        var ipcAudits = new List<JsonElement>();
        using var process = Process.Start(start) ?? throw new InvalidOperationException("엔진 시작 실패");
        try
        {
            var exit = process.WaitForExitAsync(token);
            while (!exit.IsCompleted)
            {
                await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(2), token));
                token.ThrowIfCancellationRequested();
                if (exit.IsCompleted) break;
                EngineLaunchSafety.CheckDisk(workingDirectory);
                if (plan.Kind == "Unity")
                {
                    var evidence = await EngineLaunchSafety.QueryPowerShellAsync("EngineUnityIpcAudit.ps1", new() { ["EM_ENGINE_AUDIT_PARENT"] = process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) }, token);
                    using var snapshot = JsonDocument.Parse(evidence);
                    if (snapshot.RootElement.GetProperty("policyChanged").GetBoolean()) throw new InvalidOperationException("IPC 검사는 읽기 전용이어야 합니다.");
                    ipcAudits.Add(snapshot.RootElement.Clone());
                }
            }
            await exit;
            if (process.ExitCode != 0) throw new InvalidOperationException("엔진 실행 실패. 작업 폴더의 로그를 확인해 주세요. 종료 코드: " + process.ExitCode);
            var after = await EngineLaunchSafety.AuditAsync(plan.AuditedEditor, token);
            if (!before.Profiles.Order().SequenceEqual(after.Profiles.Order())) throw new InvalidOperationException("실행 도중 네트워크 프로필이 변경되어 검증을 완료 처리하지 않습니다.");
            await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new { status = "EXECUTED", plan.Kind, before, after, invocation, finishedUtc = DateTimeOffset.UtcNow, engineStarted = true, processId = process.Id, process.ExitCode, unityIpcAudits = ipcAudits, policyChanged = false }), token);
        }
        catch (Exception error)
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); }
            await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new { status = "FAIL", plan.Kind, before, invocation, finishedUtc = DateTimeOffset.UtcNow, error = error.Message, engineStarted = true, processId = process.Id, policyChanged = false }), CancellationToken.None);
            throw;
        }
    }
}

