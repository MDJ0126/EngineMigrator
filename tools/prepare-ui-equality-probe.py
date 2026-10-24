"""Prepare owned component/owner deletion fixtures. Engine launches use the audited common runner."""
from pathlib import Path
import json

home=Path('artifacts/ui-equality-check').resolve();home.mkdir(exist_ok=True)
unity=Path('artifacts/runtime-ui-api-tests/UnitySource').resolve()
core=Path(json.loads(Path('artifacts/core-engine-launch-tests/paths.json').read_text())['run']).resolve()
folder=unity/'Assets/EngineMigratorUIEqualityProbe';folder.mkdir(exist_ok=True)
source=Path('tests/EngineMigrator.Checks/Fixtures/ui-equality.cs.txt').read_text()
(folder/'AuditUIEquality.cs').write_text(source)
if (home/'restored.cs').exists():(folder/'RestoredAuditUIEquality.cs').write_text((home/'restored.cs').read_text().replace('AuditUIEquality','RestoredAuditUIEquality'))
editor=folder/'Editor';editor.mkdir(exist_ok=True)
(editor/'EMUIEqualityProbe.cs').write_text('''using System;using System.IO;using System.Collections.Generic;using UnityEngine;using UnityEditor;
public static class EMUIEqualityProbe {
 public static void Run(){try{
 var type=typeof(AuditUIEquality).Assembly.GetType(Environment.GetEnvironmentVariable("EM_UI_CLASS"));var rows=new List<string>();var postRows=new List<string>();
 for(int ls=0;ls<5;ls++)for(int mode=0;mode<6;mode++){
 if(ls==0&&mode==6)continue;
 GameObject leftOwner=null,rightOwner=null;Component left=null,right=null;var control=new GameObject("ComponentController");
 try{
 var actor=control.AddComponent(type);
 if(ls!=0){leftOwner=new GameObject("ComponentLeftOwner");left=leftOwner.AddComponent<Canvas>();leftOwner.AddComponent<UnityEngine.UI.Button>();}
 if(mode==1){rightOwner=leftOwner;right=left;}else if(mode==6){rightOwner=leftOwner;right=rightOwner.AddComponent<Canvas>();rightOwner.AddComponent<UnityEngine.UI.Button>();}else if(mode>=2){rightOwner=new GameObject("ComponentRightOwner");right=rightOwner.AddComponent<Canvas>();rightOwner.AddComponent<UnityEngine.UI.Button>();}
 type.GetField("Left").SetValue(actor,left);type.GetField("Right").SetValue(actor,right);type.GetField("LeftButton").SetValue(actor,leftOwner?leftOwner.GetComponent<UnityEngine.UI.Button>():null);type.GetField("RightButton").SetValue(actor,rightOwner?rightOwner.GetComponent<UnityEngine.UI.Button>():null);type.GetField("LeftOwner").SetValue(actor,leftOwner);type.GetField("RightOwner").SetValue(actor,rightOwner);
 if(ls==2)UnityEngine.Object.DestroyImmediate(left);else if(ls==3)leftOwner.SetActive(false);else if(ls==4)UnityEngine.Object.DestroyImmediate(leftOwner);
 if(mode==3)UnityEngine.Object.DestroyImmediate(right);else if(mode==4)rightOwner.SetActive(false);else if(mode==5)UnityEngine.Object.DestroyImmediate(rightOwner);
 var methods=new[]{"Equal","Unequal","LeftMissing","RightMissing","LeftAlive","RightAlive","ButtonEqual","LeftButtonAlive","RightButtonAlive","MixedLeft","MixedRight","LeftOwnerAlive","RightOwnerAlive","ViaReturn","ViaArguments","CallerPreserved"};
 var row=new List<int>{ls,mode};foreach(var name in methods)row.Add(Convert.ToInt32(type.GetMethod(name).Invoke(actor,null)));rows.Add("["+string.Join(",",row)+"]");
 GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();var post=new List<int>{ls,mode};foreach(var name in methods)post.Add(Convert.ToInt32(type.GetMethod(name).Invoke(actor,null)));postRows.Add("["+string.Join(",",post)+"]");
 }finally{UnityEngine.Object.DestroyImmediate(control);if(leftOwner)UnityEngine.Object.DestroyImmediate(leftOwner);if(rightOwner)UnityEngine.Object.DestroyImmediate(rightOwner);}
 }
 File.WriteAllText(Environment.GetEnvironmentVariable("EM_UI_OUTPUT"),"{\\"status\\":\\"PASS\\",\\"rows\\":["+string.Join(",",rows)+"],\\"afterGarbageCollectionRows\\":["+string.Join(",",postRows)+"]}");EditorApplication.Exit(0);
 }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}}
}
''')
runner=home/'runner';runner.mkdir(exist_ok=True)
(runner/'Runner.csproj').write_bytes(Path('artifacts/object-equality-check/runner/Runner.csproj').read_bytes())
(runner/'Program.cs').write_text('''using EngineMigrator.Core;
var home=HOME;var unity=UNITY;var core=CORE;var descriptor=Path.Combine(core,"Project/RuntimeWorld.uproject");
if(args.Length!=1)throw new ArgumentException("Explicit mode required");
if(args[0]is "prepare-legacy"or "prepare"or "verify-public"){
 var source=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/ui-equality.cs.txt");var files=args[0]=="prepare-legacy"?ActorScripts.FromUnity(source,false):ActorScripts.FromUnity(source);
 if(args[0]=="verify-public"){if(File.ReadAllText(Path.Combine(home,"AuditUIEquality.h"))!=files.Single().Code||File.ReadAllText(Path.Combine(core,"Project/Plugins/EMActorPort/Source/EMActorPort/Public/AuditUIEquality.h"))!=files.Single().Code||File.ReadAllText(Path.Combine(home,"restored.cs"))!=ActorScripts.ToUnity(files).Single().Code)throw new Exception("Public conversion differs from engine-tested fixture");Console.WriteLine("PUBLIC_COMPONENT_FORWARD_AND_REVERSE_MATCH_NO_ENGINE_STARTED");return;}
 var back=ActorScripts.ToUnity(files);File.WriteAllText(Path.Combine(home,"restored.cs"),back.Single().Code);File.WriteAllText(Path.Combine(home,"AuditUIEquality.h"),files.Single().Code);File.WriteAllText(Path.Combine(core,"Project/Plugins/EMActorPort/Source/EMActorPort/Public/AuditUIEquality.h"),files.Single().Code);Console.WriteLine("COMPONENT_HEADER_GENERATED_NO_ENGINE_STARTED");return;
}
if(args[0]=="build"){using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));await ResourcePorting.RunEditor(EngineLaunchSafety.Dotnet,[EngineLaunchSafety.Ubt,"RuntimeWorldEditor","Win64","Development","-Project="+descriptor,"-NoHotReload","-NoPCH","-NoSharedPCH","-log="+Path.Combine(home,"native-build.log")],core,timeout.Token);return;}
if(args[0]=="native"){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(180));await ResourcePorting.RunEditor(EngineLaunchSafety.Unreal,ResourcePorting.UnrealArgs(descriptor,Path.Combine(home,"native-probe.py"),Path.Combine(home,"native.log")),core,timeout.Token);return;}
if(args[0]is "unity-original"or "unity-restored"){
 var output=Path.Combine(home,args[0]+".json");if(File.Exists(output))throw new IOException("Fresh capture required");var settings=Path.Combine(unity,"ProjectSettings/EditorSettings.asset");var before=File.ReadAllBytes(settings);var priorClass=Environment.GetEnvironmentVariable("EM_UI_CLASS");var priorOutput=Environment.GetEnvironmentVariable("EM_UI_OUTPUT");
 Environment.SetEnvironmentVariable("EM_UI_CLASS",args[0]=="unity-original"?"AuditUIEquality":"RestoredAuditUIEquality");Environment.SetEnvironmentVariable("EM_UI_OUTPUT",output);
 try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(240));await ResourcePorting.RunEditor(EngineLaunchSafety.Unity,["-batchmode","-nographics","-projectPath",unity,"-executeMethod","EMUIEqualityProbe.Run","-logFile",Path.Combine(home,args[0]+".log")],Directory.GetParent(unity)!.FullName,timeout.Token);if(!File.Exists(output))throw new IOException("Component capture missing");}
 finally{File.WriteAllBytes(settings,before);Environment.SetEnvironmentVariable("EM_UI_CLASS",priorClass);Environment.SetEnvironmentVariable("EM_UI_OUTPUT",priorOutput);}return;
}
throw new ArgumentException("Unknown mode; refusing engine launch");
'''.replace('var home=HOME;var unity=UNITY;var core=CORE;', 'var home='+json.dumps(str(home))+';var unity='+json.dumps(str(unity))+';var core='+json.dumps(str(core))+';'))
(home/'native-probe.py').write_text('''import unreal,json,traceback
from pathlib import Path
output=Path(__file__).resolve().parent/'native-execution.json'
if output.exists():raise RuntimeError('Fresh capture required')
rows=[];post=[];completed=[]
try:
 unreal.EditorLoadingAndSavingUtils.new_blank_map(False);sub=unreal.get_editor_subsystem(unreal.EditorActorSubsystem);cls=unreal.load_class(None,'/Script/EMActorPort.AuditUIEquality')
 if cls is None:raise RuntimeError('Component class missing')
 def spawn(c):return sub.spawn_actor_from_class(c,unreal.Vector())
 methods=['equal','unequal','left_missing','right_missing','left_alive','right_alive','button_equal','left_button_alive','right_button_alive','mixed_left','mixed_right','left_owner_alive','right_owner_alive','via_return','via_arguments','caller_preserved']
 for ls in range(5):
  for mode in range(6):
   if ls==0 and mode==6:continue
   actor=spawn(cls);owners=[];deleted=set()
   def owner():
    value=spawn(unreal.StaticMeshActor);value.tags=[unreal.Name('EMUI:Canvas'),unreal.Name('EMUI:Button')];owners.append((value,value.get_path_name()));return value
   def component(root):
    value=spawn(cls)
    if not value.attach_to_actor(root,unreal.Name('None'),unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,False):raise RuntimeError('Component owner attachment failed')
    return value
   def remove_owner(value):
    deleted.add(value.get_path_name())
    for child in value.get_attached_actors():sub.destroy_actor(child)
    sub.destroy_actor(value)
   lo=owner()if ls else None;left=lo
   if mode==1:ro=lo;right=left
   elif mode==6:ro=lo;right=ro
   elif mode>=2:ro=owner();right=ro
   else:ro=None;right=None
   for field,value in [('left',left),('right',right),('left_button',lo),('right_button',ro),('left_owner',lo),('right_owner',ro)]:actor.set_editor_property(field,value)
   if ls==2:lo.tags=[t for t in lo.tags if str(t)!='EMUI:Canvas']
   elif ls==3:lo.tags=list(lo.tags)+[unreal.Name('EMInactiveSelf')]
   elif ls==4:remove_owner(lo)
   if mode==3:ro.tags=[t for t in ro.tags if str(t)!='EMUI:Canvas']
   elif mode==4:ro.tags=list(ro.tags)+[unreal.Name('EMInactiveSelf')]
   elif mode==5:remove_owner(ro)
   rows.append([ls,mode]+[int(getattr(actor,name)())for name in methods]);gc=unreal.EMGCReferenceProbe.collect_now();completed.append(gc)
   if not gc:raise RuntimeError('Synchronous GC completion missing')
   post.append([ls,mode]+[int(getattr(actor,name)())for name in methods]);sub.destroy_actor(actor)
   for value,key in owners:
    if key not in deleted:remove_owner(value)
 output.write_text(json.dumps({'status':'CAPTURED','rows':rows,'afterGarbageCollectionRows':post,'synchronousGCCompleted':completed}))
except Exception:output.write_text(json.dumps({'status':'FAIL','error':traceback.format_exc()}));raise
''')
print('COMPONENT_EQUALITY_PRIVATE_FIXTURES_PREPARED_NO_ENGINE_STARTED')
