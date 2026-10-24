"""Prepare the actual world-travel fixture without launching an engine."""
from pathlib import Path

home=Path('artifacts/dynamic-scene-check');home.mkdir(exist_ok=True)
unity=Path('artifacts/runtime-ui-api-tests/UnitySource')
spawner=unity/'Assets/RuntimeWorld/RuntimeSpawner.cs'
before=home/'prior-runtime-spawner.cs.txt'
if not before.exists():before.write_bytes(spawner.read_bytes())
original=before.read_text(encoding='utf-8-sig')
if 'SceneManager.LoadScene(1)' not in original:raise RuntimeError('Expected original world fixture missing')
dynamic=original.replace('public int Frame;','public int Frame;public int NextCalls;public int SceneOffset;').replace(' void Update()', ' public int NextIndex(){NextCalls++;return SceneOffset+NextCalls;}\n void Update()').replace('SceneManager.LoadScene(1)','SceneManager.LoadScene(NextIndex())')
(home/'runtime-spawner-original.cs.txt').write_text(dynamic)
spawner.write_text(dynamic)
observer=Path('tools/runtime-probe/RuntimeWorldUnityProbe.cs').read_text()
observer=observer.replace('public static class EMRuntimeWorldProbe','public static class EMDynamicWorldProbe').replace('EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s=>s.enabled).path)', 'EditorSceneManager.OpenScene("Assets/RuntimeWorld/RuntimeStart.unity")')
# Existing project already contains the observer types; only replace its source.
(unity/'Assets/Editor/RuntimeWorldUnityProbe.cs').write_text(observer)
runner=home/'runner';runner.mkdir(exist_ok=True)
(runner/'Runner.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>EngineMigrator.Checks</AssemblyName></PropertyGroup><ItemGroup><ProjectReference Include="../../../src/EngineMigrator.Core/EngineMigrator.Core.csproj" /></ItemGroup></Project>')
(runner/'Program.cs').write_text('''using EngineMigrator.Core;
using System.Text.Json.Nodes;
var home=Path.GetFullPath("artifacts/dynamic-scene-check");
var target=Path.GetFullPath("artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Target");
var work=Directory.GetParent(target)!.FullName;var descriptor=Path.Combine(target,"RuntimeWorld.uproject");
var unity=Path.GetFullPath("artifacts/runtime-ui-api-tests/UnitySource");
if(args.Length!=1)throw new ArgumentException("Explicit mode required");
if(args[0]=="prepare"){
 var header=Path.Combine(target,"Plugins/EMActorPort/Source/EMActorPort/Public/RuntimeSpawner.h");
 var backup=Path.Combine(home,"prior-runtime-spawner.h.txt");if(!File.Exists(backup))File.Copy(header,backup);
 var files=ActorScripts.FromUnity(File.ReadAllText(Path.Combine(home,"runtime-spawner-original.cs.txt")));if(files.Count!=1)throw new Exception("Unexpected classes");
 File.WriteAllText(header,files[0].Code);File.WriteAllText(Path.Combine(home,"RuntimeSpawner.h"),files[0].Code);
 var restored=ActorScripts.ToUnity(files).Single().Code;File.WriteAllText(Path.Combine(home,"runtime-spawner-restored.cs.txt"),restored);
 Console.WriteLine("DYNAMIC_SCENE_SOURCE_GENERATED_NO_ENGINE_LAUNCH");return;
}
if(args[0]=="build"){
 await ResourcePorting.RunEditor(EngineLaunchSafety.Dotnet,[EngineLaunchSafety.Ubt,"RuntimeWorldEditor","Win64","Development","-Project="+descriptor,"-NoHotReload","-NoPCH","-NoSharedPCH","-log="+Path.Combine(home,"native-build.log")],work,CancellationToken.None);
 Console.WriteLine("DYNAMIC_SCENE_BUILD_EXECUTED");return;
}
if(args[0]=="native"){
 var capture=Path.Combine(home,"native-execution.json");if(File.Exists(capture))throw new IOException("Fresh capture required");
 var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(target,".EngineMigratorProject.json")))!;
 var map=manifest["units"]!.AsArray().Single(n=>n!["path"]!.GetValue<string>()=="Assets/RuntimeWorld/RuntimeStart.unity")!["map"]!.GetValue<string>();
 using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(180));
 await ResourcePorting.RunEditor(EngineLaunchSafety.Unreal,[descriptor,map,"-game","-nullrhi","-RenderOffscreen","-nosound","-nosplash","-unattended","-nop4","-UseFixedTimeStep","-FPS=50","-EMRuntimeProbe="+capture,"-abslog="+Path.Combine(home,"native-execution.log")],work,timeout.Token);
 if(!File.Exists(capture))throw new IOException("Native capture missing");Console.WriteLine("DYNAMIC_SCENE_NATIVE_EXECUTED");return;
}
if(args[0] is "unity-original" or "unity-restored"){
 var capture=Path.Combine(home,args[0]+".json");if(File.Exists(capture))throw new IOException("Fresh capture required");
 var settings=Path.Combine(unity,"ProjectSettings/EditorSettings.asset");var settingsBefore=File.ReadAllBytes(settings);
 var source=Path.Combine(unity,"Assets/RuntimeWorld/RuntimeSpawner.cs");var sourceBefore=File.ReadAllBytes(source);
 File.WriteAllText(source,File.ReadAllText(Path.Combine(home,args[0]=="unity-original"?"runtime-spawner-original.cs.txt":"runtime-spawner-restored.cs.txt")));
 var prior=Environment.GetEnvironmentVariable("EM_WORLD_OUTPUT");Environment.SetEnvironmentVariable("EM_WORLD_OUTPUT",capture);
 try{
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(240));
  await ResourcePorting.RunEditor(EngineLaunchSafety.Unity,["-batchmode","-nographics","-projectPath",unity,"-executeMethod","EMDynamicWorldProbe.Run","-logFile",Path.Combine(home,args[0]+".log")],Directory.GetParent(unity)!.FullName,timeout.Token);
  if(!File.Exists(capture))throw new IOException("Unity capture missing");
 }finally{File.WriteAllBytes(settings,settingsBefore);File.WriteAllBytes(source,sourceBefore);Environment.SetEnvironmentVariable("EM_WORLD_OUTPUT",prior);}
 Console.WriteLine("DYNAMIC_SCENE_UNITY_EXECUTED");return;
}
throw new ArgumentException("Unknown mode; refusing engine launch");
''')
print('DYNAMIC_SCENE_PRIVATE_FIXTURE_PREPARED_NO_ENGINE_LAUNCH')
