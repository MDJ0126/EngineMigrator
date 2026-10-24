using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace EngineMigrator.Core;

public sealed record EngineFirewallRule(string Name, string Enabled, string Direction, string Action, string Profiles, string Protocol, string LocalPort, string RemotePort, string LocalAddress, string RemoteAddress, string Interface, string InterfaceType, string Service);
public sealed record EngineFirewallAudit(string Executable, string[] Profiles, EngineFirewallRule[] Rules, string Timestamp, bool PolicyChanged);
internal sealed record EngineLaunchPlan(string Executable, string AuditedEditor, string Kind, string[] Arguments);

public static class EngineLaunchSafety
{
    internal const string Unity = @"C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe";
    internal const string Unreal = @"C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe";
    internal const string Dotnet = @"C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\ThirdParty\DotNet\10.0\win-x64\dotnet.exe";
    internal const string Ubt = @"C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\DotNET\UnrealBuildTool\UnrealBuildTool.dll";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    internal static void VerifyCoverage(EngineFirewallAudit audit, string expectedExecutable)
    {
        if (!Path.GetFullPath(audit.Executable).Equals(Path.GetFullPath(expectedExecutable), StringComparison.OrdinalIgnoreCase) || audit.PolicyChanged)
            throw new InvalidOperationException("방화벽 검사 대상 또는 읽기 전용 검사 결과가 다릅니다.");
        if (audit.Profiles is not { Length: > 0 } || audit.Rules is null) throw new InvalidOperationException("활성 네트워크 프로필을 확인할 수 없습니다.");
        foreach (var profile in audit.Profiles)
        {
            if (profile is not ("Domain" or "Private" or "Public")) throw new InvalidOperationException("알 수 없는 네트워크 프로필입니다.");
            foreach (var protocol in new[] { "TCP", "UDP" })
                if (!audit.Rules.Any(rule => rule.Enabled == "True" && rule.Direction == "Inbound" && rule.Action is "Allow" or "Block" &&
                    (rule.Profiles == "Any" || rule.Profiles.Split(',', StringSplitOptions.TrimEntries).Contains(profile)) &&
                    (rule.Protocol == "Any" || rule.Protocol == protocol) && rule.LocalPort == "Any" && rule.RemotePort == "Any" &&
                    rule.LocalAddress == "Any" && rule.RemoteAddress == "Any" && rule.Interface == "Any" && rule.InterfaceType == "Any" && rule.Service == "Any"))
                    throw new InvalidOperationException($"엔진 실행 전 방화벽 규칙이 부족합니다: {profile}/{protocol}. 요구된 규칙을 확인할 수 없습니다.");
        }
    }

    internal static EngineLaunchPlan Prepare(string executable, IEnumerable<string> arguments)
    {
        executable = Path.GetFullPath(executable);
        var args = arguments.ToList();
        if (executable.Equals(Unity, StringComparison.OrdinalIgnoreCase))
        {
            if (!args.Contains("-batchmode", StringComparer.OrdinalIgnoreCase) || !args.Contains("-nographics", StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("감사된 Unity 실행에는 batchmode와 nographics가 필요합니다.");
            if (args.Contains("-noUpm", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("noUpm은 필요한 UI 패키지 참조를 제거하므로 사용하지 않습니다.");
            return new(executable, Unity, "Unity", args.ToArray());
        }
        if (executable.Equals(Unreal, StringComparison.OrdinalIgnoreCase))
        {
            Enforce("-notraceserver"); Enforce("-NoZenAutoLaunch"); Enforce("-ddc=InstalledNoZenLocalFallback"); Enforce("-UDPMESSAGING_TRANSPORT_ENABLE=0");
            if (args.Any(a => a.Equals("-ZenStore", StringComparison.OrdinalIgnoreCase) || a.StartsWith("-trace=", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("자동 서버 또는 trace 활성화 인수는 감사된 실행 범위가 아닙니다.");
            return new(executable, Unreal, "Unreal", args.ToArray());
        }
        if (executable.Equals(Dotnet, StringComparison.OrdinalIgnoreCase) && args.Count > 0 && Path.GetFullPath(args[0]).Equals(Ubt, StringComparison.OrdinalIgnoreCase))
        {
            if(args.Any(a=>a.StartsWith("-UBA",StringComparison.OrdinalIgnoreCase)&&!a.Equals("-UBADisableRemote",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("UBA 활성화 인수는 감사된 컴파일 범위가 아닙니다.");
            Enforce("-NoUBA"); Enforce("-UBADisableRemote"); Enforce("-NoXGE"); Enforce("-NoFASTBuild"); Enforce("-NoSNDBS"); Enforce("-MaxParallelActions=1");
            if (args.Any(a => a.StartsWith("-Target=", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("다중 타깃 문자열 내부 인수는 전역 NoUBA 설정을 보장하지 못하므로 미지원입니다.");
            return new(executable, Unreal, "Compiler", args.ToArray());
        }
        throw new InvalidOperationException("아직 감사되지 않은 엔진/컴파일러 경로입니다. 자동으로 다른 실행 파일을 시작하지 않습니다: " + executable);

        void Enforce(string required)
        {
            var key = required.Split('=')[0];
            var existing = args.Where(arg => arg.Equals(key, StringComparison.OrdinalIgnoreCase) || arg.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (existing.Any(arg => !arg.Equals(required, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("안전 실행 인수 충돌: " + key);
            args.RemoveAll(arg => arg.Equals(required, StringComparison.OrdinalIgnoreCase)); args.Add(required);
        }
    }

    internal static void VerifyProjectOwnership(EngineLaunchPlan plan, string workingDirectory)
    {
        string project;
        if (plan.Kind == "Unity")
        {
            var indices = plan.Arguments.Select((value,index)=>(value,index)).Where(item=>item.value.Equals("-projectPath",StringComparison.OrdinalIgnoreCase)).Select(item=>item.index).ToArray();
            if(indices.Length!=1 || indices[0]+1>=plan.Arguments.Length || plan.Arguments.Any(value=>value.Equals("-createProject",StringComparison.OrdinalIgnoreCase)||value.StartsWith("-createProject=",StringComparison.OrdinalIgnoreCase)||value.StartsWith("-projectPath=",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("단일 개인 복사본 projectPath가 필요합니다.");
            project=plan.Arguments[indices[0]+1];
        }
        else if(plan.Kind=="Compiler")
        {
            var projects=plan.Arguments.Where(value=>value.StartsWith("-Project=",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(projects.Length!=1)throw new InvalidOperationException("단일 개인 컴파일 프로젝트가 필요합니다.");
            project=projects[0]["-Project=".Length..];
        }
        else
        {
            var projects=plan.Arguments.Where(value=>!value.StartsWith('-')&&Path.GetExtension(value).Equals(".uproject",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(projects.Length!=1||plan.Arguments.Any(value=>value.StartsWith("-Project=",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("단일 개인 Unreal 프로젝트가 필요합니다.");
            project=projects[0];
        }
        var root=Path.GetFullPath(workingDirectory); var full=Path.GetFullPath(project);
        if(!full.Equals(root,StringComparison.OrdinalIgnoreCase)&&!full.StartsWith(Path.TrimEndingDirectorySeparator(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("원본/다른 프로젝트를 실행하지 않습니다. 현재 작업의 개인 복사본만 허용합니다.");
        if(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("프로젝트 설명 파일의 링크는 지원하지 않습니다.");
        ResourcePorting.GuardPath(full);
    }

    public static async Task<EngineFirewallAudit> AuditAsync(string executable, CancellationToken token = default)
    {
        executable = Path.GetFullPath(executable);
        if (!executable.Equals(Unity, StringComparison.OrdinalIgnoreCase) && !executable.Equals(Unreal, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("아직 감사되지 않은 엔진 경로입니다: " + executable);
        var json = await QueryPowerShellAsync("EngineFirewallAudit.ps1", new() { ["EM_ENGINE_AUDIT_PROGRAM"] = executable }, token);
        var result = JsonSerializer.Deserialize<EngineFirewallAudit>(json, JsonOptions) ?? throw new InvalidOperationException("방화벽 검사 결과가 비어 있습니다.");
        VerifyCoverage(result, executable); return result;
    }

    internal static async Task<string> QueryPowerShellAsync(string resource, Dictionary<string, string> environment, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("현재 감사된 엔진 실행 사전 검사는 Windows 전용입니다.");
        using var stream = typeof(EngineLaunchSafety).Assembly.GetManifestResourceStream("EngineMigrator.Core." + resource) ?? throw new InvalidOperationException("내장 실행 검사 파일이 없습니다.");
        using var reader = new StreamReader(stream); var script = await reader.ReadToEndAsync(token);
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo(shell) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) start.ArgumentList.Add(argument);
        foreach (var pair in environment) start.Environment[pair.Key] = pair.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("읽기 전용 사전 검사 시작 실패");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); throw; }
        var text = await output; var failure = await error;
        if (process.ExitCode != 0) throw new InvalidOperationException("엔진 실행 전 읽기 전용 검사 실패: " + failure);
        return text.TrimStart('\uFEFF');
    }

    internal static void CheckDisk(string workingDirectory)
    {
        foreach (var root in new[] { Path.GetPathRoot(Path.GetFullPath(workingDirectory))!, Path.GetPathRoot(Environment.SystemDirectory)! }.Distinct(StringComparer.OrdinalIgnoreCase))
            if (new DriveInfo(root).AvailableFreeSpace < 500_000_000) throw new IOException("디스크 안전 여유가 부족해 이 엔진 작업을 중단합니다: " + root);
    }
}
