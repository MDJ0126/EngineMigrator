"""Regression replay and corrupt-capture checks without engine execution."""
from pathlib import Path
import copy,hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'artifacts/runtime-regression-integrity';OUT.mkdir(exist_ok=True)
PROFILES=['physics-prefab','animation-prefab','dependency','dormant','order','phase','active','enable','components','placement']
checks=[]
def read(path):
 raw=path.read_bytes();return json.loads(raw.decode('utf-16' if raw[:2] in (b'\xff\xfe',b'\xfe\xff') else 'utf-8-sig'))
def call(script,expected,actual,report,optimized=False):
 return subprocess.run([sys.executable]+(['-O'] if optimized else [])+[str(script),str(expected),str(actual),str(report)],capture_output=True,text=True)
def state(actor):
 value=actor['state'];return {v['name']:v['value'] for v in value} if isinstance(value,list) else value
for profile in PROFILES:
 label='component' if profile=='components' else profile
 script=ROOT/('tools/compare-runtime-'+profile+'.py');base=ROOT/('artifacts/runtime-'+label+'-tests');expected=base/('unity-original-v5.json' if profile=='components' else 'unity-original.json');actual=ROOT/('artifacts/runtime-ui-api-tests/'+label+'-regression.json')
 before=[hashlib.sha256(p.read_bytes()).hexdigest() for p in [expected,actual]]
 result=call(script,expected,actual,OUT/(profile+'-positive.json'))
 if result.returncode:raise RuntimeError(profile+': '+result.stderr)
 checks.append({'profile':profile,'case':'saved native capture','status':'PASS'})
 sequence=[int(n) for n in read(OUT/(profile+'-positive.json'))['checkpoints']]
 data=read(actual)
 driver_name=next(a.get('class',a.get('className')) for f in data['frames'] for a in f['actors'] if a.get('class',a.get('className')) not in ('RuntimeMiddle','RuntimeFinal') and 'Frame'in state(a) and int(state(a)['Frame'])==sequence[0])
 def selected(frame,n):return any(a.get('class',a.get('className'))==driver_name and int(state(a)['Frame'])==n for a in frame['actors'])
 mutations={}
 for collection in ['actors','objects']:
  mutation=copy.deepcopy(data);f=next(f for f in mutation['frames'] if selected(f,sequence[1]));f[collection].append(copy.deepcopy(f[collection][0]));mutations['duplicate-'+collection]=mutation
 mutation=copy.deepcopy(data);index=next(i for i,f in enumerate(mutation['frames']) if selected(f,sequence[1]));mutation['frames'].insert(index,copy.deepcopy(mutation['frames'][index]));mutations['duplicate-checkpoint']=mutation
 mutation=copy.deepcopy(data);first=next(i for i,f in enumerate(mutation['frames']) if selected(f,sequence[1]));second=next(i for i,f in enumerate(mutation['frames']) if selected(f,sequence[2]));mutation['frames'][first],mutation['frames'][second]=mutation['frames'][second],mutation['frames'][first];mutations['out-of-order-checkpoint']=mutation
 mutation=copy.deepcopy(data);mutation['frames']=[f for f in mutation['frames'] if not selected(f,sequence[1])];mutations['missing-checkpoint']=mutation
 mutation=copy.deepcopy(data);f=next(f for f in mutation['frames'] if selected(f,sequence[1]));actor=copy.deepcopy(next(a for a in f['actors'] if a.get('class',a.get('className'))==driver_name));actor['id']+='-duplicate-driver';f['actors'].append(actor);mutations['multiple-runtime-drivers']=mutation
 mutation=copy.deepcopy(data);mutation['nonfinite']=float('nan');mutations['nonstandard-json-number']=mutation
 for label,mutation in mutations.items():
  path=OUT/(profile+'-'+label+'.json');path.write_text(json.dumps(mutation),encoding='utf-8');report=OUT/(profile+'-'+label+'-verification.json');report.write_text('{"status":"PASS","stale":true}',encoding='utf-8');result=call(script,expected,path,report)
  if result.returncode==0 or read(report)['status']!='FAIL' or read(report).get('stale'):raise RuntimeError('Invalid capture or stale PASS accepted: '+profile+'/'+label)
  checks.append({'profile':profile,'case':label,'status':'REJECTED'})
 report=OUT/(profile+'-optimized-verification.json');result=call(script,expected,actual,report,optimized=True)
 if result.returncode==0 or read(report)['status']!='FAIL':raise RuntimeError('Optimized assertions accepted: '+profile)
 checks.append({'profile':profile,'case':'optimized-Python','status':'REJECTED'})
 if profile in ('physics-prefab','animation-prefab'):
  for delta,accepted in [(0.00001,True),(0.1,False)]:
   mutation=copy.deepcopy(data);f=next(f for f in mutation['frames'] if selected(f,2));driver=next(a for a in f['actors'] if a.get('class',a.get('className'))==driver_name);owner=state(driver)['First'];objects={o['id']:o for o in f['objects']}
   if profile=='physics-prefab':objects[owner]['position']['x']+=delta
   else:
    counter=next(a for a in f['actors'] if a['owner']==owner and 'Target' in state(a))
    objects[state(counter)['Target']]['localPosition']['x']+=delta
   path=OUT/(profile+'-tolerance-'+str(delta)+'.json');path.write_text(json.dumps(mutation),encoding='utf-8');report=OUT/(profile+'-tolerance-'+str(delta)+'-verification.json');result=call(script,expected,path,report)
   if (result.returncode==0)!=accepted:raise RuntimeError('Solver/animation tolerance changed: '+profile+' '+str(delta)+' '+result.stderr)
   checks.append({'profile':profile,'case':'tolerance '+str(delta),'status':'PASS' if accepted else 'REJECTED'})
 if before!=[hashlib.sha256(p.read_bytes()).hexdigest() for p in [expected,actual]]:raise RuntimeError('Original capture changed')
(OUT/'verification.json').write_text(json.dumps({'status':'PASS','count':len(checks),'checks':checks,'newEngineExecution':False,'existingCaptureReplayOnly':True,'originalPhysicsAndAnimationTolerancesPreserved':True},indent=2),encoding='utf-8')
print(f'{len(checks)} runtime regression integrity checks PASS')

