"""Compare object-validity conversions and real prefab/world-travel execution."""
from pathlib import Path
import hashlib
import importlib.util
import json

if not __debug__:raise RuntimeError('Assertions must remain enabled')
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/implicit-truth-check'
spec=importlib.util.spec_from_file_location('prefab',root/'tools/compare-runtime-prefab.py')
prefab=importlib.util.module_from_spec(spec);spec.loader.exec_module(prefab)
output=home/'roundtrip-verification.json'

def require(value,message):
 if not value:raise RuntimeError(message)

def sha(path):
 with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest().upper()

try:
 managed=json.loads((home/'managed-rows.json').read_text())
 unity=json.loads((home/'unity-truth-rows.json').read_text())
 native=json.loads((home/'native-truth-rows.json').read_text())
 require(unity['status']=='PASS' and native['status']=='CAPTURED','Object truth engine capture missing or failed')
 expected=[]
 for ls in range(4):
  for rs in range(4):
   left=int(ls in (1,3));right=int(rs in (1,3))
   expected.append([ls,rs,left,1-left,left,left,left,left,left,left,2*left,31*left,left&right,left,left|right,1,left,right])
 require(len(managed)==len(unity['runs'])==2,'Source/restored captures missing')
 for rows in managed+unity['runs']+[native['rows']]:
  require(len(rows)==16 and all(len(row)==18 and all(type(v) is int for v in row) for row in rows),'Malformed truth capture')
  require(rows==expected,'Object truth or short-circuit calls differ from independent oracle')
 paths=[home/'unity-original.json',home/'native-execution.json',home/'unity-restored.json'];summaries=[]
 for path in paths:
  data=prefab.world.load(path);require(data['status']=='CAPTURED','World capture incomplete');summaries.append(prefab.summarize(path))
  observed=0
  for frame in data['frames']:
   for actor in frame['actors']:
    if actor['class']!='RuntimeSpawner':continue
    state=actor['state'];tick=state['Frame'];observed+=1
    for name,wanted in [('InactiveStillAlive',tick>=12),('AliveNextFrame',False),('MissingAfterDestroy',tick>=22)]:require(type(state[name]) is bool and state[name] is wanted,'World object validity differs: '+name)
  require(observed>30,'Runtime validity window missing')
 require(summaries[0]==summaries[1]==summaries[2],'Prefab lifecycle/reference/world-travel checkpoints differ')
 restored=list((home/'full-restored-scripts').glob('*.cs'));require(len(restored)==49,'Full-project reverse set incomplete')
 require(sha(home/'full-restored-scripts/RuntimeSpawner.cs')==sha(home/'runtime-spawner-restored.cs.txt'),'Full reverse output differs from executed source')
 target=root/'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Target/Plugins/EMActorPort/Source/EMActorPort/Public/RuntimeSpawner.h'
 require(sha(target)==sha(home/'RuntimeSpawner.h'),'Actual world header differs from recorded input')
 core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
 require(sha(core/'Project/Plugins/EMActorPort/Source/EMActorPort/Public/AuditImplicitTruth.h')==sha(home/'AuditImplicitTruth.h'),'Native truth header differs from current generator')
 baseline=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'));require(all(Path(p).is_file() and sha(p)==h.upper() for p,h in baseline.items()),'Original user project changed')
 inputs=[home/'managed-rows.json',home/'unity-truth-rows.json',home/'native-truth-rows.json']+paths
 result={'status':'PASS','truthStates':['null','live','destroyedOwner','inactiveOwner'],'referenceKinds':['GameObject','Transform','Canvas','Button','UserMonoBehaviour'],'truthRowsPerCapture':16,'truthValuesPerRow':16,'truthCapturesCompared':5,'prefabWorldCheckpoints':summaries[0],'worldTruthFlagsVerified':True,'fullProjectScriptsRestored':len(restored),'sourceHeadersSha256':{'world':sha(target),'truth':sha(home/'AuditImplicitTruth.h')},'inputHashes':{str(p):sha(p) for p in inputs},'originalFilesUnchanged':len(baseline),'projectPortComplete':False}
except Exception as error:
 output.write_text(json.dumps({'status':'FAIL','error':str(error)},indent=2),encoding='utf-8');raise
output.write_text(json.dumps(result,indent=2),encoding='utf-8')
print('OBJECT_TRUTH_ACTUAL_ROUNDTRIP_PASS: 5 x 16 x 16 values and real prefab/world validity checkpoints')
