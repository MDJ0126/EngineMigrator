using System.Text.Json;

namespace EngineMigrator.Core;

public enum Engine { Unity, Unreal }
public sealed record Finding(string Severity, string Message)
{
    public string? Code { get; init; }
    public string? SourcePath { get; init; }
    public int? Line { get; init; }
    public string? Suggestion { get; init; }
}
public sealed record AssetEntry(string Path, string Kind, long Bytes);
public sealed record InspectionReport(string Source, string Target, Engine SourceEngine,
    Engine TargetEngine, string? SourceVersion, IReadOnlyList<AssetEntry> Assets,
    IReadOnlyList<Finding> Findings, DateTimeOffset InspectedAt)
{
    public IReadOnlyList<ScriptUnit> Scripts { get; init; } = [];
    public bool CanConvert => false;
    public string ConversionStatus => "전체 프로젝트 포팅 미완료 — 지원 장면·스크립트·리소스·프리팹 왕복은 실제 엔진 포팅에서 별도로 검사합니다.";
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    });
}

public static class ProjectInspector
{
    public static InspectionReport Inspect(string source, string target, Engine sourceEngine,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>();
        var assets = new List<AssetEntry>();
        var targetEngine = sourceEngine == Engine.Unity ? Engine.Unreal : Engine.Unity;
        source = Normalize(source);
        target = Normalize(target);
        if (Overlaps(source, target))
            findings.Add(new("오류", "원본과 대상은 같거나 서로 포함하는 폴더일 수 없습니다."));
        string? version = null;
        if (!Directory.Exists(source)) findings.Add(new("오류", "원본 폴더가 없습니다."));
        else
        {
            ValidateProject(source, sourceEngine, "원본", findings);
            try
            {
                if (sourceEngine == Engine.Unity)
                {
                    var versionFile = Path.Combine(source, "ProjectSettings", "ProjectVersion.txt");
                    if (File.Exists(versionFile)) version = File.ReadLines(versionFile)
                        .FirstOrDefault(x => x.StartsWith("m_EditorVersion:"))?.Split(':', 2)[1].Trim();
                }
                else
                {
                    var descriptors = Directory.GetFiles(source, "*.uproject");
                    foreach (var descriptor in descriptors)
                    {
                        var info = new FileInfo(descriptor);
                        if (!info.Attributes.HasFlag(FileAttributes.ReparsePoint)) assets.Add(new(info.Name, "Unreal 프로젝트 설정", info.Length));
                        else findings.Add(new("경고", "링크 프로젝트 설정 파일 제외: " + info.Name));
                    }
                    if (descriptors.Length == 1)
                    {
                        try
                        {
                            using var document = JsonDocument.Parse(File.ReadAllText(descriptors[0]));
                            if (document.RootElement.TryGetProperty("EngineAssociation", out var value))
                                version = value.GetString();
                        }
                        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
                        { findings.Add(new("오류", $"Unreal 프로젝트 버전 읽기 실패: {ex.Message}")); }
                    }
                }
                var roots = sourceEngine == Engine.Unity
                    ? new[] { "Assets", "Packages", "ProjectSettings" }
                    : new[] { "Content", "Source", "Plugins", "Config" };
                foreach (var relative in roots)
                {
                    var folder = Path.Combine(source, relative);
                    if (Directory.Exists(folder)) Scan(folder, source, assets, findings, cancellationToken);
                }
                if (sourceEngine == Engine.Unity)
                    findings.Add(new("미확인", "외부 패키지 캐시·로컬 경로 패키지·DLL 내부 코드는 조사하지 않았습니다. 전체 의존성 해석이 필요합니다."));
                else
                    findings.Add(new("미확인", "엔진 설치 플러그인·외부 모듈 및 .uasset 내부 Blueprint는 조사하지 않았습니다."));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            { findings.Add(new("오류", $"원본 읽기 실패: {ex.Message}")); }
        }
        if (!Directory.Exists(target)) findings.Add(new("오류", "대상은 미리 생성한 엔진 프로젝트 폴더여야 합니다."));
        else ValidateProject(target, targetEngine, "대상", findings);
        if(sourceEngine==Engine.Unity)UnityProjectAudit.Add(source,assets,findings,cancellationToken);
        findings.Add(new("안내", "파일·GUID·장면·프리팹 구조와 스크립트를 조사했습니다. 패키지 GUID 해석·실제 컴포넌트 검사는 작업 복사본의 엔진 사전 검사에서 확인합니다."));
        findings.Add(new("차단", "전체 게임 자동 포팅은 미완료입니다. 지원 범위 밖의 문법·객체·엔진 API는 실제 대상 생성 전에 차단합니다."));
        var scripts = new List<ScriptUnit>();
        foreach (var asset in assets.Where(a => ScriptPorting.IsCode(a.Path)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { scripts.Add(ScriptPorting.Analyze(source, asset.Path, cancellationToken)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                scripts.Add(new(asset.Path, "미확인", "", "차단: 파일 읽기 실패", [], [], [], [ex.Message]));
                findings.Add(new("오류", $"스크립트 분석 실패: {asset.Path}: {ex.Message}"));
            }
        }
        return new(source, target, sourceEngine, targetEngine, version, assets, findings, DateTimeOffset.Now) { Scripts = scripts };
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("프로젝트 폴더를 선택해 주세요.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
    private static bool Overlaps(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase)
        || a.StartsWith(Path.EndsInDirectorySeparator(b) ? b : b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || b.StartsWith(Path.EndsInDirectorySeparator(a) ? a : a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void ValidateProject(string root, Engine engine, string role, List<Finding> findings)
    {
        try
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                findings.Add(new("오류", $"{role} 프로젝트 루트가 링크입니다. 실제 경로를 선택해 주세요."));
            if (engine == Engine.Unity)
            {
                if (!Directory.Exists(Path.Combine(root, "Assets")) || !File.Exists(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt")))
                    findings.Add(new("오류", $"{role} Unity 프로젝트에 Assets 또는 ProjectSettings/ProjectVersion.txt가 없습니다."));
            }
            else if (Directory.GetFiles(root, "*.uproject").Length != 1)
                findings.Add(new("오류", $"{role} Unreal 프로젝트 루트에는 .uproject 파일이 정확히 하나 있어야 합니다."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { findings.Add(new("오류", $"{role} 프로젝트 검사 실패: {ex.Message}")); }
    }

    private static void Scan(string folder, string root, List<AssetEntry> assets, List<Finding> findings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
            { findings.Add(new("경고", $"링크 폴더 제외: {Path.GetRelativePath(root, folder)}")); return; }
            if (new[] { "Binaries", "Intermediate", "Library", "Temp", "obj", "bin", ".git" }.Contains(Path.GetFileName(folder), StringComparer.OrdinalIgnoreCase))
            {
                findings.Add(new("안내", $"캐시/빌드 폴더 이름에 따라 조사 제외: {Path.GetRelativePath(root, folder)}. 사용자 코드가 있다면 별도 확인이 필요합니다."));
                return;
            }
            foreach (var path in Directory.EnumerateFiles(folder))
            {
                token.ThrowIfCancellationRequested();
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                { findings.Add(new("경고", $"링크 파일 제외: {Path.GetRelativePath(root, path)}")); continue; }
                var ext = Path.GetExtension(path).ToLowerInvariant();
                var kind = ext switch
                {
                    ".meta" => "Unity 메타데이터 (GUID/참조 해석 대기)",
                    ".png" or ".jpg" or ".jpeg" or ".tga" or ".exr" or ".psd" => "텍스처 후보",
                    ".fbx" or ".obj" or ".blend" => "메시 후보",
                    ".mat" => "Unity 머티리얼",
                    ".unity" => "Unity 장면",
                    ".prefab" => "Unity 프리팹",
                    ".cs" => "C# 코드",
                    ".cpp" or ".cc" or ".cxx" or ".c" => "C/C++ 코드",
                    ".h" or ".hpp" or ".inl" => "C/C++ 헤더",
                    ".dll" => "바이너리 의존성 (내부 코드 미확인)",
                    ".asmdef" or ".asmref" => "Unity 어셈블리 설정",
                    ".uplugin" => "Unreal 플러그인 설정",
                    ".uasset" => "Unreal 에셋 (내부 유형 미확인)",
                    ".umap" => "Unreal 맵",
                    _ => "기타 (미분류)"
                };
                assets.Add(new(Path.GetRelativePath(root, path), kind, new FileInfo(path).Length));
            }
            foreach (var directory in Directory.EnumerateDirectories(folder)) Scan(directory, root, assets, findings, token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { findings.Add(new("오류", $"파일 목록 일부 검사 실패 ({Path.GetRelativePath(root, folder)}): {ex.Message}")); }
    }
}
