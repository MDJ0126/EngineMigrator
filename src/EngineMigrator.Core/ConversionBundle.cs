using System.Text.Json;

namespace EngineMigrator.Core;

public sealed record ConvertedFile(string SourcePath, string? OutputPath, bool Generated, IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<CodeMapping> Mappings { get; init; } = [];
}
public sealed record ConversionBundleResult(string Directory, IReadOnlyList<ConvertedFile> Files)
{
    public bool ProjectPortComplete => false;
    public IReadOnlyList<PortingMapEntry> Mapping { get; init; } = [];
}

public static class ConversionBundle
{
    public static ConversionBundleResult Export(InspectionReport report, string destination, CancellationToken token = default)
    {
        if (report.Findings.Any(f => f.Severity == "오류")) throw new InvalidOperationException("프로젝트 검사 오류를 먼저 해결해 주세요.");
        destination = Path.GetFullPath(destination);
        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(report.Source));
        if (destination.Equals(source, StringComparison.OrdinalIgnoreCase) || destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("원본 프로젝트 안에는 변환 결과를 저장할 수 없습니다.");
        for (var parent = new DirectoryInfo(destination); parent is not null; parent = parent.Parent)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("링크 경로 대신 실제 출력 경로를 선택해 주세요.");
        var results = new List<ConvertedFile>();
        var outputs = new List<(string Path, string Code)>();
        foreach (var script in report.Scripts)
        {
            token.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(Path.Combine(source, script.Path));
            if (!path.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("원본 범위를 벗어난 코드 경로입니다.");
            if (new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("링크 파일은 변환할 수 없습니다.");
            for (var parent = new FileInfo(path).Directory; parent is not null && !parent.FullName.Equals(source, StringComparison.OrdinalIgnoreCase); parent = parent.Parent)
                if (parent.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("링크 폴더 내부 코드는 변환할 수 없습니다.");
            var bytes = File.ReadAllBytes(path);
            var hash = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            if (hash != script.Sha256) throw new InvalidOperationException("검사 후 원본 코드가 변경되었습니다. 다시 검사해 주세요: " + script.Path);
            if (report.SourceEngine == Engine.Unreal && !script.Path.EndsWith(".h", StringComparison.OrdinalIgnoreCase) && !script.Path.EndsWith(".hpp", StringComparison.OrdinalIgnoreCase))
            { results.Add(new(script.Path, null, false, ["C++ 구현 파일/빌드 스크립트는 아직 지원하지 않습니다. 헤더의 제한된 정적 함수만 지원합니다."])); continue; }
            using var input = new StreamReader(new MemoryStream(bytes), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var conversion = PortableScriptConverter.Convert(input.ReadToEnd(), report.SourceEngine);
            var relative = conversion.Generated ? $"{results.Count:D4}/{conversion.FileName}" : null;
            if (relative is not null) outputs.Add((relative, conversion.Code!));
            results.Add(new(script.Path, relative, conversion.Generated, conversion.Diagnostics) { Mappings = conversion.Mappings });
        }
        token.ThrowIfCancellationRequested();
        var outputDirectory = Path.Combine(destination, "EngineMigrator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        foreach (var output in outputs)
        {
            var path = Path.Combine(outputDirectory, output.Path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = new FileStream(path, FileMode.CreateNew); using var writer = new StreamWriter(stream); writer.Write(output.Code);
        }
        var bundle = new ConversionBundleResult(outputDirectory, results) { Mapping = PortingMap.Build(report, results) };
        File.WriteAllText(Path.Combine(outputDirectory, "conversion-manifest.json"), JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(outputDirectory, "porting-map.html"), PortingMap.ToHtml(report, bundle));
        File.WriteAllText(Path.Combine(outputDirectory, "README.txt"), "부분 코드 생성 결과입니다. 프로젝트 포팅 완료가 아닙니다.\n파일별 Generated는 코드 생성 여부이며 대상 엔진 컴파일/동작 성공을 뜻하지 않습니다.\n" + string.Join("\n", results.Select(f => $"{f.SourcePath}: {(f.Generated ? "생성" : "미지원")} — {string.Join("; ", f.Diagnostics)}")));
        return bundle;
    }
}
