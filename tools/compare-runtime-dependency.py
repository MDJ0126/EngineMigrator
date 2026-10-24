"""Compare real engine observations for transitive, cyclic and self prefab references."""
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison, single_runtime_driver
from pathlib import Path
import json,sys
REFS={'Source','First','Second','Third','Again','Copy','Extra','A','B','C','A2','Copied','B2','Next','SelfAsset','LocalRoot','Peer'}
COUNTERS=('A','B','C','A2','Copied','B2')
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
  driver=single_runtime_driver((a for a in f['actors'] if a['class'] in ('RuntimeDependencyDriver', 'RuntimeMiddle', 'RuntimeFinal')))
  if not driver:continue
  kind=driver['class'];s=driver['state']
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimeDependencyDriver' or int(s['Frame']) not in range(1,19):continue
  actors={a['id']:a for a in f['actors']};objects={o['id']:o for o in f['objects']};counters={}
  for key in COUNTERS:
   a=actors.get(s[key]);obj=objects.get(a['owner']) if a else None
   if a and obj and not obj.get('hidden',False):counters[key]={k:v for k,v in a['state'].items() if k not in REFS}
  register_checkpoint(rows,int(s['Frame']),None)
  rows[str(s['Frame'])]={'driver':{k:v for k,v in s.items() if k not in REFS},'counterStates':counters,'referencesAlive':{k:bool(s[k]) for k in COUNTERS}}
 assert len(rows)==18,len(rows);assert order==['RuntimeDependencyDriver','RuntimeMiddle','RuntimeFinal'],order
 return rows
def compare(a,b,path=''):
 if isinstance(a,dict):
  assert set(a)==set(b),(path,'keys',set(a),set(b))
  for k in a:compare(a[k],b[k],path+'/'+k)
 else:assert a==b,(path,a,b)
def semantics(rows):
 def fields(frame,**values):
  for key,value in values.items():assert rows[str(frame)]['driver'][key]==value,(frame,key,value,rows[str(frame)]['driver'][key])
 fields(2,FirstKind=1,AAsset=False,LocalLinked=True,PeerLinked=True,ReturnAwakes=1,ReturnStarts=0)
 fields(3,SecondKind=2,BAsset=False)
 fields(4,ThirdKind=3,CAsset=False)
 fields(5,AgainKind=1,A2Asset=False)
 fields(6,CopyKind=1,CopyAsset=False,CopyLinked=True,CopyPeerLinked=True)
 fields(8,ExtraKind=2)
 assert not rows['7']['referencesAlive']['A']
 assert not any(rows['13']['referencesAlive'].values())
if __name__=='__main__':
 run_comparison(sys.argv[1:],normalize,semantics,lambda expected,actual,path:{},'RUNTIME_DEPENDENCY_EXECUTION_COMPARISON_PASS',comparison=compare)
