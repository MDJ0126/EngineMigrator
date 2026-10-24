from runtime_capture_validation import load_capture, register_checkpoint, run_comparison
import importlib.util,json,sys,math
from pathlib import Path
spec=importlib.util.spec_from_file_location('world',Path(__file__).with_name('compare-runtime-world.py'));world=importlib.util.module_from_spec(spec);spec.loader.exec_module(world)
def normalize(path):
 data=world.load(path);result={};order=[]
 for f in data['frames']:
  drivers=[a for a in f['actors'] if a['class'] in ('PlacementDriver','RuntimeMiddle','RuntimeFinal')]
  if not drivers:continue
  assert len(drivers)==1;d=drivers[0];kind=d['class']
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='PlacementDriver':continue
  frame=int(d['state']['Frame'])
  if frame not in range(3,31,3):continue
  objects={o['id']:o for o in f['objects']};rows={}
  for label,field in (('prefab','LastPrefab'),('scene','LastScene')):
   owner=d['state'][field];clone=next(a for a in f['actors'] if a['class']=='PlacementCounter' and a['owner']==owner);state=clone['state'];obj=objects[owner]
   for key,value in list(state.items()):
    if isinstance(value,str) and value.startswith('{'):state[key]=json.loads(value)
   assert state['Awakes']==(1 if label=='prefab' else 2);assert state['Starts']==(1 if label=='prefab' else 2)
   assert state['Target']==state['TargetTransform'];child=objects[state['Target']];assert child['parent']==owner
   assert state['ParentAtAwake']==obj['parent'];parent='destination' if obj['parent']==d['state']['Parent'] else 'none' if not obj['parent'] else 'unexpected';assert parent!='unexpected'
   for key,pose in (('AwakeWorld','position'),('AwakeLocal','localPosition')):
    assert max(abs(state[key][axis]-obj[pose][axis]) for axis in ('x','y','z'))<1e-4,(frame,label,'pose changed after Awake',key)
   for key,pose in (('AwakeRotation','rotation'),('AwakeLocalRotation','localRotation')):
    assert quat_same(state[key],obj[pose]),(frame,label,'rotation changed after Awake')
   assert child['mesh'] and child['material']
   rows[label]={'parent':parent,'awakes':state['Awakes'],'starts':state['Starts'],'internalTransformReference':True,**{k:obj[k] for k in ('position','localPosition','rotation','localRotation','scale','localScale')},'childPose':{k:child[k] for k in ('position','rotation','scale')}}
  register_checkpoint(result,int(frame),tuple(range(3,31,3)))
  result[str(frame)]=rows
 assert len(result)==10,len(result);assert order==['PlacementDriver','RuntimeMiddle','RuntimeFinal'],order
 return result
def quat_same(a,b):return min(sum((a[k]-b[k])**2 for k in ('x','y','z','w')),sum((a[k]+b[k])**2 for k in ('x','y','z','w')))<1e-8
def compare(a,b,path=''):
 if isinstance(a,dict):
  assert set(a)==set(b),(path,'keys')
  if set(a)==set('xyzw'):assert quat_same(a,b),(path,a,b)
  else:
   for k in a:compare(a[k],b[k],path+'/'+k)
 elif isinstance(a,(int,float)) and not isinstance(a,bool):assert math.isclose(a,b,rel_tol=1e-5,abs_tol=1e-4),(path,a,b)
 else:assert a==b,(path,a,b)
if __name__=='__main__':
 run_comparison(sys.argv[1:],normalize,lambda rows:None,lambda expected,actual,path:{'creationModes': 10, 'sourceKinds': ['prefab', 'scene'], 'beforeAwakePoseAndParentVerified': True, 'quaternionAndTransformReferencesVerified': True},'RUNTIME_PLACEMENT_EXECUTION_COMPARISON_PASS',comparison=compare,dependencies=(Path(__file__).with_name('compare-runtime-world.py'),))
