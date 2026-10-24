using System.Text.Json;
namespace EngineMigrator.Core;

internal static class EngineExecutionPolicy
{
    internal const string BlockFile = ".engine-execution-blocked";

    internal static string? FindRestriction(string origin)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(origin)); directory != null; directory = directory.Parent)
        {
            var marker = Path.Combine(directory.FullName, BlockFile);
            if (File.Exists(marker)) return marker;
        }
        return null;
    }

    internal static void AssertAllowed(params string[] origins)
    {
        foreach (var origin in origins.Concat(new[] { Environment.CurrentDirectory, AppContext.BaseDirectory }))
            if (FindRestriction(origin) is { } marker && !IsAuditedPrivateScope(marker, origins, false))
                throw new InvalidOperationException("보안 창 반복 발생으로 엔진 실행이 차단되었습니다. 원인을 해결하기 전에는 자동 재시도하지 마세요. 차단 파일: " + marker);
    }
    internal const string ScopeFile = ".engine-audited-private.json";
    private sealed record PrivateScope(int Schema, string ArtifactsRoot, string[] Editors, bool AllowBuilds);

    internal static bool IsAuditedPrivateScope(string marker, string[] origins, bool compiler, string? editor = null)
    {
        try
        {
            if (origins.Length == 0) return false;
            var directory = Path.GetDirectoryName(Path.GetFullPath(marker))!;
            var file = Path.Combine(directory, ScopeFile);
            if (!File.Exists(file) || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) return false;
            var scope = JsonSerializer.Deserialize<PrivateScope>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var root = Path.GetFullPath(Path.Combine(directory, "artifacts"));
            if (scope is null || scope.Schema != 1 || scope.Editors is not { Length: > 0 } || !Path.GetFullPath(scope.ArtifactsRoot).Equals(root, StringComparison.OrdinalIgnoreCase)) return false;
            if (scope.Editors.Any(path => path != EngineLaunchSafety.Unity && path != EngineLaunchSafety.Unreal)) return false;
            if (editor is not null && !scope.Editors.Contains(editor)) return false;
            if (compiler && (!scope.AllowBuilds || !scope.Editors.Contains(EngineLaunchSafety.Unreal))) return false;
            foreach (var origin in origins)
            {
                var full = Path.GetFullPath(origin);
                if (!full.Equals(root, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
                for (var entry = new DirectoryInfo(full); entry is not null; entry = entry.Parent)
                    if (entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            }
            return true;
        }
        catch (Exception error) when (error is JsonException or IOException or ArgumentException or UnauthorizedAccessException) { return false; }
    }

    internal static FileStream? AcquirePrivateLease(string workingDirectory, string editor, bool compiler)
    {
        var marker = FindRestriction(workingDirectory) ?? FindRestriction(Environment.CurrentDirectory) ?? FindRestriction(AppContext.BaseDirectory);
        if (marker is null) return null;
        if (!IsAuditedPrivateScope(marker, [workingDirectory], compiler, editor)) throw new InvalidOperationException("감사된 개인 테스트 실행 범위가 아닙니다.");
        return File.Open(Path.Combine(Path.GetDirectoryName(marker)!, "artifacts", "offline-editor-runtime.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

}
