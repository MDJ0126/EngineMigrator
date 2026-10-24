"""Gate late-event implementation on real update/late order and lifecycle captures."""
from pathlib import Path
import json,math,re
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;h=root/'artifacts/late-update-check';unity=root/'artifacts/runtime-ui-api-tests';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
bindings={};floating={1,11,12,20,21}
def bind(name,kind,folder,capture=True):
 p=h/(name+'.json')if capture else None;matches=[]
 for rp in folder.glob('engine-preflight-*.json'):
  r=load(rp)
  try:validate_receipt(r,kind,h/(name+'.log'),p,root,unity_method='EMLateProbe.Run')
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  matches.append((r['finishedUtc'],rp))
 require(bool(matches),'Successful scoped receipt missing '+name);rp=sorted(matches)[-1][1];bindings[name]={'receipt':str(rp),'receiptSha256':sha(rp),'captureSha256':sha(p)if p else None}
def rows(name):
 d=load(h/(name+'.json'));r=d.get('rows');require(d.get('status')=='CAPTURED'and type(r)is list and len(r)==24,'Incomplete late capture '+name)
 for i,a in enumerate(r,1):
  require(type(a)is list and len(a)==25 and a[0]==i and all(type(v)in(int,float)and math.isfinite(v)for v in a),'Malformed late rows '+name)
  require(all(v==int(v)for j,v in enumerate(a)if j not in floating),'Non-integral late state '+name)
  require(a[23:25]==[i,i],'Final ordered actor missed update/late phase '+name)
  for start in [5,14]:
   if a[start]:require(a[start+1]==1 and a[start+2]==a[start+3]and a[start+4]==a[start+5]and a[start+8]==1,'Inherited calls/start-before-late differ '+name)
 require(r[6][7]==r[5][7] and r[6][9]==r[5][9]+1,'Re-enabled late same frame not covered '+name)
 require(r[12][7]==r[11][7] and r[12][9]==r[11][9]+1,'Reactivated GameObject late phase not covered '+name)
 require(r[14][14:20]==[1,1,0,0,1,1],'Created component first late/start ordering differs '+name)
 require(r[17][1]==0 and r[18][1]==0 and r[19][1]==0 and r[19][18]==r[16][18]+3,'Paused late callbacks did not continue '+name)
 require(r[20][14]==0 and r[22][5]==0,'Destroyed component late target remained live '+name)
 return r
for case in ['100-20','30-20']:
 data={mode:rows(mode+'-'+case)for mode in ['original','native','restored']}
 for mode,r in data.items():
  for i,(a,b)in enumerate(zip(data['original'],r)):
   for j,(x,y)in enumerate(zip(a,b)):require(abs(x-y)<=(2e-6 if j in floating else 0),f'{mode}-{case} frame={i+1} column={j}: {x}/{y}')
  bind(mode+'-'+case,'Unreal'if mode=='native'else'Unity',core if mode=='native'else unity)
bind('build','Compiler',core,False)
for p,d in load(h/'build-hashes.json').items():require(sha(Path(p)).lower()==d.lower(),'Native build changed '+p)
for file in(h/'restored').glob('*.cs'):
 s=file.read_text()
 for name in ['LateController','LateLedger','LateWorker','LateBase','LateTail']:s=re.sub(r'\b'+name+r'\b','Restored'+name,s)
 require((unity/'UnitySource/Assets/EngineMigratorLateProbe'/('Restored'+file.name)).read_text()==s,'Actually executed restored class changed '+file.name)
require(sha(root/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll')==sha(h/'runner/bin/Release/net9.0/EngineMigrator.Core.dll'),'Test and engine generator cores differ')
record={'status':'ACTUAL_LATE_UPDATE_ROUNDTRIP_PASS','goalComplete':False,'scenarios':2,'actualCaptures':6,'framesPerCapture':24,'valuesPerFrame':25,'allUpdatesBeforeLateOrderedActor':True,'createdComponentFirstLateSameFrame':True,'reenableAndReactivateSameFrameLate':True,'pauseAndDestroyVerified':True,'bindings':bindings,'prefabAndSceneTransitionLateVerified':False};(h/'verification.json').write_text(json.dumps(record,indent=2));print(record['status'])
