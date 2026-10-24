"""Prepare private object-truth fixtures; never starts an engine."""
from pathlib import Path
import json
import re

home=Path('artifacts/implicit-truth-check');unity=Path('artifacts/runtime-ui-api-tests/UnitySource')
core=Path(json.loads(Path('artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
(core/'Project/Source/EMPortHost/EMPortHost.cpp').write_bytes(Path('tools/runtime-probe/RuntimeWorldProbe.cpp').read_bytes())
wrapper=core/'Project/Plugins/EMActorPort/Source/EMActorPort/Private/EMConditionalProbe.cpp'
wrapper_text=wrapper.read_text()
if '#include "UObject/Package.h"' not in wrapper_text:wrapper.write_text('#include "UObject/Package.h"\n'+wrapper_text)
(core/'Project/Plugins/EMActorPort/Source/EMActorPort/Public/AuditImplicitTruth.h').write_bytes((home/'AuditImplicitTruth.h').read_bytes())
spawner=unity/'Assets/RuntimeWorld/RuntimeSpawner.cs';prior=home/'prior-runtime-spawner.cs.txt'
if not prior.exists():prior.write_bytes(spawner.read_bytes())
source=prior.read_text(encoding='utf-8-sig')
if 'Spawned!=null' not in source:raise RuntimeError('Expected prior spawner fixture missing')
source=source.replace('public bool AliveImmediatelyAfterDestroy;', 'public bool AliveImmediatelyAfterDestroy;public bool AliveNextFrame;public bool InactiveStillAlive;public bool MissingAfterDestroy;')
source=source.replace('Spawned!=null','Spawned').replace('Frame++;','Frame++;if(Frame==12){Spawned.SetActive(false);InactiveStillAlive=Spawned;Spawned.SetActive(true);}if(Frame==22){AliveNextFrame=Spawned;if(!Spawned){MissingAfterDestroy=true;}}')
(home/'runtime-spawner-original.cs.txt').write_text(source);spawner.write_text(source)
folder=unity/'Assets/EngineMigratorImplicitTruthProbe';folder.mkdir(exist_ok=True)
for restored in [False,True]:
 code=(home/('restored.cs' if restored else 'original.cs')).read_text()
 name=('Restored' if restored else '')+'AuditImplicitTruth'
 if restored:code=re.sub(r'\bAuditImplicitTruth\b',name,code)
 (folder/(name+'.cs')).write_text(code)
editor=folder/'Editor';editor.mkdir(exist_ok=True)
standalone='''using System;using System.IO;using System.Collections.Generic;using UnityEngine;using UnityEngine.UI;
public static class EMImplicitTruthStandalone{
 public static void Run(){
  var output=OUTPUT;if(File.Exists(output))throw new IOException("Fresh truth capture required");var runs=new List<string>();
  foreach(var prefix in new[]{"","Restored"}){
   var type=typeof(AuditImplicitTruth).Assembly.GetType(prefix+"AuditImplicitTruth");var rows=new List<string>();
   foreach(var ls in new[]{0,1,2,3})foreach(var rs in new[]{0,1,2,3}){
    var control=new GameObject("TruthControl");var actor=control.AddComponent(type);GameObject left=null,right=null;
    try{
     if(ls!=0){left=new GameObject("TruthLeft",typeof(RectTransform),typeof(Canvas),typeof(Button));var peer=left.AddComponent(type);
      type.GetField("Target").SetValue(actor,left);type.GetField("Pose").SetValue(actor,left.transform);type.GetField("CanvasTarget").SetValue(actor,left.GetComponent<Canvas>());type.GetField("ButtonTarget").SetValue(actor,left.GetComponent<Button>());type.GetField("Peer").SetValue(actor,peer);
      if(ls==2)UnityEngine.Object.DestroyImmediate(left);else if(ls==3)left.SetActive(false);
     }
     if(rs!=0){right=new GameObject("TruthRight");type.GetField("Right").SetValue(actor,right);if(rs==2)UnityEngine.Object.DestroyImmediate(right);else if(rs==3)right.SetActive(false);}
     var row=new List<int>{ls,rs};foreach(var method in new[]{"Truth","Not","Branch","Local","Arg","Explicit","EqualTrue","PeerNotFalse","Loops","ReadFields","Lazy"})row.Add(Convert.ToInt32(type.GetMethod(method).Invoke(actor,null)));
     row.Add((int)type.GetField("Calls").GetValue(actor));row.Add(Convert.ToInt32(type.GetMethod("LazyOr").Invoke(actor,null)));row.Add((int)type.GetField("Calls").GetValue(actor));foreach(var choose in new[]{true,false})row.Add(Convert.ToInt32(type.GetMethod("Choose").Invoke(actor,new object[]{choose})));rows.Add("["+string.Join(",",row)+"]");
    }finally{UnityEngine.Object.DestroyImmediate(control);if(left)UnityEngine.Object.DestroyImmediate(left);if(right)UnityEngine.Object.DestroyImmediate(right);}
   }
   runs.Add("["+string.Join(",",rows)+"]");
  }
  File.WriteAllText(output,"{\\"status\\":\\"PASS\\",\\"runs\\":["+string.Join(",",runs)+"]}");Debug.Log("IMPLICIT_TRUTH_ACTUAL_UNITY_CAPTURED");
 }
}
'''.replace('OUTPUT',json.dumps(str((home/'unity-truth-rows.json').resolve())))
(editor/'EMImplicitTruthStandalone.cs').write_text(standalone)
observer=Path('tools/runtime-probe/RuntimeWorldUnityProbe.cs').read_text()
observer=observer.replace('public static class EMRuntimeWorldProbe','public static class EMImplicitWorldProbe').replace('EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s=>s.enabled).path)', 'if(!File.Exists('+json.dumps(str((home/'unity-truth-rows.json').resolve()))+'))EMImplicitTruthStandalone.Run();EditorSceneManager.OpenScene("Assets/RuntimeWorld/RuntimeStart.unity")')
(unity/'Assets/Editor/RuntimeWorldUnityProbe.cs').write_text(observer)
runner=home/'runner';runner.mkdir(exist_ok=True)
(runner/'Runner.csproj').write_bytes(Path('artifacts/dynamic-scene-check/runner/Runner.csproj').read_bytes())
program=Path('artifacts/dynamic-scene-check/runner/Program.cs').read_text().replace('artifacts/dynamic-scene-check','artifacts/implicit-truth-check').replace('EMDynamicWorldProbe.Run','EMImplicitWorldProbe.Run')
(runner/'Program.cs').write_text(program)
core_runner=home/'core-runner';core_runner.mkdir(exist_ok=True)
(core_runner/'Runner.csproj').write_bytes((runner/'Runner.csproj').read_bytes())
(core_runner/'Program.cs').write_text('''using EngineMigrator.Core;
var run=Path.GetFullPath(CORE);var home=Path.GetFullPath("artifacts/implicit-truth-check");var descriptor=Path.Combine(run,"Project/RuntimeWorld.uproject");
if(args.Length!=1)throw new ArgumentException("Explicit mode required");
if(args[0]=="build"){
 using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
 await ResourcePorting.RunEditor(EngineLaunchSafety.Dotnet,[EngineLaunchSafety.Ubt,"RuntimeWorldEditor","Win64","Development","-Project="+descriptor,"-NoHotReload","-NoPCH","-NoSharedPCH","-log="+Path.Combine(home,"core-native-build.log")],run,timeout.Token);Console.WriteLine("TRUTH_CORE_BUILD_EXECUTED");return;
}
if(args[0]=="probe"){
 using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(180));
 await ResourcePorting.RunEditor(EngineLaunchSafety.Unreal,ResourcePorting.UnrealArgs(descriptor,Path.Combine(home,"native-truth-probe.py"),Path.Combine(home,"native-truth.log")),run,timeout.Token);Console.WriteLine("TRUTH_CORE_NATIVE_EXECUTED");return;
}
throw new ArgumentException("Unknown mode; refusing engine launch");
'''.replace('Path.GetFullPath(CORE)','Path.GetFullPath('+json.dumps(str(core.resolve()))+')'))
(home/'native-truth-probe.py').write_text('''import unreal,json,re,traceback
from pathlib import Path
home=Path(__file__).resolve().parent;output=home/'native-truth-rows.json'
if output.exists():raise RuntimeError('Fresh capture required')
rows=[]
def call(actor,name,*args):
 name=re.sub(r'([a-z0-9])([A-Z])',r'\\1_\\2',name).lower();fn=getattr(actor,name,None) or getattr(actor,name+'_',None)
 if fn is None:raise RuntimeError('Native method missing: '+name)
 return int(fn(*args))
try:
 unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
 cls=unreal.load_class(None,'/Script/EMActorPort.AuditImplicitTruth');sub=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
 if cls is None:raise RuntimeError('Generated truth class missing')
 def spawn():return sub.spawn_actor_from_class(cls,unreal.Vector())
 for ls in range(4):
  for rs in range(4):
   actor=spawn();left=spawn() if ls else None;right=spawn() if rs else None
   for field in ['target','pose','canvas_target','button_target','peer']:actor.set_editor_property(field,left)
   actor.set_editor_property('right',right)
   if ls==2:sub.destroy_actor(left)
   elif ls==3:left.tags=[unreal.Name('EMInactiveSelf')]
   if rs==2:sub.destroy_actor(right)
   elif rs==3:right.tags=[unreal.Name('EMInactiveSelf')]
   row=[ls,rs]+[call(actor,m) for m in ['Truth','Not','Branch','Local','Arg','Explicit','EqualTrue','PeerNotFalse','Loops','ReadFields','Lazy']]
   row+=[actor.get_editor_property('calls'),call(actor,'LazyOr'),actor.get_editor_property('calls'),call(actor,'Choose',True),call(actor,'Choose',False)];rows.append(row)
   sub.destroy_actor(actor)
   if ls in [1,3]:sub.destroy_actor(left)
   if rs in [1,3]:sub.destroy_actor(right)
 output.write_text(json.dumps({'status':'CAPTURED','rows':rows},indent=2));unreal.log('IMPLICIT_TRUTH_NATIVE_CAPTURED')
except Exception:
 output.write_text(json.dumps({'status':'FAIL','rows':rows,'error':traceback.format_exc()},indent=2));raise
''')
print('IMPLICIT_TRUTH_FIXTURES_PREPARED_NO_ENGINE_LAUNCH')
