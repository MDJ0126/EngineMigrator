using EngineMigrator.Core;
using System.Text.Json;
if(args.Length==3&&args[0]=="audit-engine")
{
    try{if(File.Exists(args[2]))throw new IOException("출력 파일이 이미 있습니다.");var audit=await EngineLaunchSafety.AuditAsync(args[1]);File.WriteAllText(args[2],JsonSerializer.Serialize(audit,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("READ_ONLY_ENGINE_PREFLIGHT_PASS");return 0;}
    catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
}
if(args.Length==3&&args[0]=="report-project"&&Enum.TryParse<Engine>(args[2],true,out var reportSource))
{
    try{var run=Path.GetFullPath(args[1]);ResourcePorting.GuardPath(run);var verification=Path.Combine(run,"Transfer",reportSource==Engine.Unity?"unreal-import-result.json":"unity-import-result.json");using var data=JsonDocument.Parse(File.ReadAllText(verification));if(!data.RootElement.GetProperty("savedAndReloaded").GetBoolean()||!data.RootElement.GetProperty("referencesVerified").GetBoolean())throw new InvalidOperationException("실제 저장·참조 검증이 통과한 결과만 보고서를 생성할 수 있습니다.");var result=new ResourcePortResult(run,Path.Combine(run,"Source"),Path.Combine(run,"Target"),verification);File.WriteAllText(Path.Combine(run,"resource-port-result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Path.Combine(run,"resource-port.html"),ResourcePorting.ResourceHtml(result,data.RootElement));Console.WriteLine("VERIFIED_PROJECT_REPORT_WRITTEN");return 0;}
    catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
}
if(args.Length==3&&args[0]=="prepare-scene-settings")
{
    try{ResourcePorting.PrepareUnrealSceneSettings(args[1],args[2]);Console.WriteLine("SCENE_SETTINGS_PREPARED");return 0;}
    catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
}
if(args.Length==2&&args[0]=="validate-project")
{
    try{Console.WriteLine("PROJECT_PREFLIGHT_PASS: "+ProjectPorting.ValidatePrepared(args[1]));return 0;}
    catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
}
if(args.Length==4&&args[0]=="install-bridge")
{
    try{BlueprintNativePorting.InstallForEngine(args[1],args[2],args[3]);Console.WriteLine("MATCHING_NATIVE_BRIDGE_INSTALLED");return 0;}
    catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
}

if(args.Length==2&&args[0]=="runtime-host")
{
    try{UnrealRuntimeHost.Ensure(args[1]);Console.WriteLine("RUNTIME_GAME_HOST_READY");return 0;}
    catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
}

if(args.Length==3&&args[0] is "actors-to-graph" or "actor-graph-to-unity")
{
    try{if(File.Exists(args[2]))throw new IOException("출력 파일이 이미 있습니다.");File.WriteAllText(args[2],args[0]=="actors-to-graph"?ActorBlueprintGraphs.FromUnity(File.ReadAllText(args[1])):ActorBlueprintGraphs.ToUnity(File.ReadAllText(args[1])));return 0;}
    catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
}

if(args.Length==5&&args[0]=="actor-project")
{
    try{Console.WriteLine(await ActorProjectPorting.RunAsync(File.ReadAllText(args[1]),args[2],args[3],args[4]));return 0;}
    catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
}

if(args.Length==3&&args[0] is "actors-to-cpp" or "actors-to-unity")
{
    try {
        var output=Path.GetFullPath(args[2]);if(Directory.Exists(output))throw new IOException("출력 폴더가 이미 있습니다.");
        IReadOnlyList<ActorFile> files;
        if(args[0]=="actors-to-cpp")files=ActorScripts.FromUnity(File.ReadAllText(args[1]));
        else files=ActorScripts.ToUnity(Directory.GetFiles(args[1],"*.h").Select(p=>new ActorFile(Path.GetFileName(p),File.ReadAllText(p))).ToArray());
        Directory.CreateDirectory(output);foreach(var file in files)File.WriteAllText(Path.Combine(output,file.Name),file.Code);Console.WriteLine(output);return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
}

if(args.Length==8 && args[0]=="blueprints")
{
    try { Console.WriteLine(await BlueprintNativePorting.RunAsync(args[1],args[2],args[3],args[4],args[5],args[6],args[7])); return 0; }
    catch(Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
}

if(args.Length==5 && args[0]=="verify-blueprint")
{
    try { var count=BlueprintVerification.Verify(File.ReadAllText(args[1]),File.ReadAllText(args[2]),File.ReadAllText(args[3]),File.ReadAllText(args[4])); Console.WriteLine($"PASS C# / native Blueprint / restored C#: {count} cases"); return 0; }
    catch(Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
}

if(args.Length==3 && args[0] is "cs-to-blueprint" or "blueprint-to-cs")
{
    try {
        var input=File.ReadAllText(args[1]);
        if(File.Exists(args[2])) throw new IOException("출력 파일이 이미 있습니다.");
        string output;
        if(args[0]=="cs-to-blueprint") output=BlueprintPorting.FromCSharp(input);
        else { var result=BlueprintPorting.ToCSharp(input); if(!result.Generated) throw new NotSupportedException(string.Join("; ",result.Diagnostics)); output=result.Code!; }
        File.WriteAllText(args[2],output); Console.WriteLine(args[2]); return 0;
    } catch(Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
}

if (args.Length != 8 || args[0] != "resources" || !Enum.TryParse<Engine>(args[1], true, out var sourceEngine))
{
    Console.Error.WriteLine("Usage: resources Unity|Unreal SOURCE TARGET OUTPUT UNITY_EXE UNREAL_EXE ADAPTERS_DIR");
    return 2;
}
try
{
    var report = ProjectInspector.Inspect(args[2], args[3], sourceEngine);
    var progress = new ConsoleProgress();
    var result = await ResourcePorting.RunAsync(report, args[4], new(args[5], args[6]), args[7], progress);
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }

sealed class ConsoleProgress : IProgress<string>
{
    public void Report(string message) => Console.Error.WriteLine(message);
}
