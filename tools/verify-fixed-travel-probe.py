"""Validate real scene-travel captures, scoped launch receipts and build identity."""
from pathlib import Path
import json,math
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/fixed-travel-check';unity=root/'artifacts/runtime-ui-api-tests';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
bindings={}
def bind(name,kind,folder,capture=True,base=None,method='EMFixedTravelProbe.Run'):
 h=base or home;p=h/(name+'.json') if capture else None;matches=[]
 for rp in folder.glob('engine-preflight-*.json'):
  r=load(rp)
  try:validate_receipt(r,kind,h/(name+'.log'),p,root,unity_method=method)
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  matches.append((r['finishedUtc'],rp))
 require(bool(matches),'Scoped successful receipt missing: '+name)
 rp=sorted(matches)[-1][1];bindings[str(h.name)+'/'+name]={'receipt':str(rp),'receiptSha256':sha(rp),'captureSha256':sha(p) if p else None}
def rows(name,empty=False):
 d=load(home/(name+'.json'));r=d.get('rows');expect=[(s,f)for s,n in [(0,7),(1,5 if empty else 7),(2,12)]for f in range(1,n+1)]
 require(d.get('status')=='CAPTURED' and isinstance(r,list) and len(r)==len(expect),'Incomplete scene capture '+name)
 for a,key in zip(r,expect):
  require(isinstance(a,list) and len(a)==7 and tuple(a[:2])==key and all(type(x)in(int,float) and math.isfinite(x)for x in a),'Invalid rows '+name)
  require(all(x==int(x)for i,x in enumerate(a)if i!=5),'Nonintegral scene state '+name)
  require(a[6]==1 and (a[2]==1 or empty and a[0]==1),'Start-before-fixed lifecycle '+name)
 return r
def compare(a,b,label,floats={5}):
 require(len(a)==len(b),'Capture length mismatch '+label)
 for i,(x,y)in enumerate(zip(a,b)):
  require(len(x)==len(y),'Row width mismatch '+label)
  for j,(v,w)in enumerate(zip(x,y)):require(abs(v-w)<=(2e-6 if j in floats else 0),f'{label} row={i} column={j}: {v} != {w}')
for case,empty in [('100-20',False),('30-10',False),('100-20',True)]:
 modes=['originalempty','nativeempty','restoredempty']if empty else ['original','native','restored']
 values=[rows(m+'-'+case,empty)for m in modes]
 for m,v in zip(modes,values):
  compare(values[0],v,m+'-'+case);bind(m+'-'+case,'Unreal'if m.startswith('native')else'Unity',core if m.startswith('native')else unity)
old=root/'artifacts/fixed-update-check'
for case in ['100-20','50-20','30-20','30-10']:
 a=load(old/('original-'+case+'.json'))['rows'];b=load(home/('single-'+case+'.json'))['rows'];require(len(a)==24 and len(b)==24,'Single-world regression incomplete');compare(a,b,'single-'+case,{1,9,10,11});bind('single-'+case,'Unreal',core);bind('original-'+case,'Unity',unity,base=old,method='EMFixedProbe.Run')
bind('build','Compiler',core,False)
for p,digest in load(home/'build-hashes.json').items():require(sha(Path(p)).lower()==digest.lower(),'Build changed '+p)
require(sha(home/'restored.cs')==sha(home/'before-fix-restored.cs.txt'),'Restored source no longer matches engine-tested baseline')
record={'status':'ACTUAL_FIXED_TRAVEL_ROUNDTRIP_PASS','goalComplete':False,'travelScenarios':3,'travelCaptures':9,'singleWorldNativeRegressions':4,'emptyMiddleFrames':5,'emptyMiddleTravel':'test observer initiates final scene load in both engines','bindings':bindings,'physicsSubstepEquivalence':False}
(home/'verification.json').write_text(json.dumps(record,indent=2));print(record['status'])
