"""Gate existing fixed-event and scene-travel regressions against newly built engine module."""
from pathlib import Path
import json,math
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;h=root/'artifacts/fixed-spawn-check';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
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
for case in ['100-20','30-20']:
 name='rate-'+case;data=load(h/(name+'.json'));r=data.get('rows');a=load(root/('artifacts/time-scale-check/original-'+case+'.json'))['rows'];require(data.get('status')=='CAPTURED'and type(r)is list and len(r)==22,'Rate regression incomplete '+name)
 for i,(x,y)in enumerate(zip(a,r)):
  require(len(y)==14,'Invalid rate regression width')
  for j,(v,w)in enumerate(zip(x[:13],y[:13])):require(abs(v-w)<=(0 if j in{0,1,2,3,4,9,10,11}else 2e-6),f'Rate regression {case} frame={i} col={j}: {v}/{w}')
 bind(name)
for case in ['100-20','30-20']:compare('scaled-'+case,root/('artifacts/scaled-travel-check/original-'+case+'.json'),54,32,{9,10,25,26,38,39,51,52})
for case in ['100-20','30-20']:
 compare('late-'+case,root/('artifacts/late-update-check/original-'+case+'.json'),25,24,{1,11,12,20,21})
 compare('prefab-'+case,root/('artifacts/scaled-prefab-check/original-'+case+'.json'),158,24,{2}|{start+offset for start in [8,38,68,98,128]for offset in [14,15,27,28]})
for case in ['100-20','30-20']:
 compare('mutation-'+case,root/('artifacts/late-mutation-check/original-'+case+'.json'),24,24,set())
 compare('scenelate-'+case,root/('artifacts/late-travel-check/original-'+case+'.json'),57,32,{6,7,8,9,10}|{start+offset for start in [15,29,43]for offset in [8,9,10,11,12]})
for case in ['100-20','30-20']:compare('chain-'+case,root/('artifacts/phase-chain-check/original-'+case+'.json'),47,24,set())
for case in ['100-20','30-20']:compare('boot-'+case,root/('artifacts/start-chain-check/original-'+case+'.json'),65,24,set())
for p,d in load(h/'build-hashes.json').items():require(sha(Path(p)).lower()==d.lower(),'Current build no longer matches '+p)
record={'status':'FIXED_SPAWN_NATIVE_REGRESSIONS_PASS','singleWorldCases':4,'rateCases':2,'scaledTravelCases':2,'newNativeCaptures':20,'lateMutationCases':2,'lateSceneCases':2,'lateCases':2,'prefabCases':2,'unityBaselinesReused':True,'bindings':bindings};(h/'regression-verification.json').write_text(json.dumps(record,indent=2));print(record['status'])



