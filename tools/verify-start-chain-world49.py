"""Check real private-project roundtrip and GC state without launching engines."""
from pathlib import Path
import importlib.util,json
from object_equality_evidence import require,sha,validate_receipt
require(__debug__,'Project semantic comparisons require Python assertions enabled')
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/start-chain-check/world49'
work=root/'artifacts/start-chain-check/world49'
unity=root/'artifacts/runtime-ui-api-tests'
spec=importlib.util.spec_from_file_location('component_prefab',root/'tools/compare-runtime-prefab.py');prefab=importlib.util.module_from_spec(spec);spec.loader.exec_module(prefab)

def receipt_for(kind,log,capture=None):
 matches=[]
 for folder in (work,unity):
  for path in folder.glob('engine-preflight-*.json'):
   data=json.loads(path.read_text(encoding='utf-8-sig'))
   try:validate_receipt(data,kind,home/log,capture,root,unity_method='EMRuntimeWorldProbe.Run')
   except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
   matches.append((data['finishedUtc'],path))
 require(bool(matches),'Audited successful receipt missing: '+log)
 return sorted(matches)[-1][1]

summaries=[];bindings={};counts={}
for name,kind in [('original','Unity'),('native','Unreal'),('restored','Unity')]:
 path=home/('world-'+name+'.json');data=prefab.world.load(path)
 require(data['status']=='CAPTURED'and data.get('gcCompleted')is True,'Actual project GC capture incomplete: '+name)
 summaries.append(prefab.summarize(path));counts[name]=len(data['frames']);seen=set()
 for frame in data['frames']:
  for actor in frame['actors']:
   if actor['class']!='RuntimeSpawner':continue
   state=actor['state'];tick=state['Frame'];seen.add(tick)
   for field,threshold in [('AliasBeforeDestroy',10),('AliasAfterDestroy',22),('DifferentBeforeGC',22),('AliasAfterGC',28),('DifferentAfterGC',28),('OldAliasDiffersFromNew',32),('TransformBeforeGC',22),('TransformAfterGC',28),('TransformAliasAfterGC',28),('TransformOldDiffersFromNew',32)]:
    require(type(state.get(field))is bool and state[field]is(tick>=threshold),'Runtime identity mismatch: '+name+'/'+field)
   if kind=='Unreal'and tick>=28:require(frame.get('gcCompleted')is True,'Native world GC not completed at checkpoint')
 require({10,18,20,22,24,28,30,32,38}.issubset(seen),'Runtime identity frames missing')
 receipt=receipt_for(kind,'world-'+name+'.log',path)
 bindings[name]={'capture':str(path),'sha256':sha(path),'receipt':str(receipt),'receiptSha256':sha(receipt)}
require(summaries[0]==summaries[1]==summaries[2],'Prefab lifecycle, reference, hierarchy or scene sequence differs')
headers=[p for p in(work/'Project/Plugins/EMActorPort/Source/EMActorPort/Public').glob('*.h')if p.read_text().startswith('// EM_ACTOR ')]
require(len(headers)==len(list((home/'world-restored-scripts').glob('*.cs')))==49,'49-script restoration missing')
require(all(json.loads(p.read_text().splitlines()[0][12:]).get('weakUI')is True for p in headers),'Mixed component backend in candidate project')
require(all(json.loads(p.read_text().splitlines()[0][12:]).get('startQueue')==1 for p in headers),'No-Late startup metadata missing')
for p,d in json.loads((home/'build-hashes.json').read_text(encoding='utf-8-sig')).items():require(sha(Path(p)).lower()==d.lower(),'49-project compiled source/binary changed '+p)
require(sha(home.parent/'world49-runner/bin/Release/net9.0/EngineMigrator.Core.dll')==sha(root/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'),'49-project core differs from tested core')
compiler=receipt_for('Compiler','world-build.log')
source=json.loads((home/'executed-source-verification.json').read_text(encoding='utf-8-sig'))
require(source['status']=='ACTUAL_49_UNITY_CLASSES_MATCH_CURRENT_NATIVE_GENERATION'and source['classes']==49,'Executed original source identity missing')
require(source['coreSha256']==sha(root/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'),'Executed source generator differs from checked core')
for p,d in source['files'].items():require(sha(Path(p))==d,'Original/override source changed '+p)
restored_inputs=json.loads((home/'world-restored.source-inputs.json').read_text(encoding='utf-8-sig'))
require(restored_inputs['allRestoredClasses']==49 and len(restored_inputs['files'])==49,'Restored execution must replace all 49 classes')
for name,path in source['classFiles'].items():require(restored_inputs['files'][path].lower()==sha(home/'world-restored-scripts'/(name+'.cs')),'Actual restored class differs '+name)
original_inputs=json.loads((home/'world-original.source-inputs.json').read_text(encoding='utf-8-sig'))
require(len(original_inputs['files'])==1 and next(iter(original_inputs['files'].values())).lower()==sha(home/'runtime-spawner-original.cs.txt'),'Original RuntimeSpawner override differs')
record={'status':'ACTUAL_UI_REFERENCE_PROJECT_ROUNDTRIP_PASSED','goalComplete':False,'headers':49,'worldFrameCounts':counts,'prefabCheckpoints':8,'sceneExecutionOrder':summaries[0]['sceneExecutionOrder'],'bindings':bindings,'compilerReceipt':str(compiler),'compilerReceiptSha256':sha(compiler),'headerHashes':{p.name:sha(p)for p in headers},'physicsTrajectoryEquivalent':False,'renderingEquivalent':False}
(home/'project-verification.json').write_text(json.dumps(record,indent=2));print(json.dumps(record))

