"""Saved-engine-capture mutation tests for cloning, prefab creation and deletion."""
from pathlib import Path
import copy,hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'artifacts/core-capture-integrity';OUT.mkdir(exist_ok=True)
CASES=[('world','RuntimeSpawner',(10,12,18,20,22,30,32,38),'unity-original-final.json','unreal-position-fixed.json','unity-restored.json'),('prefab','RuntimeSpawner',(10,12,18,20,22,30,32,38),'unity-original.json','unreal-safe-destroy.json','unity-restored-safe.json'),('destroy','RuntimeDestroyDriver',tuple(range(1,19)),'unity-original.json','unreal-v3.json','unity-restored.json')]
checks=[]
def read(path):
 raw=path.read_bytes();return json.loads(raw.decode('utf-16' if raw[:2] in (b'\xff\xfe',b'\xfe\xff') else 'utf-8-sig'))
def call(script,expected,actual,report,optimized=False):
 return subprocess.run([sys.executable]+(['-O'] if optimized else [])+[str(script),str(expected),str(actual),str(report)],capture_output=True,text=True)
def number(frame,driver):
 for actor in frame['actors']:
  if actor.get('class',actor.get('className'))==driver:
   state=actor['state'];state={x['name']:x['value'] for x in state} if isinstance(state,list) else state
   return int(state['Frame'])
 return None
for profile,driver,sequence,original,actual,restored in CASES:
 base=ROOT/('artifacts/runtime-'+profile+'-tests');script=ROOT/('tools/compare-runtime-'+profile+'.py');expected=base/original;actual_path=base/actual
 originals=[expected,actual_path,base/restored];hashes=[hashlib.sha256(p.read_bytes()).hexdigest() for p in originals]
 for label,path in [('native',actual_path),('restored',base/restored)]:
  result=call(script,expected,path,OUT/(profile+'-'+label+'-positive.json'))
  if result.returncode:raise RuntimeError(result.stderr)
  checks.append({'profile':profile,'case':label+' saved capture','status':'PASS'})
 data=read(actual_path);mutations={}
 for collection,identity in [('objects','id'),('actors','id'),('templates','id')]:
  if not any(f.get(collection) for f in data['frames']):continue
  mutation=copy.deepcopy(data);frame=next(f for f in mutation['frames'] if f.get(collection));frame[collection].append(copy.deepcopy(frame[collection][0]));mutations['duplicate-'+collection]=mutation
 mutation=copy.deepcopy(data);index=next(i for i,f in enumerate(mutation['frames']) if number(f,driver)==sequence[1]);mutation['frames'].insert(index,copy.deepcopy(mutation['frames'][index]));mutations['duplicate-checkpoint']=mutation
 mutation=copy.deepcopy(data);first=next(i for i,f in enumerate(mutation['frames']) if number(f,driver)==sequence[1]);second=next(i for i,f in enumerate(mutation['frames']) if number(f,driver)==sequence[2]);mutation['frames'][first],mutation['frames'][second]=mutation['frames'][second],mutation['frames'][first];mutations['out-of-order-checkpoint']=mutation
 mutation=copy.deepcopy(data);mutation['frames']=[f for f in mutation['frames'] if number(f,driver)!=sequence[1]];mutations['missing-checkpoint']=mutation
 mutation=copy.deepcopy(data);frame=next(f for f in mutation['frames'] if number(f,driver)==sequence[1]);actor=next(a for a in frame['actors'] if a.get('class',a.get('className'))==driver);state=actor['state'];key=next(k for k,v in state.items() if type(v)is bool);state[key]=int(state[key]);mutations['numeric-boolean-state']=mutation
 mutation=copy.deepcopy(data);mutation['extra']=float('nan');mutations['nonstandard-json-number']=mutation
 for label,mutation in mutations.items():
  path=OUT/(profile+'-'+label+'.json');path.write_text(json.dumps(mutation),encoding='utf-8');report=OUT/(profile+'-'+label+'-verification.json');report.write_text('{"status":"PASS","stale":true}',encoding='utf-8');result=call(script,expected,path,report)
  if result.returncode==0 or read(report)['status']!='FAIL' or 'stale'in read(report):raise RuntimeError('Invalid capture or stale PASS accepted: '+profile+'/'+label)
  checks.append({'profile':profile,'case':label,'status':'REJECTED'})
 result=call(script,expected,actual_path,OUT/(profile+'-optimized-verification.json'),optimized=True)
 if result.returncode==0 or read(OUT/(profile+'-optimized-verification.json'))['status']!='FAIL':raise RuntimeError('Optimized assertion-free run accepted')
 checks.append({'profile':profile,'case':'optimized-Python','status':'REJECTED'})
 if hashes!=[hashlib.sha256(p.read_bytes()).hexdigest() for p in originals]:raise RuntimeError('Saved engine evidence changed')
(OUT/'verification.json').write_text(json.dumps({'status':'PASS','count':len(checks),'checks':checks,'newEngineExecution':False,'existingCaptureReplayOnly':True},indent=2),encoding='utf-8')
print(f'{len(checks)} core capture integrity checks PASS')
