"""Prepare private fixtures only. Engine creation goes through the audited common runner."""
from pathlib import Path
import json

home=Path('artifacts/object-equality-check');home.mkdir(exist_ok=True)
unity=Path('artifacts/runtime-ui-api-tests/UnitySource').resolve()
core=Path(json.loads(Path('artifacts/core-engine-launch-tests/paths.json').read_text())['run']).resolve()
folder=unity/'Assets/EngineMigratorObjectEqualityProbe';folder.mkdir(exist_ok=True)
source=Path('tests/EngineMigrator.Checks/Fixtures/object-equality.cs.txt').read_text()
(folder/'AuditObjectEquality.cs').write_text(source)
restored=home/'restored.cs'
if restored.exists():(folder/'RestoredAuditObjectEquality.cs').write_text(restored.read_text().replace('AuditObjectEquality','RestoredAuditObjectEquality'))
header=home/'AuditObjectEquality.h'
if header.exists():(core/'Project/Plugins/EMActorPort/Source/EMActorPort/Public/AuditObjectEquality.h').write_bytes(header.read_bytes())
module=core/'Project/Plugins/EMActorPort/Source/EMActorPort'
(module/'Public/EMGCReferenceProbe.h').write_text('''#pragma once
#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "EMGCReferenceProbe.generated.h"
UCLASS()
class UEMGCReferenceProbe:public UBlueprintFunctionLibrary {
 GENERATED_BODY()
 public: UFUNCTION(BlueprintCallable,Category="EngineMigratorTest") static bool CollectNow();
};
''')
(module/'Private/EMGCReferenceProbe.cpp').write_text('''#include "EMGCReferenceProbe.h"
#include "UObject/UObjectGlobals.h"
bool UEMGCReferenceProbe::CollectNow(){
 bool Completed=false;
 const FDelegateHandle Handle=FCoreUObjectDelegates::GetPostGarbageCollect().AddLambda([&Completed](){Completed=true;});
 CollectGarbage(RF_NoFlags,true);
 FCoreUObjectDelegates::GetPostGarbageCollect().Remove(Handle);
 return Completed;
}
''')
editor=folder/'Editor';editor.mkdir(exist_ok=True)
(editor/'EMObjectEqualityProbe.cs').write_text('''using System;using System.IO;using System.Collections.Generic;using UnityEngine;using UnityEditor;
public static class EMObjectEqualityProbe {
 public static void Run(){try{
 var name=Environment.GetEnvironmentVariable("EM_EQUALITY_CLASS");var type=typeof(AuditObjectEquality).Assembly.GetType(name);var rows=new List<string>();var postRows=new List<string>();var flows=new List<string>();
 for(int ls=0;ls<4;ls++)for(int mode=0;mode<5;mode++){
 GameObject left=null,right=null;var control=new GameObject("EqualityController");
 try{
 var actor=control.AddComponent(type);
 type.GetField("Peer").SetValue(actor,actor);
 if(ls!=0){left=new GameObject("EqualityLeft");if(ls==2)UnityEngine.Object.DestroyImmediate(left);else if(ls==3)left.SetActive(false);}
 if(mode==1)right=left;else if(mode>=2){right=new GameObject("EqualityRight");if(mode==3)UnityEngine.Object.DestroyImmediate(right);else if(mode==4)right.SetActive(false);}
 type.GetField("Left").SetValue(actor,left);type.GetField("Right").SetValue(actor,right);
 var row=new List<int>{ls,mode};foreach(var method in new[]{"Equal","Unequal","LeftMissing","RightMissing","LeftAlive","RightAlive","ViaLocals","Negated"})row.Add(Convert.ToInt32(type.GetMethod(method).Invoke(actor,null)));
 rows.Add("["+string.Join(",",row)+"]");
 GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
 var post=new List<int>{ls,mode};foreach(var method in new[]{"Equal","Unequal","LeftMissing","RightMissing","LeftAlive","RightAlive","ViaLocals","Negated"})post.Add(Convert.ToInt32(type.GetMethod(method).Invoke(actor,null)));postRows.Add("["+string.Join(",",post)+"]");
 var flow=new List<int>{ls,mode,Convert.ToInt32(type.GetMethod("ViaArguments").Invoke(actor,null)),Convert.ToInt32(type.GetMethod("ViaReturns").Invoke(actor,null)),Convert.ToInt32(type.GetMethod("ViaConditional").Invoke(actor,new object[]{true})),Convert.ToInt32(type.GetMethod("ViaConditional").Invoke(actor,new object[]{false})),Convert.ToInt32(type.GetMethod("ViaReflectedArguments").Invoke(actor,null)),Convert.ToInt32(type.GetMethod("ViaReflectedReturns").Invoke(actor,null)),Convert.ToInt32(type.GetMethod("ViaParameterCopy").Invoke(actor,null)),Convert.ToInt32(type.GetMethod("ResetThenCompare").Invoke(actor,null))};flows.Add("["+string.Join(",",flow)+"]");
 }finally{UnityEngine.Object.DestroyImmediate(control);if(left)UnityEngine.Object.DestroyImmediate(left);if(right)UnityEngine.Object.DestroyImmediate(right);}
 }
 File.WriteAllText(Environment.GetEnvironmentVariable("EM_EQUALITY_OUTPUT"),"{\\"status\\":\\"PASS\\",\\"rows\\":["+string.Join(",",rows)+"],\\"afterGarbageCollectionRows\\":["+string.Join(",",postRows)+"],\\"identityFlows\\":["+string.Join(",",flows)+"]}");EditorApplication.Exit(0);
 }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}}
}
''')
runner=home/'runner';runner.mkdir(exist_ok=True)
(runner/'Runner.csproj').write_bytes(Path('artifacts/implicit-truth-check/core-runner/Runner.csproj').read_bytes())
(runner/'Program.cs').write_text('''using EngineMigrator.Core;
var home=Path.GetFullPath("artifacts/object-equality-check");var core=CORE;var unity=UNITY;
if(args.Length!=1)throw new ArgumentException("Explicit mode required");
if(args[0]=="prepare"){
 var source=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/object-equality.cs.txt");var files=ActorScripts.FromUnity(source,true);var back=ActorScripts.ToUnity(files,true).Single().Code;
 File.WriteAllText(Path.Combine(home,"restored.cs"),back);File.WriteAllText(Path.Combine(home,"AuditObjectEquality.h"),files.Single().Code);
 File.WriteAllText(Path.Combine(core,"Project/Plugins/EMActorPort/Source/EMActorPort/Public/AuditObjectEquality.h"),files.Single().Code);return;
}
if(args[0] is "unity-original" or "unity-restored"){
 var output=Path.Combine(home,args[0]+".json");if(File.Exists(output))throw new IOException("Fresh capture required");
 var settings=Path.Combine(unity,"ProjectSettings/EditorSettings.asset");var before=File.ReadAllBytes(settings);
 Environment.SetEnvironmentVariable("EM_EQUALITY_CLASS",args[0]=="unity-original"?"AuditObjectEquality":"RestoredAuditObjectEquality");Environment.SetEnvironmentVariable("EM_EQUALITY_OUTPUT",output);
 try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(240));await ResourcePorting.RunEditor(EngineLaunchSafety.Unity,["-batchmode","-nographics","-projectPath",unity,"-executeMethod","EMObjectEqualityProbe.Run","-logFile",Path.Combine(home,args[0]+".log")],Directory.GetParent(unity)!.FullName,timeout.Token);if(!File.Exists(output))throw new IOException("Capture missing");}
 finally{File.WriteAllBytes(settings,before);Environment.SetEnvironmentVariable("EM_EQUALITY_CLASS",null);Environment.SetEnvironmentVariable("EM_EQUALITY_OUTPUT",null);}return;
}
var descriptor=Path.Combine(core,"Project/RuntimeWorld.uproject");
if(args[0]=="full-reverse"){
 var publicRoot=Path.GetFullPath("artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Target/Plugins/EMActorPort/Source/EMActorPort/Public");
 var headers=Directory.GetFiles(publicRoot,"*.h").Select(p=>new ActorFile(Path.GetFileName(p),File.ReadAllText(p))).Where(f=>f.Code.StartsWith("// EM_ACTOR ",StringComparison.Ordinal)).ToArray();
 if(headers.Length!=49)throw new InvalidOperationException("Expected complete 49-header regression fixture");
 var restored=ActorScripts.ToUnity(headers);if(restored.Count!=49)throw new InvalidOperationException("Full reverse script count changed");
 var output=Path.Combine(home,"full-restored-scripts");Directory.CreateDirectory(output);foreach(var file in restored)File.WriteAllText(Path.Combine(output,file.Name),file.Code);
 string Sha(string path)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
 foreach(var file in restored){var prior=Path.GetFullPath(Path.Combine("artifacts/implicit-truth-check/full-restored-scripts",file.Name));if(Sha(prior)!=Sha(Path.Combine(output,file.Name)))throw new InvalidOperationException("Public reverse output changed: "+file.Name);}
 File.WriteAllText(Path.Combine(home,"full-reverse-verification.json"),System.Text.Json.JsonSerializer.Serialize(new{status="SOURCE_REGRESSION_PASS",headers=headers.Length,scripts=restored.Count,actualEngineExecuted=false,priorRestoredSourcesEqual=true}));Console.WriteLine("FULL_49_HEADER_REVERSE_PASS_NO_ENGINE_STARTED");return;
}
if(args[0]=="build"){using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));await ResourcePorting.RunEditor(EngineLaunchSafety.Dotnet,[EngineLaunchSafety.Ubt,"RuntimeWorldEditor","Win64","Development","-Project="+descriptor,"-NoHotReload","-NoPCH","-NoSharedPCH","-log="+Path.Combine(home,"native-build.log")],core,timeout.Token);return;}
if(args[0]=="native"){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(180));await ResourcePorting.RunEditor(EngineLaunchSafety.Unreal,ResourcePorting.UnrealArgs(descriptor,Path.Combine(home,"native-probe.py"),Path.Combine(home,"native.log")),core,timeout.Token);return;}
throw new ArgumentException("Unknown mode; refusing launch");
'''.replace('var core=CORE;var unity=UNITY;', 'var core='+json.dumps(str(core))+';var unity='+json.dumps(str(unity))+';'))
(home/'native-probe.py').write_text('''import json,traceback,unreal
from pathlib import Path
output=Path(__file__).resolve().parent/'native-execution.json'
if output.exists():raise RuntimeError('Fresh equality capture required')
rows=[];post_rows=[];gc_completed=[];flows=[]
try:
 unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
 cls=unreal.load_class(None,'/Script/EMActorPort.AuditObjectEquality')
 if cls is None:raise RuntimeError('Generated equality actor missing')
 sub=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
 def spawn():return sub.spawn_actor_from_class(cls,unreal.Vector())
 def row(actor,ls,mode):return [ls,mode]+[int(getattr(actor,name)()) for name in ['equal','unequal','left_missing','right_missing','left_alive','right_alive','via_locals','negated']]
 for ls in range(4):
  for mode in range(5):
   actor=spawn();left=spawn() if ls else None;right=left if mode==1 else spawn() if mode>=2 else None
   actor.set_editor_property('peer',actor)
   actor.set_editor_property('left',left);actor.set_editor_property('right',right)
   if ls==2:sub.destroy_actor(left)
   elif ls==3:left.tags=[unreal.Name('EMInactiveSelf')]
   if mode==3:sub.destroy_actor(right)
   elif mode==4:right.tags=[unreal.Name('EMInactiveSelf')]
   rows.append(row(actor,ls,mode));completed=unreal.EMGCReferenceProbe.collect_now();gc_completed.append(completed)
   if not completed:raise RuntimeError('Synchronous GC completion callback missing')
   post_rows.append(row(actor,ls,mode))
   flows.append([ls,mode,int(actor.via_arguments()),int(actor.via_returns()),int(actor.via_conditional(True)),int(actor.via_conditional(False)),int(actor.via_reflected_arguments()),int(actor.via_reflected_returns()),int(actor.via_parameter_copy()),int(actor.reset_then_compare())])
   sub.destroy_actor(actor)
   if ls in (1,3):sub.destroy_actor(left)
   if mode in (2,4):sub.destroy_actor(right)
 output.write_text(json.dumps({'status':'CAPTURED','rows':rows,'afterGarbageCollectionRows':post_rows,'synchronousGCCompleted':gc_completed,'identityFlows':flows}))
except Exception:
 output.write_text(json.dumps({'status':'FAIL','error':traceback.format_exc()}));raise
''')
print('PRIVATE_EQUALITY_FIXTURES_PREPARED_NO_ENGINE_STARTED')
