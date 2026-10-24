import importlib.util,json,sys
from pathlib import Path
from runtime_capture_validation import register_checkpoint, run_comparison
spec=importlib.util.spec_from_file_location('world',Path(__file__).with_name('compare-runtime-world.py'));world=importlib.util.module_from_spec(spec);spec.loader.exec_module(world)
def summarize(path):
 data=world.load(path);order=[];checkpoints={}
 for f in data['frames']:
  actors=f['actors'];drivers=[a for a in actors if a['class'] in ('RuntimeSpawner','RuntimeMiddle','RuntimeFinal')]
  if not drivers:continue
  assert len(drivers)==1;driver=drivers[0];kind=driver['class'];frame=int(driver['state']['Frame'])
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimeSpawner' or frame not in (10,12,18,20,22,30,32,38):continue
  register_checkpoint(checkpoints,frame,(10,12,18,20,22,30,32,38))
  state=driver['state'];objects={o['id']:o for o in f['objects']};templates={o['id']:o for o in f['templates']};counters=[a for a in actors if a['class']=='RuntimeTemplateCounter'];removed=frame in (20,22)
  assert len(counters)==(0 if removed else 1),(frame,'live prefab counter count',len(counters))
  assert state['Source'] not in objects and state['Source'] in templates,('prefab asset mistaken for a scene instance',frame)
  definition=templates[state['Source']];children=[o for o in templates.values() if o['parent']==definition['id']];assert len(children)==1;child=children[0]
  row={'liveInstances':len(counters),'prefabDefinitionSeparate':True,'firstCreated':state['FirstCreated'],'removed':state['Removed'],'secondCreated':state['SecondCreated'],'aliveImmediatelyAfterDestroy':state['AliveImmediatelyAfterDestroy']}
  if not removed:
   clone=counters[0];assert clone['owner']==state['Spawned'];cloneRoot=objects[clone['owner']];cloneChild=objects[clone['state']['Target']];assert cloneChild['parent']==cloneRoot['id'];assert clone['state']['External']==''
   assert not cloneRoot['hidden'] and not cloneChild['hidden']
   assert clone['state']['Awakes']==1 and clone['state']['Starts']==1
   for actual,expected in ((cloneRoot,definition),(cloneChild,child)):
    assert all(abs(actual['position'][k]-expected['position'][k])<1e-5 for k in ('x','y','z'))
   assert child['mesh'] and child['material'];assert cloneChild['mesh']==child['mesh'] and cloneChild['material']==child['material']
   if frame not in (10,30):assert clone['state']['Updates']>0
   row.update(internalReferenceRemapped=True,meshAndMaterialShared=True,liveHierarchyVisible=True,cloneAwakes=1,cloneStarts=1,transformsPreserved=True)
  else:assert not state['Spawned'] or state['Spawned'] not in objects;row['hierarchyRemoved']=True
  checkpoints[str(frame)]=row
 assert list(checkpoints)==['10','12','18','20','22','30','32','38'];assert checkpoints['20']['aliveImmediatelyAfterDestroy'] is True
 assert order==['RuntimeSpawner','RuntimeMiddle','RuntimeFinal'];return {'checkpoints':checkpoints,'sceneExecutionOrder':order}
if __name__=='__main__':
 def metadata(expected,actual,path):return dict(checkpoints=actual['checkpoints'],sceneExecutionOrder=actual['sceneExecutionOrder'],actualPrefabInstantiated=True)
 run_comparison(sys.argv[1:],summarize,lambda rows:None,metadata,'RUNTIME_PREFAB_EXECUTION_COMPARISON_PASS',dependencies=(Path(__file__).with_name('compare-runtime-world.py'),))
