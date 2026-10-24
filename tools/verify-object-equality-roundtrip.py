"""Verify actual GC fix, fresh engine provenance, full-project runtime and source restoration."""
from pathlib import Path
from datetime import datetime
import hashlib,importlib.util,json
from object_equality_oracle import require_rows,require_flows,expected_rows
from object_equality_evidence import verify,validate_receipt

root=Path(__file__).resolve().parent.parent;home=root/'artifacts/object-equality-check'
core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
work=root/'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da'
unity_work=root/'artifacts/runtime-ui-api-tests'
def require(condition,message):
 if not condition:raise RuntimeError(message)
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def load(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def receipt_for(kind,log,capture=None,world=False):
 matches=[]
 for folder in (core,work,unity_work):
  for path in folder.glob('engine-preflight-*.json'):
   data=load(path)
   try:validate_receipt(data,kind,home/log,capture,root,unity_method='EMRuntimeWorldProbe.Run'if world else 'EMObjectEqualityProbe.Run')
   except (RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
   matches.append((data['finishedUtc'],path))
 require(bool(matches),'No matching successful audited receipt: '+log)
 return sorted(matches)[-1][1]

require(__debug__,'Assertions must remain enabled')
for rows in load(home/'managed-rows.json'):require_rows(rows,'managed source/restored')
for rows in load(home/'managed-flow-rows.json'):require_flows(rows,'managed flows')
baseline=load(home/'native-before-fix.json');require_rows(baseline['rows'],'actual pre-fix before GC')
require(len(baseline.get('synchronousGCCompleted',[]))==20 and all(v is True for v in baseline['synchronousGCCompleted']),'Pre-fix actual GC completion missing')
differences=[{'case':want[:2],'columns':[i for i,(a,b)in enumerate(zip(want,got))if a!=b]}for want,got in zip(expected_rows(),baseline['afterGarbageCollectionRows'])if want!=got]
require(differences==[{'case':[2,3],'columns':[2,3,8,9]}],'Actual pre-fix failure no longer matches recorded cause')
capture_receipts={}
for name,kind,log in [('unity-original','Unity','unity-original.log'),('native-execution','Unreal','native.log'),('unity-restored','Unity','unity-restored.log')]:
 path=home/(name+'.json');data=load(path);require(data['status']in ('PASS','CAPTURED'),'Actual capture failed: '+name)
 require_rows(data['rows'],name);require_rows(data['afterGarbageCollectionRows'],name+' after GC');require_flows(data['identityFlows'],name)
 if kind=='Unreal':require(len(data.get('synchronousGCCompleted',[]))==20 and all(v is True for v in data['synchronousGCCompleted']),'Synchronous GC completion missing')
 receipt=receipt_for(kind,log,path);capture_receipts[name]={'captureSha256':sha(path),'receipt':str(receipt),'receiptSha256':sha(receipt)}

spec=importlib.util.spec_from_file_location('prefab',root/'tools/compare-runtime-prefab.py');prefab=importlib.util.module_from_spec(spec);spec.loader.exec_module(prefab)
summaries=[];world_captures={};frame_counts={}
for name,kind in [('original','Unity'),('native','Unreal'),('restored','Unity')]:
 path=home/('world-'+name+'.json');data=prefab.world.load(path);require(data['status']=='CAPTURED'and data.get('gcCompleted')is True,'Actual world/GC capture incomplete: '+name)
 summary=prefab.summarize(path);summaries.append(summary);frame_counts[name]=len(data['frames']);seen=set()
 for frame in data['frames']:
  for actor in frame['actors']:
   if actor['class']!='RuntimeSpawner':continue
   state=actor['state'];tick=state['Frame'];seen.add(tick)
   for field,threshold in [('AliasBeforeDestroy',10),('AliasAfterDestroy',22),('DifferentBeforeGC',22),('AliasAfterGC',28),('DifferentAfterGC',28),('OldAliasDiffersFromNew',32)]:
    require(type(state.get(field))is bool and state[field]is(tick>=threshold),'World identity mismatch: '+name+'/'+field+'/'+str(tick))
   if kind=='Unreal'and tick>=28:require(frame.get('gcCompleted')is True,'World GC completed too late for identity checkpoint')
 require({10,18,20,22,24,28,30,32,38}.issubset(seen),'Required identity/lifecycle frames missing: '+name)
 receipt=receipt_for(kind,'world-'+name+'.log',path,world=True);world_captures[name]={'path':str(path),'sha256':sha(path),'receipt':str(receipt),'receiptSha256':sha(receipt),'log':str(home/('world-'+name+'.log'))}
require(summaries[0]==summaries[1]==summaries[2],'Prefab lifecycle/reference/scene sequence differs')
target=work/'Target/Plugins/EMActorPort/Source/EMActorPort/Public'
headers=[p for p in target.glob('*.h')if p.read_text().startswith('// EM_ACTOR ')]
restored=list((home/'world-restored-scripts').glob('*.cs'))
require(len(headers)==len(restored)==49,'Full 49-script reverse incomplete')
require(all(json.loads(p.read_text().splitlines()[0].removeprefix('// EM_ACTOR ')).get('weakGameObjects')is True for p in headers),'Project contains unconverted strong GameObject headers')
require(sha(home/'runtime-spawner-restored.cs.txt')==sha(home/'world-restored-scripts/RuntimeSpawner.cs'),'Executed restored runtime differs from project reverse output')
require(sha(home/'AuditObjectEquality.h')==sha(core/'Project/Plugins/EMActorPort/Source/EMActorPort/Public/AuditObjectEquality.h'),'Native standalone header differs from current generator')
source_paths=[root/'src/EngineMigrator.Core/ActorScripts.cs',root/'src/EngineMigrator.Core/ResourcePorting.cs',root/'src/EngineMigrator.Core/WeakGameObjectPorting.cs',root/'tests/EngineMigrator.Checks/Fixtures/object-equality.cs.txt',home/'AuditObjectEquality.h',home/'restored.cs',home/'native-probe.py',home/'RuntimeWorldGCProbe.cpp',home/'runtime-spawner-original.cs.txt',home/'runtime-spawner-restored.cs.txt']+headers
record={'status':'PASS','sceneTravelVerified':True,'creationDestructionVerified':True,'sourceHashes':{str(p.relative_to(root)).replace('\\','/'):sha(p)for p in source_paths},'captureReceipts':capture_receipts,'worldCaptures':world_captures,'compilerReceipt':str(receipt_for('Compiler','native-build.log')),'worldCompilerReceipt':str(receipt_for('Compiler','world-build.log')),'actualBeforeFixMismatch':differences,'worldFrameCounts':frame_counts,'fullRestoredScripts':49,'goalComplete':False}
reflected_baseline=load(home/'native-reflected-before-fix.json')
reflected_mismatch=[r[:2]for r in reflected_baseline['identityFlows']if r[6]!=next(e[2]for e in expected_rows()if e[:2]==r[:2])]
require(reflected_mismatch==[[2,3]],'Recorded reflected argument failure differs from expected cause')
gen=next((core/'Project/Plugins/EMActorPort/Intermediate').rglob('AuditObjectEquality.gen.cpp'))
require(all('P_GET_WEAKOBJECT_REF_NO_PTR(TWeakObjectPtr<AActor>,Z_Param_Out_EM_in_'+name+')'in gen.read_text()for name in ('a','b')),'UHT did not generate a weak-reference-preserving input thunk')
record['reflectedArgumentBeforeFixMismatch']=reflected_mismatch;record['generatedNativeThunkSha256']=sha(gen)
(home/'project-integration-verification.json').write_text(json.dumps(record,indent=2))
proof=verify(root,home)
result={'status':'ACTUAL_GC_REFERENCE_ROUNDTRIP_PASS','beforeFixActualFailure':differences,'reflectedArgumentBeforeFixMismatch':reflected_mismatch,'actualComparisonsPerCapture':20,'actualIdentityFlowsPerCapture':20,'nativeSynchronousGCCycles':20,'worldFrameCounts':frame_counts,'worldCheckpoints':summaries[0],'fullRestoredScripts':49,'originalFilesUnchanged':60,'provenanceGate':proof,'goalComplete':False}
(home/'actual-roundtrip-verification.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items()if k!='worldCheckpoints'}))
