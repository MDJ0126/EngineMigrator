"""Validate actual nested lifecycle captures, callback order, scoped execution and build identity."""
from pathlib import Path
import json,math,re
from object_equality_evidence import require,validate_receipt,sha
r=Path(__file__).resolve().parent.parent;a=r/'artifacts';h=a/'phase-chain-check';u=a/'runtime-ui-api-tests';core=Path(json.loads((a/'core-engine-launch-tests/paths.json').read_text())['run']);bindings={}
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def bind(name,kind,capture=True):
 p=h/(name+'.json')if capture else None;found=[]
 for rp in(u if kind=='Unity'else core).glob('engine-preflight-*.json'):
  x=load(rp)
  try:validate_receipt(x,kind,h/(name+'.log'),p,r,unity_method='EMPhaseChainProbe.Run')
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  found.append((x['finishedUtc'],rp))
 require(bool(found),'Scoped successful receipt missing '+name);rp=sorted(found)[-1][1];bindings[name]={'receipt':str(rp),'receiptSha256':sha(rp),'captureSha256':sha(p)if p else None}
def rows(name):
 d=load(h/(name+'.json'));v=d.get('rows');require(d.get('status')=='CAPTURED'and type(v)is list and len(v)==24,'Incomplete '+name)
 for i,row in enumerate(v,1):require(type(row)is list and len(row)==47 and row[0]==i and all(type(x)in(int,float)and math.isfinite(x)and x==int(x)for x in row),'Malformed integer phase state '+name)
 require(v[1][6:9]==[1,0,1]and v[1][14:17]==[1,0,1],'Post-update Start chain not covered '+name)
 require(v[12][28:31]==[1,1,1]and v[12][36:39]==[1,1,1],'Pre-update Start chain not covered '+name)
 for frame,late,enable in [(8,8,9),(10,8,9),(16,30,31),(18,30,31)]:require(v[frame-1][late]==v[frame-2][late]and v[frame-1][enable]==v[frame-2][enable]+1,'Same-phase toggle not covered '+name)
 require(v[19][3]==1 and v[20][3]==0 and v[21][25]==1 and v[22][25]==0,'Deferred root destruction not covered '+name)
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
proof=load(h/'source-verification.json');require(proof['status']=='CURRENT_GENERATED_NATIVE_AND_EXECUTED_ORIGINAL_RESTORED_MATCH','Actual source identity gate missing')
require(proof['coreSha256'].lower()==sha(r/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'),'Source verifier and tested core differ')
require(sha(h/'runner/bin/Release/net9.0/EngineMigrator.Core.dll')==sha(r/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'),'Engine generator and tested core differ')
for entry in proof['originalClasses']:require(sha(r/entry['source']).lower()==entry['sha256'].lower(),'Executed original source changed '+entry['source'])
result={'status':'ACTUAL_PHASE_CHAIN_ROUNDTRIP_PASS','goalComplete':False,'captures':6,'frames':24,'values':47,'postUpdateAndPreUpdateNestedStarts':True,'sameLateToggleCases':4,'scriptOrderingAfterOwnerDestruction':True,'engineVersions':['Unity 6000.0.83f1','Unreal 5.8'],'noLateBundleNestedStartsVerified':False,'bindings':bindings};(h/'verification.json').write_text(json.dumps(result,indent=2));print(result['status'])
