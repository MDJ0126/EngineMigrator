"""Prepare real active/dormant prefab asset tests on the audited private projects."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parent.parent;h=root/'artifacts/scaled-prefab-check';h.mkdir(exist_ok=True);unity=root/'artifacts/runtime-ui-api-tests/UnitySource';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
f=unity/'Assets/EngineMigratorScaledPrefabProbe';(f/'Editor').mkdir(parents=True,exist_ok=True)
source=(root/'tests/EngineMigrator.Checks/Fixtures/scaled-prefab.cs.txt').read_text();a,b=source.split('public class ScaledPrefabWorker',1);(f/'ScaledPrefabDriver.cs').write_text(a);(f/'ScaledPrefabWorker.cs').write_text('using UnityEngine;\npublic class ScaledPrefabWorker'+b)
for name in ['ScaledPrefabDriver','ScaledPrefabWorker']:
 p=h/'restored'/(name+'.cs')
 if p.exists():
  s=p.read_text()
  for n in ['ScaledPrefabDriver','ScaledPrefabWorker']:s=re.sub(r'\b'+n+r'\b','Restored'+n,s)
  (f/('Restored'+name+'.cs')).write_text(s)
(f/'Editor/EMScaledPrefabProbe.cs').write_bytes((root/'tools/scaled-prefab-unity-probe.cs.txt').read_bytes())
r=h/'runner';r.mkdir(exist_ok=True);(r/'Runner.csproj').write_bytes((root/'artifacts/scaled-travel-check/runner/Runner.csproj').read_bytes())
s=(root/'artifacts/scaled-travel-check/runner/Program.cs').read_text();start=s.index('var files=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/scaled-travel.cs.txt"))');end=s.index('return;}',start)
s=s[:start]+'''var files=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/scaled-prefab.cs.txt"));foreach(var f in files){File.WriteAllText(Path.Combine(home,f.Name),f.Code);File.WriteAllText(Path.Combine(core,"Project/Plugins/EMActorPort/Source/EMActorPort/Public",f.Name),f.Code);}Directory.CreateDirectory(Path.Combine(home,"restored"));foreach(var f in ActorScripts.ToUnity(files))File.WriteAllText(Path.Combine(home,"restored",f.Name),f.Code);File.WriteAllText(Path.Combine(core,"Project/Source/EMPortHost/EMPortHost.cpp"),File.ReadAllText("artifacts/ui-equality-check/core-host-before-ui-destruction.cpp.txt")+File.ReadAllText("tools/fixed-update-game-observer.cpp.txt")+File.ReadAllText("tools/fixed-travel-game-observer.cpp.txt")+File.ReadAllText("tools/time-scale-game-observer.cpp.txt")+File.ReadAllText("tools/scaled-travel-game-observer.cpp.txt")+File.ReadAllText("tools/initial-timing-game-observer.cpp.txt")+File.ReadAllText("tools/scaled-prefab-game-observer.cpp.txt"));'''+s[end:]
s=s.replace('.Concat(ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/fixed-travel.cs.txt"))))','.Concat(ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/fixed-travel.cs.txt"))).Concat(ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/scaled-travel.cs.txt"))))')
s=s.replace(str(root/'artifacts/scaled-travel-check').replace('\\','\\\\'),str(h).replace('\\','\\\\'))
s=s.replace('EMScaledTravelProbe.Run','EMScaledPrefabProbe.Run').replace('EM_SCALED_TRAVEL_','EM_SCALED_PREFAB_')
s=s.replace('"/Game/ScaledTravel/ScaledStart"','"/Game/ScaledPrefab/Start"').replace('"-EMScaledTravelProbe="','"-EMScaledPrefabProbe="')
s=s.replace('tools\\\\prepare-scaled-travel-maps.py','tools\\\\prepare-scaled-prefab-map.py')
s=s.replace('var keys=new[]{"EM_SCALED_PREFAB_OUTPUT"','var keys=new[]{"EM_SCALED_PREFAB_EXPORT_ROOT","EM_SCALED_PREFAB_OUTPUT"')
s=s.replace('var values=new[]{output,','var values=new[]{Path.Combine(home,"Transfer"),output,')
# The standalone fixture starts at .5 by default; alternate zero remains available.
s=s.replace('mode.EndsWith("zero",StringComparison.Ordinal)?"0":"1",mode.StartsWith','mode.EndsWith("zero",StringComparison.Ordinal)?"0":"0.5",mode.StartsWith')
s=s.replace('args[0].StartsWith(\"mapszero\",StringComparison.Ordinal)?\"0\":\"1\"','args[0].StartsWith(\"mapszero\",StringComparison.Ordinal)?\"0\":\"0.5\"')
s=s.replace('else if(mode=="rate")await','else if(mode=="scaled")await ResourcePorting.RunEditor(EngineLaunchSafety.Unreal,[descriptor,"/Game/ScaledTravel/ScaledStart","-game","-nullrhi","-RenderOffscreen","-nosound","-unattended","-nop4","-nosplash","-UseFixedTimeStep","-FPS="+fps,"-EMScaledTravelProbe="+output,"-abslog="+log],core,timeout.Token);else if(mode=="rate")await')
(r/'Program.cs').write_text(s);print('SCALED_PREFAB_FIXTURES_READY_NO_ENGINE_STARTED')
