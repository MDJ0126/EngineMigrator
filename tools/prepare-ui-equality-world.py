"""Prepare full private-project weak-reference roundtrip; starts no engine."""
from pathlib import Path
import json

home=Path('artifacts/ui-equality-check').resolve();unity=Path('artifacts/runtime-ui-api-tests/UnitySource').resolve()
target=Path('artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Target').resolve()
public=target/'Plugins/EMActorPort/Source/EMActorPort/Public'
backup=home/'prior-target-headers';backup.mkdir(exist_ok=True)
headers=[p for p in public.glob('*.h')if p.read_text().startswith('// EM_ACTOR ')]
if len(headers)!=49:raise RuntimeError('Expected existing 49-script fixture')
for path in headers:
 saved=backup/path.name
 if not saved.exists():saved.write_bytes(path.read_bytes())
source=Path('artifacts/implicit-truth-check/runtime-spawner-original.cs.txt').read_text()
source=source.replace('public int NextIndex()', 'public GameObject Alias;public GameObject FirstDestroyed;public GameObject SecondDestroyed;public bool AliasBeforeDestroy;public bool AliasAfterDestroy;public bool DifferentBeforeGC;public bool DifferentAfterGC;public bool AliasAfterGC;public bool OldAliasDiffersFromNew;\n public int NextIndex()')
source=source.replace('Spawned=Instantiate(Source);FirstCreated=Spawned;', 'Spawned=Instantiate(Source);Alias=Spawned;AliasBeforeDestroy=Alias==Spawned;FirstCreated=Spawned;')
source=source.replace('if(Frame==20)', 'if(Frame==18){FirstDestroyed=new GameObject("IdentityLeft");SecondDestroyed=new GameObject("IdentityRight");Destroy(FirstDestroyed);Destroy(SecondDestroyed);}if(Frame==22){AliasAfterDestroy=Alias==Spawned;DifferentBeforeGC=FirstDestroyed!=SecondDestroyed;}if(Frame==28){AliasAfterGC=Alias==Spawned;DifferentAfterGC=FirstDestroyed!=SecondDestroyed;}if(Frame==32){OldAliasDiffersFromNew=Alias!=Spawned;}if(Frame==20)')
source=source.replace('public GameObject Alias;', 'public Transform FirstTransform;public Transform SecondTransform;public Transform SavedTransform;public bool TransformBeforeGC;public bool TransformAfterGC;public bool TransformAliasAfterGC;public bool TransformOldDiffersFromNew;public GameObject Alias;')
source=source.replace('Alias=Spawned;AliasBeforeDestroy', 'Alias=Spawned;SavedTransform=Spawned.transform;AliasBeforeDestroy')
source=source.replace('Destroy(FirstDestroyed);Destroy(SecondDestroyed);', 'FirstTransform=FirstDestroyed.transform;SecondTransform=SecondDestroyed.transform;Destroy(FirstDestroyed);Destroy(SecondDestroyed);')
source=source.replace('DifferentBeforeGC=FirstDestroyed!=SecondDestroyed;', 'DifferentBeforeGC=FirstDestroyed!=SecondDestroyed;TransformBeforeGC=FirstTransform!=SecondTransform;')
source=source.replace('DifferentAfterGC=FirstDestroyed!=SecondDestroyed;', 'DifferentAfterGC=FirstDestroyed!=SecondDestroyed;TransformAfterGC=FirstTransform!=SecondTransform;TransformAliasAfterGC=SavedTransform==SavedTransform;')
source=source.replace('OldAliasDiffersFromNew=Alias!=Spawned;', 'OldAliasDiffersFromNew=Alias!=Spawned;TransformOldDiffersFromNew=SavedTransform!=Spawned.transform;')
(home/'runtime-spawner-original.cs.txt').write_text(source)
observer=Path('tools/runtime-probe/RuntimeWorldUnityProbe.cs').read_text()
observer=observer.replace('public class EMWorldCapture {public string status;', 'public class EMWorldCapture {public string status;public bool gcCompleted;')
needle="frame.actors.Add(row);"
observer=observer.replace(needle,needle+'if(b.GetType().Name=="RuntimeSpawner"&&(int)b.GetType().GetField("Frame").GetValue(b)==24&&!result.gcCompleted){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();result.gcCompleted=true;}')
observer=observer.replace('EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s=>s.enabled).path)','EditorSceneManager.OpenScene("Assets/RuntimeWorld/RuntimeStart.unity")')
(unity/'Assets/Editor/RuntimeWorldUnityProbe.cs').write_text(observer)
native=Path('tools/runtime-probe/RuntimeWorldProbe.cpp').read_text()
native=native.replace('#include "Engine/World.h"','#include "Engine/World.h"\n#include "Engine/Engine.h"\n#include "UObject/UObjectGlobals.h"')
native=native.replace('int32 TickCount=0;','int32 TickCount=0;bool EM_GCRequested=false;int32 EM_GCCompleted=0;\nvoid EM_GCFinished(){if(EM_GCRequested)++EM_GCCompleted;}')
native=native.replace('Value->SetObjectField(TEXT("state"),State);','if(A->GetClass()->GetName()==TEXT("RuntimeSpawner")&&State->GetNumberField(TEXT("Frame"))==24&&!EM_GCRequested){EM_GCRequested=true;GEngine->ForceGarbageCollection(true);}\n  Value->SetObjectField(TEXT("state"),State);')
native=native.replace('Frame->SetArrayField(TEXT("actors"),Actors);','Frame->SetBoolField(TEXT("gcCompleted"),EM_GCCompleted>0);Frame->SetArrayField(TEXT("actors"),Actors);')
native=native.replace('Root->SetArrayField(TEXT("frames"),Frames);','Root->SetBoolField(TEXT("gcCompleted"),EM_GCCompleted>0);Root->SetArrayField(TEXT("frames"),Frames);')
native=native.replace('if(!EM_ProbeOutput.IsEmpty())FWorldDelegates::OnWorldTickEnd.AddStatic(&Observe);','if(!EM_ProbeOutput.IsEmpty()){FCoreUObjectDelegates::GetPostGarbageCollect().AddStatic(&EM_GCFinished);FWorldDelegates::OnWorldTickEnd.AddStatic(&Observe);}')
(home/'RuntimeWorldGCProbe.cpp').write_text(native)
(target/'Source/EMPortHost/EMPortHost.cpp').write_text(native)
runner=home/'world-runner';runner.mkdir(exist_ok=True)
(runner/'Runner.csproj').write_bytes((home/'runner/Runner.csproj').read_bytes())
(runner/'Program.cs').write_text('''using EngineMigrator.Core;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json.Nodes;
var home=HOME;var unity=UNITY;var target=TARGET;var work=Directory.GetParent(target)!.FullName;var descriptor=Path.Combine(target,"RuntimeWorld.uproject");
if(args.Length!=1)throw new ArgumentException("Explicit mode required");
if(args[0]is "prepare"or "verify-public"){
 var prior=Path.GetFullPath("artifacts/implicit-truth-check/full-restored-scripts");var sources=Directory.GetFiles(prior,"*.cs");if(sources.Length!=49)throw new Exception("Expected 49 prior scripts");
 var combined="using UnityEngine;\\nusing UnityEngine.UI;\\nusing UnityEngine.SceneManagement;\\n"+string.Join("\\n",sources.Select(p=>(Path.GetFileName(p)=="RuntimeSpawner.cs"?File.ReadAllText(Path.Combine(home,"runtime-spawner-original.cs.txt")):File.ReadAllText(p)).Replace("using UnityEngine;","").Replace("using UnityEngine.UI;","").Replace("using UnityEngine.SceneManagement;","")));
 var files=ActorScripts.FromUnity(combined);if(files.Count!=49)throw new Exception("Unexpected forward script count");
 if(args[0]=="verify-public"){
  foreach(var file in files)if(File.ReadAllText(Path.Combine(target,"Plugins/EMActorPort/Source/EMActorPort/Public",file.Name))!=file.Code)throw new Exception("Public pipeline differs from engine-tested header: "+file.Name);
  var back=ActorScripts.ToUnity(files);int legacyMatches=0;foreach(var file in back){if(File.ReadAllText(Path.Combine(home,"world-restored-scripts",file.Name))!=file.Code)throw new Exception("Public reverse differs: "+file.Name);if(file.Name!="RuntimeSpawner.cs"){var oldTokens=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(prior,file.Name))).GetRoot().DescendantTokens().Select(t=>(t.RawKind,t.Text,t.IsMissing));var newTokens=CSharpSyntaxTree.ParseText(file.Code).GetRoot().DescendantTokens().Select(t=>(t.RawKind,t.Text,t.IsMissing));if(!oldTokens.SequenceEqual(newTokens)){File.WriteAllText(Path.Combine(home,"public-mismatch-"+file.Name+".txt"),file.Code);throw new Exception("Unrelated restored script changed: "+file.Name);}legacyMatches++;}}
  File.WriteAllText(Path.Combine(home,"public-pipeline-verification.json"),System.Text.Json.JsonSerializer.Serialize(new{status="PUBLIC_PIPELINE_MATCHES_ENGINE_TESTED_HEADERS",headers=files.Count,restored=back.Count,unchangedPriorScripts=legacyMatches,priorComparison="Roslyn token kind/text/missing sequence (trivia ignored)",actualEngineStartedByThisCheck=false}));Console.WriteLine("PUBLIC_PIPELINE_49_EXACT_HEADERS_AND_48_PRIOR_SCRIPT_TOKENS_MATCH_NO_ENGINE_STARTED");return;
 }
 foreach(var file in files)File.WriteAllText(Path.Combine(target,"Plugins/EMActorPort/Source/EMActorPort/Public",file.Name),file.Code);
 File.WriteAllText(Path.Combine(home,"RuntimeSpawner.h"),files.Single(f=>f.Name=="RuntimeSpawner.h").Code);
 var restored=ActorScripts.ToUnity(files,true);var folder=Path.Combine(home,"world-restored-scripts");Directory.CreateDirectory(folder);foreach(var file in restored)File.WriteAllText(Path.Combine(folder,file.Name),file.Code);
 File.WriteAllText(Path.Combine(home,"runtime-spawner-restored.cs.txt"),restored.Single(f=>f.Name=="RuntimeSpawner.cs").Code);Console.WriteLine("ALL_49_WEAK_REFERENCE_HEADERS_AND_RESTORED_SCRIPTS_GENERATED_NO_ENGINE_STARTED");return;
}
if(args[0]=="build"){using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));await ResourcePorting.RunEditor(EngineLaunchSafety.Dotnet,[EngineLaunchSafety.Ubt,"RuntimeWorldEditor","Win64","Development","-Project="+descriptor,"-NoHotReload","-NoPCH","-NoSharedPCH","-log="+Path.Combine(home,"world-build.log")],work,timeout.Token);return;}
if(args[0]=="world-native"){
 var capture=Path.Combine(home,"world-native.json");if(File.Exists(capture))throw new IOException("Fresh capture required");
 var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(target,".EngineMigratorProject.json")))!;var map=manifest["units"]!.AsArray().Single(n=>n!["path"]!.GetValue<string>()=="Assets/RuntimeWorld/RuntimeStart.unity")!["map"]!.GetValue<string>();
 using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(180));await ResourcePorting.RunEditor(EngineLaunchSafety.Unreal,[descriptor,map,"-game","-nullrhi","-RenderOffscreen","-nosound","-nosplash","-unattended","-nop4","-UseFixedTimeStep","-FPS=50","-EMRuntimeProbe="+capture,"-abslog="+Path.Combine(home,"world-native.log")],work,timeout.Token);if(!File.Exists(capture))throw new IOException("World capture missing");return;
}
if(args[0]is "world-original"or "world-restored"){
 var capture=Path.Combine(home,args[0]+".json");if(File.Exists(capture))throw new IOException("Fresh capture required");var source=Path.Combine(unity,"Assets/RuntimeWorld/RuntimeSpawner.cs");var oldSource=File.ReadAllBytes(source);var settings=Path.Combine(unity,"ProjectSettings/EditorSettings.asset");var oldSettings=File.ReadAllBytes(settings);var oldOutput=Environment.GetEnvironmentVariable("EM_WORLD_OUTPUT");
 File.WriteAllText(source,File.ReadAllText(Path.Combine(home,args[0]=="world-original"?"runtime-spawner-original.cs.txt":"runtime-spawner-restored.cs.txt")));Environment.SetEnvironmentVariable("EM_WORLD_OUTPUT",capture);
 try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(240));await ResourcePorting.RunEditor(EngineLaunchSafety.Unity,["-batchmode","-nographics","-projectPath",unity,"-executeMethod","EMRuntimeWorldProbe.Run","-logFile",Path.Combine(home,args[0]+".log")],Directory.GetParent(unity)!.FullName,timeout.Token);if(!File.Exists(capture))throw new IOException("Unity world capture missing");}
 finally{File.WriteAllBytes(source,oldSource);File.WriteAllBytes(settings,oldSettings);Environment.SetEnvironmentVariable("EM_WORLD_OUTPUT",oldOutput);}return;
}
if(args[0]is "legacy-regression"or "legacy-regression-final"){
 var folder=Path.Combine(home,args[0]);if(Directory.Exists(folder)&&Directory.EnumerateFileSystemEntries(folder).Any())throw new IOException("Fresh regression directory required");Directory.CreateDirectory(folder);
 var expected=JsonNode.Parse(File.ReadAllText("artifacts/runtime-ui-api-tests/extended-original-audited-v3/unity-execution.json"))!;if(expected["frames"]!.AsArray().Count!=250||expected["deltas"]!.AsArray().Count!=250)throw new Exception("Actual 250-frame Unity capture required");
 var input=Path.Combine(folder,"input.json");File.WriteAllText(input,new JsonObject{["deltas"]=expected["deltas"]!.DeepClone(),["endPlayOnly"]=true,["clicks"]=expected["clicks"]!.DeepClone()}.ToJsonString());
 var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(target,".EngineMigratorProject.json")))!;var map=manifest["units"]!.AsArray().Single(n=>n!["id"]!.GetValue<string>()=="s_99c9720ab356a0642a771bea13969a05")!["map"]!.GetValue<string>();
 File.WriteAllText(Path.Combine(target,"SceneVerify.json"),new JsonObject{["map"]=map,["input"]=input,["output"]=folder,["actorNames"]=new JsonObject()}.ToJsonString());
 using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(180));await ResourcePorting.RunEditor(EngineLaunchSafety.Unreal,ResourcePorting.UnrealArgs(descriptor,Path.GetFullPath("adapters/unreal/verify_scene.py"),Path.Combine(folder,"unreal.log")),work,timeout.Token);if(!File.Exists(Path.Combine(folder,"scene-execution.json")))throw new IOException("Native regression capture missing");return;
}
throw new ArgumentException("Unknown mode; refusing engine launch");
'''.replace('var home=HOME;var unity=UNITY;var target=TARGET;', 'var home='+json.dumps(str(home))+';var unity='+json.dumps(str(unity))+';var target='+json.dumps(str(target))+';'))
print('PRIVATE_GC_WORLD_FIXTURES_PREPARED_NO_ENGINE_STARTED')
