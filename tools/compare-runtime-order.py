"""Compare explicit startup ordering, inherited callbacks and activation reentry."""
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison, single_runtime_driver
from pathlib import Path
import json,sys
REFS={'Ledger','Other','First','Second','ComponentNode','ObjectNode','Component','Object','ComponentRecorder','ObjectRecorder','Recorder'}
def read(path):
 d=load_capture(path,case_insensitive_state=False)
 for f in d['frames']:
  for a in f['actors']:
   a['class']=a.get('class',a.get('className'));s=a['state'];s={v['name']:v['value'] for v in s} if isinstance(s,list) else s
   def value(k,v):
    if k in REFS:return str(v)
    if isinstance(v,str) and v.lower() in ('true','false'):return v.lower()=='true'
    if k in ('Code','InitialCode'):return int(float(v))
    if isinstance(v,str):return int(v) if v.lstrip('-').isdigit() else float(v)
    return v
   assert not ('ID' in s and 'Id' in s),'Ambiguous case-folded source identifier'
   a['state']={('Id' if k=='ID' else k):value(k,v) for k,v in s.items()}
 return d
def normalize(path):
 rows={};order=[];seen_update=False
 for f in read(path)['frames']:
  driver=single_runtime_driver((a for a in f['actors'] if a['class'] in ('RuntimeOrderDriver', 'RuntimeMiddle', 'RuntimeFinal')))
  if not driver:continue
  kind=driver['class'];s=driver['state']
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimeOrderDriver':continue
  if int(s['Frame'])==0:
   assert not seen_update,'Driver returned to startup after gameplay began'
   actors={a['id']:a for a in f['actors']}
   for ref in ('First','Second'):
    before=actors[s[ref]]['state'];assert before['Awakes']==1 and before['Starts']==1 and before['Updates']==0,'Startup sample must precede every user Update'
   assert actors[s['Ledger']]['state']['Code']==11102122122,'Startup callbacks were not completed in order'
   continue
  seen_update=True
  if int(s['Frame']) not in range(1,19):continue
  n=int(s['Frame']);actors={a['id']:a for a in f['actors']};first=actors[s['First']]['state'];second=actors[s['Second']]['state']
  assert s['InitialCode']==11102122122,(n,'initial callback order',s['InitialCode'])
  assert [first[k] for k in ('AwakeSawOther','EnableSawOther','StartSawOther')]==[0,0,0]
  assert [second[k] for k in ('AwakeSawOther','EnableSawOther','StartSawOther')]==[1,1,1]
  assert s['ComponentImmediate']=={2:124,3:2,4:42,5:4,6:2,10:4,11:2}.get(n,0),(n,'component immediate callbacks',s['ComponentImmediate'])
  assert s['ObjectImmediate']=={2:124,3:2,4:4,5:2,6:4,7:2,8:4,9:2}.get(n,0),(n,'object immediate callbacks',s['ObjectImmediate'])
  if n==4:assert s['ObjectSelf'] and not s['ObjectHierarchy'],'deactivation reentry must retain distinct self/hierarchy state'
  register_checkpoint(rows,int(n),None)
  rows[str(n)]={'driver':{k:v for k,v in s.items() if k not in REFS},'first':{k:v for k,v in first.items() if k not in REFS},'second':{k:v for k,v in second.items() if k not in REFS},'componentRecorder':actors[s['ComponentRecorder']]['state'],'objectRecorder':actors[s['ObjectRecorder']]['state']}
 assert len(rows)==18,len(rows);assert order==['RuntimeOrderDriver','RuntimeMiddle','RuntimeFinal'],order
 return rows
def compare(a,b,path=''):
 if isinstance(a,dict):
  assert set(a)==set(b),(path,'keys')
  for k in a:compare(a[k],b[k],path+'/'+k)
 else:assert a==b,(path,a,b)
if __name__=='__main__':
 run_comparison(sys.argv[1:],normalize,lambda rows:None,lambda expected,actual,path:{},'RUNTIME_ORDER_EXECUTION_COMPARISON_PASS',comparison=compare)
