"""Compare observed engine execution, including actual clone topology and references."""
import json,sys
from pathlib import Path
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison
def load(path):
 data=load_capture(path,case_insensitive_state=False)
 for f in data['frames']:
  for a in f['actors']:
   if 'className' in a:
    a['class']=a.pop('className');a['state']={v['name']:(v['value'].lower()=='true' if v['boolean'] else int(v['value']) if v['value'].lstrip('-').isdigit() and v['name'] not in ('Source','Spawned','Target','External','Prefab','SceneSource','Parent','LastPrefab','LastScene','TargetTransform','ParentAtAwake') else v['value']) for v in a['state']}
 return data
def summary(data):
 out={};order=[]
 for f in data['frames']:
  actors=f['actors'];drivers=[a for a in actors if a['class'] in ('RuntimeSpawner','RuntimeMiddle','RuntimeFinal')]
  if not drivers:continue
  assert len(drivers)==1;driver=drivers[0];kind=driver['class'];frame=int(driver['state']['Frame'])
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimeSpawner' or frame not in (10,12,18,20,22,30,32,38):continue
  register_checkpoint(out,frame,(10,12,18,20,22,30,32,38))
  state=driver['state'];counters=[a for a in actors if a['class']=='RuntimeTemplateCounter'];objects={o['id']:o for o in f['objects']}
  expected=1 if frame in (20,22) else 2;assert len(counters)==expected,(frame,'counter count',len(counters))
  original=next(a for a in counters if a['owner']==state['Source']);assert original['state']['External']==driver['owner']
  assert objects[original['state']['Target']]['parent']==original['owner'],('original child',frame)
  item={'counters':len(counters),'firstCreated':state['FirstCreated'],'removed':state['Removed'],'secondCreated':state['SecondCreated'],'aliveImmediatelyAfterDestroy':state['AliveImmediatelyAfterDestroy'],'originalAwakes':original['state']['Awakes'],'originalStarts':original['state']['Starts']}
  if frame not in (20,22):
   clone=next(a for a in counters if a['owner']==state['Spawned']);assert clone['owner']!=original['owner']
   assert clone['state']['Target']!=original['state']['Target']
   assert objects[clone['state']['Target']]['parent']==clone['owner'],('clone internal reference not remapped',frame)
   assert clone['state']['External']==driver['owner'],('external reference changed',frame)
   for a,b in ((clone['owner'],original['owner']),(clone['state']['Target'],original['state']['Target'])):
    assert all(abs(objects[a]['position'][k]-objects[b]['position'][k])<1e-5 for k in ('x','y','z')),('clone transform changed',frame)
   if frame not in (10,30):assert clone['state']['Updates']>0
   item.update(cloneAwakes=clone['state']['Awakes'],cloneStarts=clone['state']['Starts'],internalReferenceRemapped=True,externalReferencePreserved=True,transformsPreserved=True)
  else:
   assert not state['Spawned'] or state['Spawned'] not in objects
   assert len([o for o in objects.values() if o['parent']==original['owner']])==1
   item['cloneHierarchyRemoved']=True
  out[str(frame)]=item
 assert list(out)==['10','12','18','20','22','30','32','38'],list(out)
 assert out['20']['aliveImmediatelyAfterDestroy'] is True
 assert order==['RuntimeSpawner','RuntimeMiddle','RuntimeFinal'],order
 assert any(a['class']=='RuntimeFinal' and int(a['state']['Frame'])>=5 for f in data['frames'] for a in f['actors'])
 return {'checkpoints':out,'sceneExecutionOrder':order}
if __name__=='__main__':
 def summarize(path):return summary(load(path))
 def metadata(expected,actual,path):return dict(checkpoints=actual['checkpoints'],sceneExecutionOrder=actual['sceneExecutionOrder'],actualEngineWorldTravel=True,cloneHierarchyVerified=True)
 run_comparison(sys.argv[1:],summarize,lambda rows:None,metadata,'RUNTIME_WORLD_EXECUTION_COMPARISON_PASS')
