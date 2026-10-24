"""Check scaled lifecycle plus actual initial-timing adapter roundtrips."""
from pathlib import Path
import json,math,re
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;h=root/'artifacts/scaled-travel-check';unity=root/'artifacts/runtime-ui-api-tests';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
bindings={};floating={9,10,25,26,38,39,51,52}
def bind(name,kind,folder,capture=True,method='EMScaledTravelProbe.Run'):
 matches=[];p=h/(name+'.json')if capture else None
 for rp in folder.glob('engine-preflight-*.json'):
  r=load(rp)
  try:validate_receipt(r,kind,h/(name+'.log'),p,root,unity_method=method)
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  matches.append((r['finishedUtc'],rp))
 require(bool(matches),'Scoped receipt missing '+name);rp=sorted(matches)[-1][1];bindings[name]={'receipt':str(rp),'receiptSha256':sha(rp),'captureSha256':sha(p)if p else None}
def rows(name,initial):
 d=load(h/(name+'.json'));r=d.get('rows');expect=[(s,f)for s,n in [(0,12),(1,8),(2,12)]for f in range(1,n+1)]
 require(d.get('status')=='CAPTURED'and type(r)is list and len(r)==32,'Incomplete lifecycle capture '+name)
 for a,key in zip(r,expect):
  require(type(a)is list and len(a)==54 and tuple(a[:2])==key and all(type(x)in(int,float)and math.isfinite(x)for x in a),'Invalid lifecycle row '+name)
  require(all(x==int(x)for j,x in enumerate(a)if j not in floating and j not in{6,7,8,22,23,24,35,36,37,48,49,50}),'Invalid discrete state '+name)
  require(a[2:4]==[1,1],'Scene startup count differs '+name)
 require(r[0][6:9]==[initial,initial,initial]and r[12][6:9]==[2,2,2]and r[20][6:9]==[.5,.5,.5],'Initial or travelling rate lost '+name)
 require(r[6][41:45]==[1,1,1,1] and r[6][48]==0,'Paused creation/start not observed '+name)
 require(r[5][5]==1 and r[5][13]==0 and r[7][5]==2 and r[7][12]==0,'Paused destruction/reference invalidation missing '+name)
 require(r[3][11]==1 and r[15][11]==1 and r[23][11]==1,'Clone instance distinction missing '+name)
 return r
cases={}
for suffix,case,initial in [('', '100-20',1),('','30-20',1),('half','100-20',.5),('zero','100-20',0)]:
 modes=['original'+suffix,'native'+suffix,'restored'+suffix];data=[rows(m+'-'+case,initial)for m in modes]
 for mode,r in zip(modes,data):
  for i,(a,b)in enumerate(zip(data[0],r)):
   for j,(x,y)in enumerate(zip(a,b)):require(abs(x-y)<=(2e-6 if j in floating else 0),f'{mode}-{case} stage/frame={a[:2]} column={j}: {x}/{y}')
  bind(mode+'-'+case,'Unreal'if mode.startswith('native')else'Unity',core if mode.startswith('native')else unity)
 cases[(suffix or'one')+'-'+case]={'initialTimeScale':initial,'frames':32,'valuesPerFrame':54}
for suffix,initial in [('half',.5),('zero',0)]:
 export=h/('exported-timing-'+str(float(initial))+'.json');require(load(export)['timeScale']==initial,'Actual exported initial timing missing')
 for mode in ['originalonly'+suffix,'restoredonly'+suffix,'only'+suffix]:
  name=mode+'-100-20';d=load(h/(name+'.json'));r=d.get('rows');require(d.get('status')=='CAPTURED'and d.get('userBehaviours')==0 and type(r)is list and len(r)==5,'Empty timing capture missing '+name)
  for i,a in enumerate(r,1):require(len(a)==3 and a[0]==i and abs(a[1]-.01*initial)<=2e-6 and a[2]==initial,'Empty world timing differs '+name)
  bind(name,'Unreal'if mode.startswith('only')else'Unity',core if mode.startswith('only')else unity,method='EMInitialTimingProbe.Run')
  if not mode.startswith('only'):
   proof=load(h/(name+'.json.validation.json'));require(proof=={'status':'PASS','invalidInputsRejectedBeforeSettingsMutation':6},'Unity invalid timing mutation guard missing')
 for mode in ['restored'+suffix,'restoredonly'+suffix]:
  name=mode+'-100-20';used=h/(name+'.json.export-used.json');require(sha(used)==sha(export),'Restored Unity did not consume actual Unreal export '+name)
  text=(h/(name+'.json.timing.txt')).read_text(encoding='utf-8-sig');match=re.search(r'^  m_TimeScale: (.*)$',text,re.M);require(match and float(match.group(1))==initial,'TimeManager initial scale not written '+name)
 map_proof=load(h/('timing-map-'+str(float(initial))+'.json'));require(map_proof.get('status')=='PASS'and map_proof.get('mapsReloaded')==4 and map_proof.get('invalidInputsRejectedBeforeSettingsMutation')==6,'Actual adapter persistence/validation missing')
 bind('maps'+suffix+'-100-20','Unreal',core,False)
bind('build','Compiler',core,False)
for p,d in load(h/'build-hashes.json').items():require(sha(Path(p)).lower()==d.lower(),'Native build changed '+p)
f=unity/'UnitySource/Assets/EngineMigratorScaledTravelProbe'
for name in ['ScaledTravel','ScaledWorker']:
 text=(h/'restored'/(name+'.cs')).read_text()
 for n in ['ScaledTravel','ScaledWorker']:text=re.sub(r'\b'+n+r'\b','Restored'+n,text)
 require((f/('Restored'+name+'.cs')).read_text()==text,'Actual restored script changed '+name)
source=(root/'tests/EngineMigrator.Checks/Fixtures/scaled-travel.cs.txt').read_text(encoding='utf-8-sig');left,right=source.split('public class ScaledWorker',1)
require((f/'ScaledTravel.cs').read_text()==left and (f/'ScaledWorker.cs').read_text()=='using UnityEngine;\npublic class ScaledWorker'+right,'Original engine fixture changed')
require(sha(h/'bootstrap-module.cpp')==sha(core/'Project/Plugins/EMActorPort/Source/EMActorPort/Private/EMActorPort.cpp'),'Actual native bootstrap differs from generated source')
require(sha(root/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll')==sha(h/'runner/bin/Release/net9.0/EngineMigrator.Core.dll'),'Engine generation core differs from managed-test core')
require(sha(unity/'UnitySource/Assets/Editor/TimingBridge.cs')==sha(root/'adapters/unity/TimingBridge.cs'),'Actual Unity timing adapter differs from distribution')
record={'status':'ACTUAL_SCALED_LIFECYCLE_AND_INITIAL_TIMING_PASS','goalComplete':False,'lifecycleScenarios':cases,'lifecycleCaptures':12,'emptyTimingCaptures':6,'emptyTimingFrames':5,'initialTimeScales':[1,.5,0],'actualUnrealExportConsumedByUnity':True,'invalidTimingCasesPerAdapter':6,'bindings':bindings,'fullPipelineEndToEndProjectRerun':False,'physicsTrajectoryEquivalence':False}
(h/'verification.json').write_text(json.dumps(record,indent=2));print(record['status'])
