using System.Reflection;
using EngineMigrator.Core;

internal static class EngineExecutionChecks
{
    internal static void Run(string root, Action<bool, string> check)
    {
        var policy = typeof(ResourcePorting).Assembly.GetType("EngineMigrator.Core.EngineExecutionPolicy", true)!;
        var find = policy.GetMethod("FindRestriction", BindingFlags.Static | BindingFlags.NonPublic)!;
        var blocked = Path.Combine(root, "BlockedEngineWork");
        var nested = Path.Combine(blocked, "Output", "Target");
        var sibling = Path.Combine(root, "OtherProject");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(sibling);
        var marker = Path.Combine(blocked, ".engine-execution-blocked");
        File.WriteAllText(marker, "Security dialog incident");
        check((string?)find.Invoke(null, [nested]) == marker, "Engine stop marker applies to descendant output directories");
        check(find.Invoke(null, [sibling]) == null, "Engine stop lookup does not treat an unrelated sibling as blocked");

        var output = Path.Combine(blocked, "MustNotBeCreated");
        var rejected = false;
        try
        {
            // Block before actor translation, descriptor reads, copies or engine builds.
            ActorProjectPorting.RunAsync("not valid C#", sibling, output, "missing-editor").GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex) { rejected = ex.Message.Contains("보안 창 반복 발생"); }
        check(rejected && !Directory.Exists(output), "Incident stop rejects actor conversion before translation, copies or output writes");

        var run = typeof(ResourcePorting).GetMethod("RunEditor", BindingFlags.Static | BindingFlags.NonPublic)!;
        rejected = false;
        try
        {
            var task = (Task)run.Invoke(null, ["must-not-launch.exe", Array.Empty<string>(), nested, CancellationToken.None])!;
            task.GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex) { rejected = ex.Message.Contains("보안 창 반복 발생"); }
        check(rejected, "Incident stop rejects engine launch before trying to create a process");
    }
}
