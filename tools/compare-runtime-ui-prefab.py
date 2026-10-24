"""Compare real button delegate execution and prefab-scoped targets across engines."""
import json,sys
from pathlib import Path
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison
INSTANCES=('First','Second','Copy','Sleeping','Hidden','Passive')
REFS={'Source','Dormant','Disabled','Quiet',*INSTANCES,'A','B','C','D','E','F'}
def load(path):
 return load_capture(path)

def summarize(path):
 data=load(path);rows={};order=[];native=any('class' in a for f in data['frames'] for a in f['actors'])
 transition_viewports=[];global_viewports=True
 for frame in data['frames']:
  drivers=[a for a in frame['actors'] if a.get('class',a.get('className')) in ('RuntimeUIDriver','RuntimeMiddle','RuntimeFinal')]
  if len(drivers)>1:raise ValueError('Multiple runtime drivers in one captured frame')
  driver=drivers[0] if drivers else None
  if not driver:continue
  kind=driver.get('class',driver.get('className'))
  if not order or order[-1]!=kind:order.append(kind)
  state=driver['state'];state={r['name']:r['value'] for r in state} if isinstance(state,list) else state
  state={k.casefold():str(v) for k,v in state.items()}
  if kind!='RuntimeUIDriver':
   assert not frame.get('ui'),('UI presenter survived scene transition',kind)
   if native:transition_viewports.append(frame.get('uiViewportWidgets',-1));global_viewports=global_viewports and frame.get('uiViewportWorldIndependent',False)
   continue
  number=int(state['frame'])
  if number not in range(1,21):continue
  register_checkpoint(rows,number)
  ui={r['owner']:r for r in frame['ui']};instances={}
  names=tuple(k for k in INSTANCES if k.casefold() in state)
  alive={state[k.casefold()] for k in names if state[k.casefold()]}
  assert set(ui)==alive,(number,'UI scope/presenter count differs',set(ui),alive)
  for name in names:
   identity=state[name.casefold()];row={'alive':bool(identity)}
   if identity:
    actual=ui[identity];row.update({k:v for k,v in actual.items() if k not in ('owner','hasWidget','text','interactable','canvasEnabled','buttonEnabled')})
    if actual['visible']:row.update(text=actual['text'],interactable=actual['interactable'])
    if native:
     expected_widget=name!='Hidden' and not(name=='Sleeping' and number==8)
     assert actual['hasWidget']==expected_widget,(number,name,'unexpected widget lifetime',actual)
   instances[name]=row
  if native:
   assert frame['uiViewportWidgets']==sum(r.get('hasWidget',False) for r in ui.values()),(number,'leaked or prototype viewport widgets',frame['uiViewportWidgets'],ui)
  rows[str(number)]=instances
 assert len(rows)==20,len(rows)
 assert order==['RuntimeUIDriver','RuntimeMiddle','RuntimeFinal'],order
 if native and rows['20']['Sleeping']['alive']:
  assert global_viewports and transition_viewports and all(v==0 for v in transition_viewports),('Old-world viewport widget survived transition',transition_viewports)
 return rows
def semantics(rows):
 assert rows['5']['First']['hits']==2 and rows['5']['Second']['hits']==1
 assert rows['6']['Copy']['hits']==2
 assert rows['11']['First']['hits']==3 and not rows['11']['First']['visible']
 assert rows['13']['Second']['hits']==3 and not rows['13']['Second']['buttonActive']
 assert rows['18']['Copy']['hits']>rows['16']['Copy']['hits'] and not rows['18']['First']['alive']
 assert rows['19']['Hidden']['hits']==0 and not rows['19']['Hidden']['visible']
 for row in rows.values():
  for instance in row.values():
   if instance['alive']:
    assert instance['offHits']==0 and instance['hits']==instance['childHits'],instance
    assert instance['order']==(12 if instance['hits'] else 0),instance
 assert all(not row['alive'] for name,row in rows['20'].items() if name not in ('Hidden','Sleeping'))
 if rows['20']['Sleeping']['alive']:assert rows['20']['Sleeping']['visible']
 if 'Passive' in rows['16']:assert rows['16']['Passive']['visible'] and rows['16']['Passive']['hits']==0
if __name__=='__main__':
 def metadata(expected,actual,path):
  native=any('class' in a for f in load(path)['frames'] for a in f['actors'])
  return {'callbackIsolationVerified':True,'sceneTransitionWithVisibleWidgetVerified':expected['20']['Sleeping']['alive'],'nativeViewportCleanupVerified':native and expected['20']['Sleeping']['alive']}
 run_comparison(sys.argv[1:],summarize,semantics,metadata,'RUNTIME_UI_PREFAB_EXECUTION_COMPARISON_PASS')
