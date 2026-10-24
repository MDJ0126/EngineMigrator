"""Compare dynamic prefab behaviour and measured physics, with explicit solver tolerances."""
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison, single_runtime_driver
from pathlib import Path
import json,sys,math
REFS={'Box','Sphere','Capsule','Dormant','First','Ball','Pill','Sleeping','Second','Counter','SleepingCounter','Target'}
INSTANCES=('First','Ball','Pill','Sleeping','Second');SOURCES=('Box','Sphere','Capsule','Dormant')
def read(path):
 d=load_capture(path,case_insensitive_state=False);d['_native']=any('class' in a for f in d['frames'] for a in f['actors'])
 for f in d['frames']:
  for a in f['actors']:
   a['class']=a.get('class',a.get('className'));s=a['state'];s={v['name']:v['value'] for v in s} if isinstance(s,list) else s
   a['state']={k:str(v) if k in REFS else v.lower()=='true' if isinstance(v,str) and v.lower() in ('true','false') else int(v) if isinstance(v,str) and v.lstrip('-').isdigit() else v for k,v in s.items()}
 return d
def summarize(path):
 rows={};order=[]
 data=read(path)
 for f in data['frames']:
  driver=single_runtime_driver((a for a in f['actors'] if a['class'] in ('RuntimePhysicsDriver', 'RuntimeMiddle', 'RuntimeFinal')))
  if not driver:continue
  kind=driver['class'];s=driver['state']
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimePhysicsDriver' or int(s['Frame']) not in range(1,19):continue
  objects={o['id']:o for o in f['objects']};templates={o['id']:o for o in f['templates']};actors={a['id']:a for a in f['actors']};bodies={};counters={}
  for key in SOURCES:
   assert s[key] in templates,(s['Frame'],'missing template',key)
   prototype=templates[s[key]];assert prototype.get('physics',{}).get('shape'),(key,'no native prototype collider')
   assert abs(prototype['position']['y']-10)<1e-4,(s['Frame'],key,'prototype moved')
   if data['_native']:assert prototype['physics']['dynamic'] is False and prototype['hidden'] is True,(key,'prototype simulation/visibility not suppressed')
  for key in INSTANCES:
   obj=objects.get(s[key]);active=bool(obj and not obj.get('hidden',False));bodies[key]={'alive':bool(s[key]),'active':active}
   if active:
    p=obj.get('physics');assert p and p.get('shape'),(s['Frame'],key,'missing runtime collider')
    bodies[key].update(physics=p,position=obj['position'])
  for key in ('Counter','SleepingCounter'):
   actor=actors.get(s[key]);owner=objects.get(actor['owner']) if actor else None
   if owner and not owner.get('hidden',False):counters[key]={k:v for k,v in actor['state'].items() if k not in REFS}
  register_checkpoint(rows,int(s['Frame']),None)
  rows[str(s['Frame'])]={'driver':{k:v for k,v in s.items() if k not in REFS},'bodies':bodies,'counters':counters}
 assert len(rows)==18,len(rows);assert order==['RuntimePhysicsDriver','RuntimeMiddle','RuntimeFinal'],order
 assert rows['2']['driver']['ReturnAwakes']==1 and rows['2']['driver']['ReturnStarts']==0
 assert rows['5']['bodies']['Sleeping']=={'alive':True,'active':False}
 assert rows['7']['bodies']['Sleeping']['physics']['dynamic'] is True
 assert rows['8']['bodies']['First']['position']['y']<10
 assert rows['9']['bodies']['First']=={'alive':True,'active':False}
 assert rows['11']['bodies']['First']['physics']['dynamic'] is True
 assert all(not b['alive'] for b in rows['14']['bodies'].values())
 return rows
def compare(a,b,path='',metrics=None):
 if isinstance(a,dict):
  assert isinstance(b,dict) and set(a)==set(b),(path,'keys',set(a),set(b))
  for k in a:compare(a[k],b[k],path+'/'+k,metrics)
 elif isinstance(a,(float,int)) and not isinstance(a,bool):
  # This fixture has isolated, undamped free fall and no contacts. Require
  # phase alignment; the broader legacy collision-solver bound is unsuitable.
  tolerance=.0001 if '/position/' in path or '/velocity/' in path else 1e-5
  difference=abs(a-b);assert difference<=tolerance,(path,a,b,difference,tolerance)
  if '/position/' in path:metrics['maxPositionDifferenceMeters']=max(metrics['maxPositionDifferenceMeters'],difference)
  if '/velocity/' in path:metrics['maxVelocityDifferenceMetersPerSecond']=max(metrics['maxVelocityDifferenceMetersPerSecond'],difference)
 else:assert a==b,(path,a,b)
if __name__=='__main__':
 metrics={'maxPositionDifferenceMeters':0,'maxVelocityDifferenceMetersPerSecond':0}
 run_comparison(sys.argv[1:],summarize,lambda rows:None,lambda expected,actual,path:{'physicsPositionToleranceMeters':.0001,'physicsVelocityToleranceMetersPerSecond':.0001,'physicsMetrics':metrics},'RUNTIME_PHYSICS_PREFAB_EXECUTION_COMPARISON_PASS',comparison=lambda expected,actual:compare(expected,actual,metrics=metrics))
