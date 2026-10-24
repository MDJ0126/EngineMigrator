"""Compare real no-LateUpdate three-generation startup captures and audited receipts."""
from pathlib import Path
import json,math
from object_equality_evidence import require,validate_receipt,sha
r=Path(__file__).resolve().parent.parent;a=r/'artifacts';h=a/'start-chain-check';u=a/'runtime-ui-api-tests';core=Path(json.loads((a/'core-engine-launch-tests/paths.json').read_text())['run']);bindings={}
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def bind(name,kind,capture=True):
 p=h/(name+'.json')if capture else None;found=[]
 for rp in(u if kind=='Unity'else core).glob('engine-preflight-*.json'):
  x=load(rp)
  try:validate_receipt(x,kind,h/(name+'.log'),p,r,unity_method='EMBootProbe.Run')
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  found.append((x['finishedUtc'],rp))
 require(bool(found),'Scoped successful receipt missing '+name);rp=sorted(found)[-1][1];bindings[name]={'receipt':str(rp),'sha256':sha(rp),'captureSha256':sha(p)if p else None}
def rows(name):
 d=load(h/(name+'.json'));v=d.get('rows');require(d.get('status')=='CAPTURED'and type(v)is list and len(v)==24,'Incomplete '+name)
 for i,row in enumerate(v,1):require(type(row)is list and len(row)==65 and row[0]==i and all(type(x)in(int,float)and math.isfinite(x)and x==int(x)for x in row),'Malformed integer state '+name)
 grand_updates={'100':0,'30':1}[name.split('-')[1]]
 require(v[0][6:8]==[1,1]and v[0][13:15]==[1,1]and v[0][19:21]==[1,grand_updates],'Startup generation timing not covered '+name)
 require(v[1][37:39]==[1,0]and v[1][44:46]==[0,0]and v[2][50:52]==[1,1],'Post-Update nested startup not covered '+name)
 require(v[12][3]==1 and v[13][3]==0 and v[16][34]==1 and v[17][34]==0,'Owner destruction not covered '+name)
 return v
for case in ['100-20','30-20']:
 first=rows('original-'+case)
 for mode in ['original','restored','native']:
  name=mode+'-'+case;got=rows(name)
  for i,(x,y)in enumerate(zip(first,got)):
   for j,(v,w)in enumerate(zip(x,y)):require(v==w,f'{name} frame={i+1} column={j}: {v}/{w}')
  bind(name,'Unreal'if mode=='native'else'Unity')
for path,digest in load(h/'build-hashes.json').items():require(sha(Path(path)).lower()==digest.lower(),'Compiled source/binary changed '+path)
bind('build','Compiler',False)
proof=load(h/'source-verification.json');require(proof['status']=='CURRENT_START_QUEUE_SOURCES_MATCH','Current source identity missing')
require(proof['coreSha256']==sha(r/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'),'Source verifier/test core differs')
for p,d in proof['files'].items():require(sha(Path(p))==d.lower(),'Executed source changed '+p)
result={'status':'ACTUAL_START_CHAIN_ROUNDTRIP_PASS','goalComplete':False,'captures':6,'frames':24,'integerValues':65,'classOrderAndDistinctLifecycleEventsCompared':True,'sameClassUpdateInstanceOrderGuaranteed':False,'orderContract':'https://docs.unity3d.com/6000.0/Documentation/Manual/script-execution-order.html','snapshot':'Unity LateUpdate / Unreal post-actor tick, before world-end startup generation','noLateUpdateInConvertedBundle':True,'bindings':bindings}
(h/'verification.json').write_text(json.dumps(result,indent=2));print(result['status'])
