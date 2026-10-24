"""Compare reparent activation and creation/destruction inside actual engine callbacks."""
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison, single_runtime_driver
import json,sys,math
from pathlib import Path
REFS={'ActiveParent','InactiveParent','Child','Recorder','Counter','Producer','AwakeRecorder','StartRecorder','KillAwakeRecorder','KillStartRecorder','AwakeObject','StartObject','KillAwakeObject','KillStartObject','AwakeCounter','StartCounter','KillAwakeCounter','KillStartCounter'}
def read(path):
 d=load_capture(path,case_insensitive_state=False)
 for frame in d['frames']:
  for a in frame['actors']:
   a['class']=a.get('class',a.get('className'));s=a['state'];s={v['name']:v['value'] for v in s} if isinstance(s,list) else s
   a['state']={k:str(v) if k in REFS else json.loads(v) if isinstance(v,str) and v.startswith('{') else v.lower()=='true' if isinstance(v,str) and v.lower() in ('true','false') else int(v) if isinstance(v,str) and v.lstrip('-').isdigit() else v for k,v in s.items()}
 return d
def normalize(path):
 rows={};order=[]
 for f in read(path)['frames']:
  driver=single_runtime_driver((a for a in f['actors'] if a['class'] in ('RuntimeParentDriver', 'RuntimeMiddle', 'RuntimeFinal')))
  if not driver:continue
  kind=driver['class'];s=driver['state']
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimeParentDriver' or int(s['Frame']) not in range(1,19):continue
  n=int(s['Frame']);actors={a['id']:a for a in f['actors']};p=actors[s['Producer']]['state']
  assert s['Immediate']=={2:4,4:2,6:4,7:2,8:4,11:2,12:4,13:2,15:4,16:2}.get(n,0),(n,'parent activation immediate callbacks',s['Immediate'])
  for key,value in {'AwakeImmediate':12,'AwakeReturnStart':0,'StartImmediate':12,'StartReturnStart':0,'KillAwakeImmediate':14,'KillStartImmediate':12}.items():assert p[key]==value,(n,key,p[key],value)
  for key in ('AwakeRecorder','StartRecorder'):
   r=actors[p[key]]['state'];assert (r['Awakes'],r['Enables'],r['Starts'],r['Updates'])==(1,1,1,n),(n,key,'initial Start/Update timing',r)
  r=actors[p['KillAwakeRecorder']]['state'];assert (r['Code'],r['Awakes'],r['Enables'],r['Starts'],r['Disables'],r['Destroys'],r['Updates'])==(145,1,0,0,1,1,0),(n,'destroy inside Awake',r)
  r=actors[p['KillStartRecorder']]['state'];assert (r['Code'],r['Awakes'],r['Enables'],r['Starts'],r['Disables'],r['Destroys'],r['Updates'])==(12345,1,1,1,1,1,0),(n,'destroy inside Start',r)
  register_checkpoint(rows,int(n),None)
  rows[str(n)]={'driver':{k:v for k,v in s.items() if k not in REFS},'parentRecorder':actors[s['Recorder']]['state'],'producer':{k:v for k,v in p.items() if k not in REFS},'recorders':{k:actors[p[k]]['state'] for k in ('AwakeRecorder','StartRecorder','KillAwakeRecorder','KillStartRecorder')},'alive':{k:bool(actors.get(p[k])) for k in ('AwakeCounter','StartCounter','KillAwakeCounter','KillStartCounter')}}
 assert len(rows)==18,len(rows);assert order==['RuntimeParentDriver','RuntimeMiddle','RuntimeFinal'],order
 return rows
def compare(a,b,path=''):
 if isinstance(a,dict):
  assert set(a)==set(b),(path,'keys')
  if set(a)==set('xyzw'):assert min(sum((a[k]-b[k])**2 for k in a),sum((a[k]+b[k])**2 for k in a))<1e-8,(path,a,b)
  else:
   for k in a:compare(a[k],b[k],path+'/'+k)
 elif isinstance(a,(int,float)) and not isinstance(a,bool):assert math.isclose(a,b,rel_tol=1e-5,abs_tol=1e-4),(path,a,b)
 else:assert a==b,(path,a,b)
if __name__=='__main__':
 run_comparison(sys.argv[1:],normalize,lambda rows:None,lambda expected,actual,path:{},'RUNTIME_PHASE_EXECUTION_COMPARISON_PASS',comparison=compare)
