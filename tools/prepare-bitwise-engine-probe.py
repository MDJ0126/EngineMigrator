"""Prepare engine fixtures only; this script does not start any process."""
from pathlib import Path
import json
import re

root=Path('artifacts/core-engine-launch-tests')
run=Path(json.loads((root/'paths.json').read_text())['run'])
fixtures=Path('artifacts/bitwise-statement-check')
for path in fixtures.glob('*.h'):
 (run/'Project/Plugins/EMActorPort/Source/EMActorPort/Public'/path.name).write_bytes(path.read_bytes())
probe=run/'probe.py'
text=probe.read_text().replace("OUTPUT=ROOT/'reference-native-feature-verification.json'", "OUTPUT=ROOT/'bitwise-native-feature-verification.json'")
block=''' a=spawn('BitwiseActor');target=spawn('BitTarget');driver=spawn('BitDriver');driver.set_editor_property('target',target)
 def i32(value):return ((value+2147483648)%4294967296)-2147483648
 rows=[]
 for value in [-2147483648,-2147483647,-17,-1,0,1,17,2147483647]:
  for count in [-2147483648,-65,-33,-32,-31,-1,0,1,2,30,31,32,33,63,64,2147483647]:
   n=count&31;compound=i32(i32((value|32)^17)<<n)>>n
   expected=[i32(value&count),i32(value|count),i32(value^count),i32(~value),i32(value<<n),value>>n,compound,compound,compound]
   actual=[call(a,name,value,count) if name!='Not' else call(a,name,value) for name in ['And','Or','Xor','Not','Left','Right','Local','FieldUpdate']]+[call(driver,'Run',value,count)]
   require(actual==expected,'Bitwise row differs: '+str([value,count,actual,expected]));rows.append([value,count]+actual)
 require(call(a,'Loop')==31,'Signed shift loop termination differs')
 checks['bitwise']={'rows':rows,'loop':31}
'''
if "checks['bitwise']" not in text:
 text=text.replace(" OUTPUT.write_text(json.dumps({'status':'PASS'",block+" OUTPUT.write_text(json.dumps({'status':'PASS'")
text=text.replace(' return getattr(actor,py)(*args)'," fn=getattr(actor,py,None) or getattr(actor,py+'_',None)\n require(fn is not None,'Native method missing: '+py)\n return fn(*args)")
probe.write_text(text)
unity=Path('artifacts/runtime-ui-api-tests/UnitySource')
folder=unity/'Assets/EngineMigratorBitwiseProbe';folder.mkdir(exist_ok=True)
for restored in [False,True]:
 code=(fixtures/('restored.cs' if restored else 'original.cs')).read_text()
 if restored:
  for name in ['AuditBitwiseActor','AuditBitTarget','AuditBitDriver']:code=re.sub(r'\b'+name+r'\b','Restored'+name,code)
 for part in re.split(r'(?=public class )',code):
  match=re.match(r'public class (\w+)',part)
  if match:(folder/(match[1]+'.cs')).write_text('using UnityEngine;\n'+part)
editor=folder/'Editor';editor.mkdir(exist_ok=True)
(editor/'EMBitwiseProbe.cs').write_text('''using System;using System.IO;using System.Collections.Generic;using System.Linq;using UnityEngine;
public static class EMBitwiseProbe {
 public static void Run(){
  var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../bitwise-unity-execution.json"));
  if(File.Exists(output))throw new IOException("Fresh capture required");
  var all=new List<string>();var loops=new List<int>();
  foreach(var prefix in new[]{"","Restored"}){
   var game=new GameObject("EMBitwiseProbe");
   try{
    var asm=typeof(AuditBitwiseActor).Assembly;var type=asm.GetType(prefix+"AuditBitwiseActor");var actor=game.AddComponent(type);
    var target=game.AddComponent(asm.GetType(prefix+"AuditBitTarget"));var driverType=asm.GetType(prefix+"AuditBitDriver");var driver=game.AddComponent(driverType);driverType.GetField("Target").SetValue(driver,target);
    var rows=new List<string>();
    foreach(var value in new[]{int.MinValue,int.MinValue+1,-17,-1,0,1,17,int.MaxValue})foreach(var count in new[]{int.MinValue,-65,-33,-32,-31,-1,0,1,2,30,31,32,33,63,64,int.MaxValue}){
     var row=new List<int>{value,count};
     foreach(var method in new[]{"And","Or","Xor","Not","Left","Right","Local","FieldUpdate"})row.Add((int)type.GetMethod(method).Invoke(actor,method=="Not"?new object[]{value}:new object[]{value,count}));
     row.Add((int)driverType.GetMethod("Run").Invoke(driver,new object[]{value,count}));rows.Add("["+string.Join(",",row)+"]");
    }
    all.Add("["+string.Join(",",rows)+"]");loops.Add((int)type.GetMethod("Loop").Invoke(actor,null));
   }finally{UnityEngine.Object.DestroyImmediate(game);}
  }
  if(all[0]!=all[1]||loops.Any(v=>v!=31))throw new Exception("Unity source/restored mismatch");
  File.WriteAllText(output,"{\\"status\\":\\"PASS\\",\\"runs\\":["+string.Join(",",all)+"],\\"loops\\":["+string.Join(",",loops)+"]}");Debug.Log("BITWISE_ACTUAL_UNITY_PASS");
 }
}
''')
program=root/'runner/Program.cs';text=program.read_text()
if 'args[1]=="bitunity"' not in text:
 text=text.replace('if(args[1]=="unity"){','''if(args[1]=="bitunity"){
 var work=Path.GetFullPath("artifacts/runtime-ui-api-tests");var source=Path.Combine(work,"UnitySource");
 await ResourcePorting.RunEditor(EngineLaunchSafety.Unity,["-batchmode","-nographics","-quit","-projectPath",source,"-executeMethod","EMBitwiseProbe.Run","-logFile",Path.Combine(work,"bitwise-unity-execution.log")],work,CancellationToken.None);
 Console.WriteLine("BITWISE_ACTUAL_UNITY_EXECUTED");return;
}
if(args[1]=="unity"){
''')
 program.write_text(text)
print('BITWISE_FIXTURES_PREPARED_NO_ENGINE_LAUNCH')
