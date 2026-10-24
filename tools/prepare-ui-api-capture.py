"""Prepare audited UI API regression capture; no engine starts here."""
from pathlib import Path
import json
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/ui-equality-check';unity=root/'artifacts/runtime-ui-api-tests/UnitySource';target=root/'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Target'
observer=unity/'Assets/Editor/RuntimeWorldUnityProbe.cs';text=observer.read_text();text=text.replace('EditorSceneManager.OpenScene("Assets/RuntimeWorld/RuntimeStart.unity")','EditorSceneManager.OpenScene(Environment.GetEnvironmentVariable("EM_WORLD_SCENE") ?? "Assets/RuntimeWorld/RuntimeStart.unity")');observer.write_text(text)
runner=home/'api-runner';runner.mkdir(exist_ok=True);(runner/'Runner.csproj').write_bytes((home/'runner/Runner.csproj').read_bytes())
(runner/'Program.cs').write_text('''using EngineMigrator.Core;using System.Text.Json.Nodes;
var home=HOME;var unity=UNITY;var target=TARGET;var work=Directory.GetParent(target)!.FullName;var descriptor=Path.Combine(target,"RuntimeWorld.uproject");
if(args.Length!=1||args[0]is not("native"or"original"or"restored"))throw new ArgumentException("Explicit API capture mode required");
var output=Path.Combine(home,"ui-api-"+args[0]+".json");if(File.Exists(output))throw new IOException("Fresh capture required");
if(args[0]=="native"){
 var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(target,".EngineMigratorProject.json")))!;var map=manifest["units"]!.AsArray().Single(n=>n!["path"]!.GetValue<string>()=="Assets/RuntimeUIApi/UIApi.unity")!["map"]!.GetValue<string>();
 using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(180));await ResourcePorting.RunEditor(EngineLaunchSafety.Unreal,[descriptor,map,"-game","-nullrhi","-RenderOffscreen","-nosound","-nosplash","-unattended","-nop4","-UseFixedTimeStep","-FPS=50","-EMRuntimeProbe="+output,"-abslog="+Path.Combine(home,"ui-api-native.log")],work,timeout.Token);if(!File.Exists(output))throw new IOException("Actual API capture missing");return;
}
var settings=Path.Combine(unity,"ProjectSettings/EditorSettings.asset");var priorSettings=File.ReadAllBytes(settings);var priorOutput=Environment.GetEnvironmentVariable("EM_WORLD_OUTPUT");var priorScene=Environment.GetEnvironmentVariable("EM_WORLD_SCENE");var snapshots=new Dictionary<string,byte[]>();
try{
 if(args[0]=="restored")foreach(var file in Directory.GetFiles(Path.Combine(home,"world-restored-scripts"),"*.cs")){var path=Directory.GetFiles(Path.Combine(unity,"Assets"),Path.GetFileName(file),SearchOption.AllDirectories).Single();snapshots.Add(path,File.ReadAllBytes(path));File.WriteAllBytes(path,File.ReadAllBytes(file));}
 Environment.SetEnvironmentVariable("EM_WORLD_OUTPUT",output);Environment.SetEnvironmentVariable("EM_WORLD_SCENE","Assets/RuntimeUIApi/UIApi.unity");
 using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(240));await ResourcePorting.RunEditor(EngineLaunchSafety.Unity,["-batchmode","-nographics","-projectPath",unity,"-executeMethod","EMRuntimeWorldProbe.Run","-logFile",Path.Combine(home,"ui-api-"+args[0]+".log")],Directory.GetParent(unity)!.FullName,timeout.Token);if(!File.Exists(output))throw new IOException("Actual API capture missing");
}finally{foreach(var item in snapshots)File.WriteAllBytes(item.Key,item.Value);File.WriteAllBytes(settings,priorSettings);Environment.SetEnvironmentVariable("EM_WORLD_OUTPUT",priorOutput);Environment.SetEnvironmentVariable("EM_WORLD_SCENE",priorScene);}
'''.replace('var home=HOME;var unity=UNITY;var target=TARGET;','var home='+json.dumps(str(home))+';var unity='+json.dumps(str(unity))+';var target='+json.dumps(str(target))+';'))
print('PRIVATE_UI_API_CAPTURE_PREPARED_NO_ENGINE_STARTED')
