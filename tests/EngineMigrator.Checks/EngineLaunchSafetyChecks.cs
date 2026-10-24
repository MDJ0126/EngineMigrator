using EngineMigrator.Core;

internal static class EngineLaunchSafetyChecks
{
    internal static void Run(string root, Action<bool,string> check)
    {
        var tcp=new EngineFirewallRule("existing-tcp","True","Inbound","Allow","Public","TCP","Any","Any","Any","Any","Any","Any","Any");
        var udp=tcp with{Name="existing-udp",Protocol="UDP"};
        var audit=new EngineFirewallAudit(EngineLaunchSafety.Unreal,["Public"],[tcp,udp],DateTimeOffset.UtcNow.ToString("o"),false);
        static bool Rejected(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
        EngineLaunchSafety.VerifyCoverage(audit,EngineLaunchSafety.Unreal);
        check(true,"Existing TCP/UDP firewall rules cover the actual active Public profile without policy mutation");
        EngineLaunchSafety.VerifyCoverage(audit with{Rules=[tcp with{Action="Block",Protocol="Any"}]},EngineLaunchSafety.Unreal);
        check(true,"Existing exact-program inbound block also satisfies offline editor coverage");
        foreach(var (name,value) in new (string,EngineFirewallAudit)[]{
            ("missing UDP",audit with{Rules=[tcp]}),("uncovered Private",audit with{Profiles=["Public","Private"]}),
            ("disabled rule",audit with{Rules=[tcp with{Enabled="False"},udp]}),("restricted port",audit with{Rules=[tcp with{LocalPort="7777"},udp]}),
            ("restricted address",audit with{Rules=[tcp with{RemoteAddress="LocalSubnet"},udp]}),("restricted service",audit with{Rules=[tcp with{Service="OtherService"},udp]}),
            ("missing profile",audit with{Profiles=[]}),("unknown profile",audit with{Profiles=["Unknown"]}),
            ("wrong program",audit with{Executable=EngineLaunchSafety.Unity}),("policy mutation",audit with{PolicyChanged=true})})
            check(Rejected(()=>EngineLaunchSafety.VerifyCoverage(value,EngineLaunchSafety.Unreal)),"Preflight rejects "+name+" before starting an engine");
        var safe=EngineLaunchSafety.Prepare(EngineLaunchSafety.Unreal,["private.uproject","-run=pythonscript"]);
        check(new[]{"-notraceserver","-NoZenAutoLaunch","-ddc=InstalledNoZenLocalFallback","-UDPMESSAGING_TRANSPORT_ENABLE=0"}.All(safe.Arguments.Contains),"Every common Unreal editor launch receives the previously executed offline server options");
        var again=EngineLaunchSafety.Prepare(EngineLaunchSafety.Unreal,safe.Arguments);
        check(again.Arguments.SequenceEqual(safe.Arguments),"Safe Unreal arguments remain stable when already supplied by a call site");
        foreach(var flag in new[]{"-ddc=Default","-NoZenAutoLaunch=127.0.0.1","-notraceserver=0","-UDPMESSAGING_TRANSPORT_ENABLE=1","-trace=net","-ZenStore"})
            check(Rejected(()=>EngineLaunchSafety.Prepare(EngineLaunchSafety.Unreal,[flag])),"Conflicting engine argument is rejected: "+flag);
        var compile=EngineLaunchSafety.Prepare(EngineLaunchSafety.Dotnet,[EngineLaunchSafety.Ubt,"RuntimeWorldEditor","Win64","Development"]);
        check(compile.Kind=="Compiler"&&compile.AuditedEditor==EngineLaunchSafety.Unreal&&compile.Arguments.Contains("-NoUBA")&&compile.Arguments.Contains("-UBADisableRemote")&&compile.Arguments.Contains("-NoXGE")&&compile.Arguments.Contains("-NoFASTBuild")&&compile.Arguments.Contains("-NoSNDBS")&&compile.Arguments.Contains("-MaxParallelActions=1"),"Direct Unreal compilation receives global NoUBA and one-action settings and audits its editor");
        foreach(var flag in new[]{"-UBA","-UBAForceRemote","-UBALinkRemote","-UBADisableRemote=0","-MaxParallelActions=8","-Target=RuntimeWorldEditor Win64 Development"})
            check(Rejected(()=>EngineLaunchSafety.Prepare(EngineLaunchSafety.Dotnet,[EngineLaunchSafety.Ubt,flag])),"Unsafe or nested compiler setting is rejected: "+flag);
        check(Rejected(()=>EngineLaunchSafety.Prepare(EngineLaunchSafety.Dotnet,["unrelated.dll"])),"Bundled dotnet cannot launch arbitrary assemblies through the engine runner");
        check(Rejected(()=>EngineLaunchSafety.Prepare(Path.Combine(Path.GetTempPath(),"Unity.exe"),["-batchmode","-nographics"])),"An executable with the same name outside the audited installation is rejected");
        check(Rejected(()=>EngineLaunchSafety.Prepare(EngineLaunchSafety.Unity,["-batchmode"])),"Unity execution without nographics is rejected");
        check(Rejected(()=>EngineLaunchSafety.Prepare(EngineLaunchSafety.Unity,["-batchmode","-nographics","-noUpm"])),"Unity noUpm is rejected after actual missing-UGUI-reference reproduction");
        check(EngineLaunchSafety.Prepare(EngineLaunchSafety.Unity,["-batchmode","-nographics"]).Kind=="Unity","Audited Unity keeps package resolution available for named-IPC monitoring");
        var work=Path.Combine(root,"OwnedEngineWork");Directory.CreateDirectory(work);
        EngineLaunchSafety.VerifyProjectOwnership(EngineLaunchSafety.Prepare(EngineLaunchSafety.Unity,["-batchmode","-nographics","-projectPath",work]),work);
        check(true,"Engine runner accepts its own project copy");
        check(Rejected(()=>EngineLaunchSafety.VerifyProjectOwnership(EngineLaunchSafety.Prepare(EngineLaunchSafety.Unity,["-batchmode","-nographics","-projectPath",Path.Combine(root,"OtherProject")]),work)),"Working-directory scope cannot be used to open another Unity project");
        check(Rejected(()=>EngineLaunchSafety.VerifyProjectOwnership(EngineLaunchSafety.Prepare(EngineLaunchSafety.Unreal,[Path.Combine(root,"OtherProject","Other.uproject")]),work)),"Unreal descriptor must remain inside the owned working directory");
        check(Rejected(()=>EngineLaunchSafety.VerifyProjectOwnership(EngineLaunchSafety.Prepare(EngineLaunchSafety.Dotnet,[EngineLaunchSafety.Ubt,"-Project="+Path.Combine(root,"OtherProject","Other.uproject")]),work)),"Compiler cannot target an unrelated source project");
        var scopeHome=Path.Combine(root,"AuditedScope");var scopeRoot=Path.Combine(scopeHome,"artifacts");var child=Path.Combine(scopeRoot,"Work");Directory.CreateDirectory(child);
        var stop=Path.Combine(scopeHome,EngineExecutionPolicy.BlockFile);File.WriteAllText(stop,"Still blocked outside the audited private scope");
        var scopeFile=Path.Combine(scopeHome,EngineExecutionPolicy.ScopeFile);
        void Scope(string declaredRoot,string[] editors,bool builds)=>File.WriteAllText(scopeFile,System.Text.Json.JsonSerializer.Serialize(new{schema=1,artifactsRoot=declaredRoot,editors,allowBuilds=builds}));
        check(!EngineExecutionPolicy.IsAuditedPrivateScope(stop,[child],false),"Incident marker remains closed without a private-scope declaration");
        Scope(scopeRoot,[EngineLaunchSafety.Unity,EngineLaunchSafety.Unreal],false);
        check(EngineExecutionPolicy.IsAuditedPrivateScope(stop,[child],false,EngineLaunchSafety.Unity),"Audited private descendants can be selectively resumed without removing the incident marker");
        check(!EngineExecutionPolicy.IsAuditedPrivateScope(stop,[child],true,EngineLaunchSafety.Unreal),"Compilation remains denied when private scope does not allow builds");
        check(!EngineExecutionPolicy.IsAuditedPrivateScope(stop,[Path.Combine(scopeHome,"artifacts-other")],false),"Private scope uses a directory boundary instead of a loose path prefix");
        check(!EngineExecutionPolicy.IsAuditedPrivateScope(stop,[Path.Combine(root,"OtherProject")],false),"An audited scope cannot authorize other projects");
        Scope(Path.Combine(root,"OtherProject"),[EngineLaunchSafety.Unity],false);
        check(!EngineExecutionPolicy.IsAuditedPrivateScope(stop,[child],false),"Scope declaration cannot relocate artifacts into another project");
        Scope(scopeRoot,[Path.Combine(root,"Unity.exe")],true);
        check(!EngineExecutionPolicy.IsAuditedPrivateScope(stop,[child],true),"Scope declaration cannot whitelist a same-name unverified editor");
        Scope(scopeRoot,[EngineLaunchSafety.Unity],false);
        check(!EngineExecutionPolicy.IsAuditedPrivateScope(stop,[child],false,EngineLaunchSafety.Unreal),"Scope must explicitly contain the exact editor being launched");
        File.WriteAllText(scopeFile,"not json");
        check(!EngineExecutionPolicy.IsAuditedPrivateScope(stop,[child],false),"Malformed scope fails closed without changing the marker");
    }
}
