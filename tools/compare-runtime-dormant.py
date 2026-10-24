"""Compare inactive prefab activation, internal links and cloning within disable callbacks."""
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison, single_runtime_driver
from pathlib import Path
import json,sys
REFS={'Source','First','Second','SceneSource','ReentrySource','PendingCopy','ReentryCopy','FirstCounter','FirstChild','SecondCounter','SecondChild','SceneCounter','ReentryCounter','PendingCounter','ReentryCopyCounter','Ledger','Target','Peer','Copy'}
COUNTERS=('FirstCounter','FirstChild','SecondCounter','SecondChild','SceneCounter','ReentryCounter','PendingCounter','ReentryCopyCounter')
def read(path):
 d=load_capture(path,case_insensitive_state=False)
 for f in d['frames']:
  for a in f['actors']:
   a['class']=a.get('class',a.get('className'));s=a['state'];s={v['name']:v['value'] for v in s} if isinstance(s,list) else s
   a['state']={k:str(v) if k in REFS else v.lower()=='true' if isinstance(v,str) and v.lower() in ('true','false') else int(v) if isinstance(v,str) and v.lstrip('-').isdigit() else v for k,v in s.items()}
 return d
def normalize(path):
 rows={};order=[]
 for f in read(path)['frames']:
  driver=single_runtime_driver((a for a in f['actors'] if a['class'] in ('RuntimeDormantDriver', 'RuntimeMiddle', 'RuntimeFinal')))
  if not driver:continue
  kind=driver['class'];s=driver['state']
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimeDormantDriver' or int(s['Frame']) not in range(1,19):continue
  n=int(s['Frame']);actors={a['id']:a for a in f['actors']};objects={o['id']:o for o in f['objects']};counters={}
  for key in COUNTERS:
   a=actors.get(s[key]);obj=objects.get(a['owner']) if a else None
   if a and obj and not obj.get('hidden',False):counters[key]={k:v for k,v in a['state'].items() if k not in REFS}
  register_checkpoint(rows,int(n),None)
  rows[str(n)]={'driver':{k:v for k,v in s.items() if k not in REFS},'ledger':actors[s['Ledger']]['state'],'counterStates':counters,'referencesAlive':{k:bool(s[k]) for k in COUNTERS}}
 assert len(rows)==18,len(rows);assert order==['RuntimeDormantDriver','RuntimeMiddle','RuntimeFinal'],order
 return rows
def compare(a,b,path=''):
 if isinstance(a,dict):
  assert set(a)==set(b),(path,'keys',set(a),set(b))
  for k in a:compare(a[k],b[k],path+'/'+k)
 else:assert a==b,(path,a,b)

def assert_semantics(rows):
 # Measured in the original Unity fixture, then required of every target.
 def fields(frame,**values):
  for key,value in values.items():assert rows[str(frame)]['driver'][key]==value,(frame,key,value)
 fields(2,FirstAwakes=0,FirstEnables=0,FirstStarts=0,FirstSelf=False,FirstLinked=True)
 fields(3,FirstAwakes=1,FirstEnables=1,FirstStarts=0,ChildAwakes=0)
 fields(4,FirstStarts=1,ChildAwakes=1,ChildStarts=0)
 fields(6,SecondAwakes=1,SecondEnables=1,SecondStarts=1,SecondSelf=False,SecondLinked=True)
 fields(7,SecondAwakes=2,SecondEnables=2,SecondStarts=1,SecondChildAwakes=2)
 fields(8,PendingAwakes=2,PendingEnables=1,PendingStarts=1,PendingEnabled=False,PendingAlive=True)
 fields(9,PendingEnables=2,PendingStarts=1,PendingEnabled=True)
 fields(10,PendingStarts=2,PendingEnabled=False)
 fields(13,ReentryAwakes=1,ReentrySelf=False,ReentryHierarchy=False,ReentryAlive=True)
 fields(14,ReentryAwakes=2,ReentryEnables=2,ReentryStarts=1,ReentrySelf=True)
 for frame,deaths in ((8,1),(10,2),(16,3),(17,4)):
  assert rows[str(frame)]['ledger']['Deaths']==deaths,(frame,'ledger')
 assert rows['10']['referencesAlive']['PendingCounter'] is False
 assert rows['16']['referencesAlive']['ReentryCopyCounter'] is False

if __name__=='__main__':
 run_comparison(sys.argv[1:],normalize,assert_semantics,lambda expected,actual,path:{},'RUNTIME_DORMANT_EXECUTION_COMPARISON_PASS',comparison=compare)
