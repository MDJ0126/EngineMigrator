"""Gate the real 53-script project pipeline, observed runtime, saved resources and scoped receipts."""
from pathlib import Path
import json,math,importlib.util
from object_equality_evidence import require,sha,validate_receipt
r=Path(__file__).resolve().parent.parent;h=r/'artifacts/integrated-project-check'
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def module(name,file):
 s=importlib.util.spec_from_file_location(name,r/'tools'/file);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
prefab=module('integrated_prefab','compare-runtime-prefab.py');forward=load(h/'forward-result.json');reverse=load(h/'reverse-result.json');f=Path(forward['RunDirectory']);v=Path(reverse['RunDirectory']);bindings={}
def bind(key,kind,log,capture=None,method='EMRuntimeWorldProbe.Run'):
 found=[]
 for rp in h.rglob('engine-preflight-*.json'):
  d=load(rp);args=d.get('invocation',{}).get('arguments',[]);actual=None
  if '-logFile'in args and args.index('-logFile')+1<len(args):actual=args[args.index('-logFile')+1]
  for arg in args:
   if arg.startswith(('-log=','-abslog=')):actual=arg.split('=',1)[1]
  if not actual or Path(actual).resolve()!=Path(log).resolve():continue
  try:validate_receipt(d,kind,actual,capture,r,unity_method=method)
  except(RuntimeError,ValueError,TypeError,KeyError,IndexError):continue
  found.append((d['finishedUtc'],rp))
 require(bool(found),'Successful scoped execution missing '+key);rp=sorted(found)[-1][1];bindings[key]={'receipt':str(rp),'sha256':sha(rp),'captureSha256':sha(capture)if capture else None}
require(forward['ProjectPortComplete']is False and reverse['ProjectPortComplete']is False,'Unsupported whole-game completion claim')
for name,result in [('forward',forward),('reverse',reverse)]:
 d=load(Path(result['VerificationFile']));require(d['savedAndReloaded']is True and d['referencesVerified']is True,'Actual project import validation failed '+name)
units=load(f/'Transfer/project-package.json')['units'];require(len(units)==45 and sum(u['kind']=='scene'for u in units)==20 and sum(u['kind']=='prefab'for u in units)==25,'Original 45-unit project scope changed')
require(load(h/'resource-comparison.json')['status']=='PASS','Actual restored project reexport resources differ')
require(load(h/'source-verification.json')['status']=='CURRENT_53_HEADERS_AND_RESTORED_CLASSES_MATCH','Actual source verification missing')
source=load(h/'source-verification.json');require(source['classes']==53,'53-script project scope changed')
for p,digest in source['files'].items():require(sha(Path(p)).lower()==digest.lower(),'Current native/restored code or binary changed '+p)
require(source['coreSha256'].lower()==sha(r/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'),'Engine generator differs from current checked core')
summaries=[];phase=[];counts={}
for name,kind in [('original-final','Unity'),('native','Unreal'),('restored','Unity')]:
 p=h/(name+'.json');d=load(p);require(d.get('status')=='CAPTURED'and d.get('gcCompleted')is True,'Project runtime/GC capture incomplete '+name);summaries.append(prefab.summarize(p));counts[name]=len(d['frames']);rows={}
 for frame in d['frames']:
  row=frame.get('phase')
  if not row or row[0]==0:continue
  require(type(row)is list and len(row)==47 and all(type(x)in(int,float)and math.isfinite(x)and x==int(x)for x in row),'Malformed integrated phase state '+name)
  require(row[0]not in rows,'Duplicate integrated phase frame '+name);rows[row[0]]=row
 require(sorted(rows)==list(range(1,41)),'40 integrated lifecycle frames missing '+name);phase.append(rows);bind(name,kind,h/(name+'.log'),p)
 # Reuse the actual identity/GC checkpoints from the earlier independent whole-project verifier.
 normalized=prefab.world.load(p);seen=set()
 for frame in normalized['frames']:
  for actor in frame['actors']:
   if actor['class']!='RuntimeSpawner':continue
   state=actor['state'];tick=state['Frame'];seen.add(tick)
   for field,threshold in [('AliasBeforeDestroy',10),('AliasAfterDestroy',22),('DifferentBeforeGC',22),('AliasAfterGC',28),('DifferentAfterGC',28),('OldAliasDiffersFromNew',32),('TransformBeforeGC',22),('TransformAfterGC',28),('TransformAliasAfterGC',28),('TransformOldDiffersFromNew',32)]:require(type(state.get(field))is bool and state[field]is(tick>=threshold),'GC/object identity mismatch '+name+'/'+field)
   if kind=='Unreal'and tick>=28:require(frame.get('gcCompleted')is True,'Native GC not completed at identity checkpoint')
 require({10,18,20,22,24,28,30,32,38}.issubset(seen),'GC identity frames missing '+name)
require(summaries[0]==summaries[1]==summaries[2],'Integrated prefab/hierarchy/scene sequence differs')
require(phase[0]==phase[1]==phase[2],'Integrated lifecycle state or callback order differs')
bind('prepare','Unity',h/'prepare.log',h/'prepared.txt','EMIntegratedPrepare.Run');bind('project-export','Unity',f/'unity-project-export.log',f/'Transfer/preflight.json','EngineMigratorBridge.Run');bind('actor-build','Compiler',f/'actor-build.log');bind('ui-build','Compiler',f/'ui-build.log');bind('project-import','Unreal',f/'unreal-project-import.log',f/'Transfer/unreal-import-result.json');bind('project-native-export','Unreal',v/'unreal-project-export.log',v/'Transfer/project-progress.json');bind('project-unity-import','Unity',v/'unity-project-import.log',v/'Transfer/unity-import-result.json','EngineMigratorBridge.Run');bind('restored-reexport','Unity',h/'reexport.log',h/'Reexport/project-package.json','EngineMigratorBridge.Run');bind('legacy','Unreal',h/'legacy.log',h/'Legacy/scene-execution.json')
require(load(h/'Legacy/verification.json')['status']=='PASS','Fresh 250-frame legacy regression failed')
original=load(r/'artifacts/user-project-baseline/original-hashes.json');require(len(original)==60,'Original baseline scope changed')
for p,digest in original.items():require(sha(Path(p)).lower()==digest.lower(),'User original modified '+p)
result={'status':'ACTUAL_53_SCRIPT_PROJECT_PIPELINE_ROUNDTRIP_PASS','goalComplete':False,'projectUnits':45,'scenes':20,'prefabs':25,'scriptClasses':53,'runtimeFrameCounts':counts,'integratedLifecycleFrames':40,'integratedLifecycleValues':47,'prefabCheckpoints':8,'sceneExecutionOrder':summaries[0]['sceneExecutionOrder'],'garbageCollectionVerified':True,'freshLegacyFrames':250,'originalFilesUnchanged':60,'bindings':bindings,'renderingEquivalent':False,'physicsTrajectoryEquivalent':False};(h/'verification.json').write_text(json.dumps(result,indent=2));print(result['status'])
