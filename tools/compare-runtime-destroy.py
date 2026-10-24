"""Compare whole-GameObject deletion and initial prefab creation against actual Unity."""
from pathlib import Path
import json,sys
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison
REFS={'Root','Disabled','Inactive','Created','RootCounter','ChildCounter','DisabledCounter','InactiveCounter','Added','RootRecord','ChildRecord','DisabledRecord','InactiveRecord','AwakeKillRecord','StartKillRecord','Producer','Source','AwakeClone','StartClone','AwakeCounter','StartCounter','AwakeRecord','StartRecord','Record'}
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
  drivers=[a for a in f['actors'] if a['class'] in ('RuntimeDestroyDriver','RuntimeMiddle','RuntimeFinal')]
  if len(drivers)>1:raise ValueError('Multiple runtime drivers in one captured frame')
  driver=drivers[0] if drivers else None
  if not driver:continue
  kind=driver['class'];s=driver['state']
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimeDestroyDriver' or int(s['Frame']) not in range(1,19):continue
  n=int(s['Frame']);register_checkpoint(rows,n);actors={a['id']:a for a in f['actors']};p=actors[s['Producer']]['state']
  recorders={k:actors[s[k]]['state'] for k in ('RootRecord','ChildRecord','DisabledRecord','InactiveRecord','AwakeKillRecord','StartKillRecord')}
  recorders.update({k:actors[p[k]]['state'] for k in ('AwakeRecord','StartRecord')})
  assert (p['AwakeImmediate'],p['AwakeStartAtReturn'],p['StartImmediate'],p['StartAtReturn'])==(12,0,12,0),'Initial prefab callbacks ran inside the wrong phase'
  assert recorders['AwakeRecord']['Updates']==min(n,6) and recorders['StartRecord']['Updates']==min(n,7),'Prefab first Update or deletion suppression differs'
  if n>=2:
   assert not s['RootEnabledAtReturn'] and not s['ChildEnabledAtReturn'] and s['RootSelfAtReturn'] and s['RootHierarchyAtReturn'] and s['RootAliveAtReturn']
   for key in ('RootRecord','ChildRecord'):
    r=recorders[key];assert r['Disables']==1 and r['Destroys']==1 and not r['DisEnabled'] and r['DisSelf'] and r['DisHierarchy'] and not r['DesEnabled'] and not r['DesHierarchy']
   assert not recorders['RootRecord']['DesSelf'] and recorders['ChildRecord']['DesSelf'],'Root/descendant self flags differ during final destruction'
  if n>=3:
   r=recorders['DisabledRecord'];assert (r['Awakes'],r['Enables'],r['Starts'],r['Disables'],r['Destroys'])==(1,0,0,0,1)
  assert all(recorders['InactiveRecord'][k]==0 for k in ('Awakes','Enables','Starts','Disables','Destroys')),'Never-active object received lifecycle callbacks'
  if n>=5:
   r=recorders['AwakeKillRecord'];assert (r['Awakes'],r['Enables'],r['Starts'],r['Disables'],r['Destroys'])==(1,0,0,1,1)
  if n==6:
   r=recorders['StartKillRecord'];assert r['Code']==12342 and r['Destroys']==0 and s['Added'] and r['AfterEnabled']
  if n>=7:
   r=recorders['StartKillRecord'];assert not s['Added'] and (r['Enables'],r['Updates'],r['Disables'],r['Destroys'])==(2,0,2,1) and r['DesEnabled'] and r['DesSelf'] and not r['DesHierarchy']
  rows[str(n)]={'driver':{k:v for k,v in s.items() if k not in REFS},'producer':{k:v for k,v in p.items() if k not in REFS},'recorders':recorders,'alive':{k:bool(s[k]) for k in ('RootCounter','ChildCounter','DisabledCounter','InactiveCounter','Added')}}
 assert len(rows)==18,len(rows);assert order==['RuntimeDestroyDriver','RuntimeMiddle','RuntimeFinal'],order
 return rows
def compare(a,b,path=''):
 if isinstance(a,dict):
  assert set(a)==set(b),(path,'keys')
  for k in a:compare(a[k],b[k],path+'/'+k)
 else:assert a==b,(path,a,b)
if __name__=='__main__':
 run_comparison(sys.argv[1:],normalize,lambda rows:None,lambda expected,actual,path:{},'RUNTIME_DESTROY_EXECUTION_COMPARISON_PASS')
