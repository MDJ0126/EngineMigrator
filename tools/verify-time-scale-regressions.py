"""Gate existing fixed-event and scene-travel regressions against newly built engine module."""
from pathlib import Path
import json,math
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;h=root/'artifacts/time-scale-check';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
bindings={}
def bind(name):
 matches=[];p=h/(name+'.json')
 for rp in core.glob('engine-preflight-*.json'):
  r=load(rp)
  try:validate_receipt(r,'Unreal',h/(name+'.log'),p,root)
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  matches.append((r['finishedUtc'],rp))
 require(bool(matches),'New regression receipt missing '+name);rp=sorted(matches)[-1][1];bindings[name]={'receipt':str(rp),'receiptSha256':sha(rp),'captureSha256':sha(p)}
def compare(name,reference,width,count,floats):
 d=load(h/(name+'.json'));r=d.get('rows');a=load(reference)['rows'];require(d.get('status')=='CAPTURED'and type(r)is list and len(r)==count and len(a)==count,'Regression capture incomplete '+name)
 for i,(x,y)in enumerate(zip(a,r)):
  require(type(y)is list and len(y)==width and all(type(v)in(int,float)and math.isfinite(v)for v in y),'Invalid regression row '+name)
  for j,(v,w)in enumerate(zip(x,y)):require(abs(v-w)<=(2e-6 if j in floats else 0),f'{name} row={i} col={j}: {v}/{w}')
 bind(name)
for case in ['100-20','50-20','30-20','30-10']:compare('single-'+case,root/('artifacts/fixed-update-check/original-'+case+'.json'),16,24,{1,9,10,11})
for case in ['100-20','30-10']:compare('travel-'+case,root/('artifacts/fixed-travel-check/original-'+case+'.json'),7,26,{5})
compare('travelempty-100-20',root/'artifacts/fixed-travel-check/originalempty-100-20.json',7,24,{5})
for p,d in load(h/'build-hashes.json').items():require(sha(Path(p)).lower()==d.lower(),'Current build no longer matches '+p)
record={'status':'TIME_SCALE_NATIVE_REGRESSIONS_PASS','singleWorldCases':4,'travelCases':3,'newNativeCaptures':7,'unityBaselinesReused':True,'bindings':bindings};(h/'regression-verification.json').write_text(json.dumps(record,indent=2));print(record['status'])
