"""Compare observed transform playback, clone references and native helper lifetimes."""
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison, single_runtime_driver
from pathlib import Path
import json,sys
SOURCES=('Loop','Dormant','Clamp','Once','Disabled')
INSTANCES={'First':('A','Loop'),'Second':('B','Loop'),'Copy':('C','Loop'),'Sleeping':('D','Dormant'),'Ended':('E','Clamp'),'Single':('F','Once'),'Still':('G','Disabled')}
REFS=set(SOURCES)|set(INSTANCES)|{v[0] for v in INSTANCES.values()}|{'Target'}
NAMES=REFS|{'Frame','FirstPose','SecondPose','CopyPose','SleepingPose','ClampPose','OncePose','DisabledPose','Awakes','Enables','Starts','Updates','Disables'}
ALIASES={v.casefold():v for v in NAMES}
def read(path):
 d=load_capture(path,case_insensitive_state=False);d['_native']=any('class' in a for f in d['frames'] for a in f['actors'])
 for f in d['frames']:
  for a in f['actors']:
   a['class']=a.get('class',a.get('className'));s=a['state'];s={v['name']:v['value'] for v in s} if isinstance(s,list) else s;out={}
   for key,value in s.items():
    k=ALIASES.get(key.casefold(),key);assert k not in out,('Ambiguous reflected field',k)
    if k in REFS:value=str(value)
    elif isinstance(value,str):
     if value.startswith('{'):value=json.loads(value)
     elif value.lower() in ('true','false'):value=value.lower()=='true'
     else:
      try:value=int(value)
      except ValueError:value=float(value)
    out[k]=value
   a['state']=out
 return d
def summarize(path):
 data=read(path);rows={};order=[]
 for f in data['frames']:
  driver=single_runtime_driver((a for a in f['actors'] if a['class'] in ('RuntimeAnimationDriver', 'RuntimeMiddle', 'RuntimeFinal')))
  if not driver:continue
  kind=driver['class'];s=driver['state']
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimeAnimationDriver' or int(s['Frame']) not in range(1,21):continue
  objects={o['id']:o for o in f['objects']};templates={o['id']:o for o in f['templates']};actors={a['id']:a for a in f['actors']};instances={};assets={}
  for key in SOURCES:
   assert s[key] in templates,(s['Frame'],'missing prototype',key)
   children=[o for o in templates.values() if o['parent']==s[key]];assert len(children)==1
   child=children[0];assets[key]=child
   for axis,value in {'x':0,'y':2,'z':3}.items():assert abs(child['localPosition'][axis]-value)<1e-5,(s['Frame'],key,'prototype played unexpectedly',child['localPosition'])
  for key,(component,source) in INSTANCES.items():
   obj=objects.get(s[key]);active=bool(obj and not obj.get('hidden',False));row={'alive':bool(s[key]),'active':active}
   if active:
    actor=actors[s[component]];child=objects[actor['state']['Target']];assert child['parent']==obj['id'],(key,'clone child points outside hierarchy')
    assert child['mesh'] and child['material'];assert child['mesh']==assets[source]['mesh'] and child['material']==assets[source]['material']
    row.update(position=child['localPosition'],scale=child['localScale'],counter={k:v for k,v in actor['state'].items() if k not in REFS})
   instances[key]=row
  if data['_native']:
   helpers=f['animationPresenters'];alive={s[key] for key in INSTANCES if s[key]}
   assert len(helpers)==len(alive) and {h['owner'] for h in helpers}==alive,(s['Frame'],'animation presenter lifetime/owner mismatch',helpers,alive)
   assert all(not h['tickEnabled'] for h in helpers),(s['Frame'],'native auto-play competes with clone clock')
  register_checkpoint(rows,int(s['Frame']),None)
  rows[str(s['Frame'])]={'driver':{k:v for k,v in s.items() if k not in REFS},'instances':instances,'cloneLinksAndHelperLifetimesVerified':True}
 assert len(rows)==20,len(rows);assert order==['RuntimeAnimationDriver','RuntimeMiddle','RuntimeFinal'],order
 return rows
def compare(a,b,path='',metrics=None):
 if isinstance(a,dict):
  assert isinstance(b,dict) and set(a)==set(b),(path,'keys',set(a),set(b))
  for k in a:compare(a[k],b[k],path+'/'+k,metrics)
 elif isinstance(a,(float,int)) and not isinstance(a,bool):
  tolerance=.002 if any(p in path for p in ('/position/','/scale/','Pose/')) else 1e-5;difference=abs(a-b)
  assert difference<=tolerance,(path,a,b,difference,tolerance)
  if tolerance==.002:metrics['maximumTransformDifference']=max(metrics['maximumTransformDifference'],difference)
 else:assert a==b,(path,a,b)
def semantics(rows):
 def value(frame,instance,axis,expected):assert abs(rows[str(frame)]['instances'][instance]['position'][axis]-expected)<.002,(frame,instance,axis,expected)
 value(2,'First','x',0);value(2,'First','y',0);value(2,'First','z',0)
 assert rows['2']['driver']['FirstPose']=={'x':0,'y':2,'z':3}
 value(6,'Copy','x',0);assert abs(rows['6']['driver']['CopyPose']['x']-.3)<.002
 assert rows['10']['instances']['Second']=={'alive':True,'active':False}
 value(12,'Second','x',0);assert abs(rows['12']['driver']['SecondPose']['x']-.5)<.002
 value(18,'Single','x',1);value(19,'Single','x',1);value(19,'Ended','x',1)
 assert rows['19']['instances']['Still']['position']=={'x':0,'y':2,'z':3}
 assert rows['20']['instances']['Sleeping']['alive'] and rows['20']['instances']['Sleeping']['active']
 assert all(not row['alive'] for key,row in rows['20']['instances'].items() if key!='Sleeping')
if __name__=='__main__':
 metrics={'maximumTransformDifference':0}
 run_comparison(sys.argv[1:],summarize,semantics,lambda expected,actual,path:{'animationTolerance':.002,'animationMetrics':metrics},'RUNTIME_ANIMATION_PREFAB_EXECUTION_COMPARISON_PASS',comparison=lambda expected,actual:compare(expected,actual,metrics=metrics))
