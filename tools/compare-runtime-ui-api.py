"""Compare UI component access, independent instances and external callbacks."""
from pathlib import Path
import json,sys
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison
INSTANCES={'First':'A','Second':'B','Copy':'C','Sleeping':'D','Hidden':'E','Passive':'F','Scene':'SceneCounter','SceneCopy':'SceneCopyCounter'}
def load(path):
 return load_capture(path)

def state(actor):
 s=actor['state'];return {r['name'].casefold():r['value'] for r in s} if isinstance(s,list) else {k.casefold():v for k,v in s.items()}
def scalar(v):
 if isinstance(v,str):
  if v.lower() in ('true','false'):return v.lower()=='true'
  return int(v)
 return v
def summarize(path):
 data=load(path);native=any('class' in a for f in data['frames'] for a in f['actors']);rows={};order=[]
 for f in data['frames']:
  drivers=[a for a in f['actors'] if a.get('class',a.get('className')) in ('RuntimeUIApiDriver','RuntimeMiddle','RuntimeFinal')]
  if len(drivers)>1:raise ValueError('Multiple runtime drivers in one captured frame')
  driver=drivers[0] if drivers else None
  if not driver:continue
  kind=driver.get('class',driver.get('className'))
  if not order or order[-1]!=kind:order.append(kind)
  if kind!='RuntimeUIApiDriver':
   assert not f['ui']
   if native:assert f['uiViewportWorldIndependent'] and f['uiViewportWidgets']==0
   continue
  s=state(driver);number=int(s['frame'])
  if number not in range(1,21):continue
  register_checkpoint(rows,number)
  actors={a['id']:a for a in f['actors']};ui={a['owner']:a for a in f['ui']};items={};alive={str(s[n.casefold()]) for n in INSTANCES if s[n.casefold()]};assert set(ui)==alive,(number,'presenter scope mismatch',ui,alive)
  for name,component in INSTANCES.items():
   identity=str(s[name.casefold()]);row={'alive':bool(identity)}
   if identity:
    u=ui[identity];row.update({k:v for k,v in u.items() if k not in ('owner','hasWidget')})
    if not u['visible']:row.pop('text',None)
    a=actors.get(str(s[component.casefold()]))
    if a and any(o['id']==identity and not o.get('hidden',False) for o in f['objects']):
     values=state(a);row['script']={k:scalar(values[k]) for k in ('samecanvas','samebutton','seencanvas','seenenabled','seeninteractable')}
     assert str(values['canvascomponent'])==identity,(number,name,'Canvas reference did not remap')
     button_id=str(values['button']);assert str(values['actioncomponent'])==button_id,(number,name,'Button reference did not remap')
   items[name]=row
  if native:assert f['uiViewportWidgets']==sum(u['hasWidget'] for u in ui.values()),(number,'viewport widget leak')
  external=state(actors[str(s['outside'])]);rows[str(number)]={'instances':items,'externalHits':scalar(external['hits'])}
 assert len(rows)==20 and order==['RuntimeUIApiDriver','RuntimeMiddle','RuntimeFinal'],(len(rows),order)
 return rows
def semantics(rows):
 def item(frame,name):return rows[str(frame)]['instances'][name]
 assert not item(3,'First')['canvasEnabled'] and not item(3,'First')['visible']
 assert not item(5,'First')['interactable'] and not item(7,'First')['buttonEnabled']
 assert item(16,'Hidden')['canvasEnabled'] and item(16,'Hidden')['visible']
 assert not item(18,'Scene')['alive'] and item(18,'SceneCopy')['hits']>item(16,'SceneCopy')['hits']
 assert rows['18']['externalHits']>rows['16']['externalHits']
 assert item(20,'Sleeping')['visible'] and item(20,'Hidden')['visible']
 for row in rows.values():
  for name,item_ in row['instances'].items():
   if item_['alive']:
    assert item_['offHits']==0 and item_['hits']==item_['childHits']
 assert item(4,'First')['script']['samecanvas'] and item(4,'First')['script']['samebutton']
if __name__=='__main__':
 run_comparison(sys.argv[1:],summarize,semantics,lambda expected,actual,path:{'uiTypedReferencesVerified':True,'externalCallbackPreserved':True},'RUNTIME_UI_API_EXECUTION_COMPARISON_PASS')
