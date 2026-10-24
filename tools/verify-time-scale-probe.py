"""Verify time scale using real engine observations and exact event counters."""
from pathlib import Path
import json,math
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/time-scale-check';unity=root/'artifacts/runtime-ui-api-tests';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
bindings={}
def bind(name,kind,folder,capture=True):
 matches=[];path=home/(name+'.json')if capture else None
 for rp in folder.glob('engine-preflight-*.json'):
  r=load(rp)
  try:validate_receipt(r,kind,home/(name+'.log'),path,root,unity_method='EMTimeScaleProbe.Run')
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  matches.append((r['finishedUtc'],rp))
 require(bool(matches),'Scoped receipt missing '+name);rp=sorted(matches)[-1][1];bindings[name]={'receipt':str(rp),'receiptSha256':sha(rp),'captureSha256':sha(path)if path else None}
def rows(name):
 d=load(home/(name+'.json'));r=d.get('rows');require(d.get('status')=='CAPTURED'and type(r)is list and len(r)==22,'Capture incomplete '+name)
 for i,a in enumerate(r,1):
  require(type(a)is list and len(a)==14 and a[0]==i and all(type(x)in(int,float)and math.isfinite(x)for x in a),'Malformed time scale rows '+name)
  require(a[2]==1 and a[9]==i and a[10]==a[1],'Start, peer frame or shared fixed clock differs '+name)
  require(a[11]==a[4] and abs(a[12]-a[5])<2e-6,'Cross-script scale visibility or within-frame delta differs '+name)
 for a in r[8:12]:require(a[1]==r[7][1] and a[5]==0 and a[7]==r[7][7],'Pause incorrectly advances fixed calls/time '+name)
 require(r[3][3:5]==[1,.5] and r[7][3:5]==[.5,0] and r[11][3:5]==[0,2] and r[15][3:5]==[2,1],'Rate transitions missing '+name)
 require(r[17][3:5]==[1,1] and r[-1][4]==1,'Negative time scale was not ignored '+name)
 return r
for case in ['100-20','30-20']:
 data={m:rows(m+'-'+case)for m in ['original','native','restored']}
 for mode,r in data.items():
  for i,(a,b)in enumerate(zip(data['original'],r)):
   for j,(x,y)in enumerate(zip(a[:13],b[:13])):require(abs(x-y)<=(0 if j in {0,1,2,3,4,9,10,11}else 2e-6),f'{mode}-{case} frame={i+1} column={j}: {x}/{y}')
  bind(mode+'-'+case,'Unreal'if mode=='native'else'Unity',core if mode=='native'else unity)
bind('build','Compiler',core,False)
for p,d in load(home/'build-hashes.json').items():require(sha(Path(p)).lower()==d.lower(),'Native build changed '+p)
f=unity/'UnitySource/Assets/EngineMigratorTimeScaleProbe'
source=(root/'tests/EngineMigrator.Checks/Fixtures/time-scale.cs.txt').read_text(encoding='utf-8-sig');a,b=source.split('public class TimeScalePeer',1)
require((f/'TimeScaleActor.cs').read_text()==a and (f/'TimeScalePeer.cs').read_text()=='using UnityEngine;\npublic class TimeScalePeer'+b,'Actual Unity fixture source changed')
for name,file in [('TimeScaleActor','restored.cs'),('TimeScalePeer','restored-peer.cs')]:require((f/('Restored'+name+'.cs')).read_text()==(home/file).read_text().replace(name,'Restored'+name),'Actual restored source changed '+name)
record={'status':'ACTUAL_TIME_SCALE_ROUNDTRIP_PASS','goalComplete':False,'scenarios':2,'actualCaptures':6,'framesPerCapture':22,'comparedValuesPerFrame':13,'rates':[1,.5,0,2,1],'negativeIgnoredVerified':True,'crossScriptVisibilityVerified':True,'unscaledDeltaCompared':False,'unscaledDeltaReason':'Unity captureDeltaTime keeps real wall time unscaled; native fixed-step runner uses synthetic frame duration. Column 13 is diagnostic only; unscaledDeltaTime API conversion is not implemented.','bindings':bindings,'sourceHashes':{str(p.relative_to(root)):sha(p)for p in [root/'src/EngineMigrator.Core/TimeScalePorting.cs',root/'src/EngineMigrator.Core/ActorScripts.cs',root/'tests/EngineMigrator.Checks/Fixtures/time-scale.cs.txt',home/'restored.cs',home/'restored-peer.cs']}}
(home/'verification.json').write_text(json.dumps(record,indent=2));print(record['status'])
