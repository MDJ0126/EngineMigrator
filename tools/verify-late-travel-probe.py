"""Compare real late mutation/scene captures, audited receipts and final compiled identity."""
from pathlib import Path
import json,math,re
from object_equality_evidence import require,validate_receipt,sha
r=Path(__file__).resolve().parent.parent;a=r/'artifacts';h=a/'late-travel-check';m=a/'late-mutation-check';u=a/'runtime-ui-api-tests';core=Path(json.loads((a/'core-engine-launch-tests/paths.json').read_text())['run']);bindings={}
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def bind(home,name,kind,method=None,capture=True):
 p=home/(name+'.json')if capture else None;found=[]
 for rp in (u if kind=='Unity'else core).glob('engine-preflight-*.json'):
  x=load(rp)
  try:validate_receipt(x,kind,home/(name+'.log'),p,r,unity_method=method or 'EMLateTravelProbe.Run')
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  found.append((x['finishedUtc'],rp))
 require(bool(found),'Successful scoped receipt missing '+name);rp=sorted(found)[-1][1];bindings[str(home.name)+'/'+name]={'receipt':str(rp),'receiptSha256':sha(rp),'captureSha256':sha(p)if p else None}
def rows(home,name,length,width,floats=()):
 d=load(home/(name+'.json'));v=d.get('rows');require(d.get('status')=='CAPTURED'and type(v)is list and len(v)==length,'Incomplete '+name)
 for row in v:
  require(type(row)is list and len(row)==width and all(type(x)in(int,float)and math.isfinite(x)for x in row),'Invalid row '+name)
  require(all(x==int(x)for j,x in enumerate(row)if j not in floats),'Noninteger state '+name)
 return v
def equal(first,other,label,floats=()):
 require(len(first)==len(other),'Row count '+label)
 for i,(row,got)in enumerate(zip(first,other)):
  require(len(row)==len(got),'Row width '+label)
  for j,(x,y)in enumerate(zip(row,got)):require(abs(x-y)<=(2e-6 if j in floats else 0),f'{label} row={i} col={j}: {x}/{y}')
for case in ['100-20','30-20']:
 original=rows(m,'original-'+case,24,24)
 require(original[1][7:10]==[0,0,0]and original[2][7:10]==[1,1,1],'Late creation next-frame start/update missing')
 require(original[7][9]==original[6][9]and original[11][9]==original[10][9],'Late reactivation phase boundary not tested')
 require(original[13][6]==1 and original[14][6]==0 and original[14][3]==1,'Late GameObject deferred destruction not covered')
 require(original[19][18]==1 and original[20][18]==0 and original[20][3]==3,'Late component deferred destruction not covered')
 for home,mode,kind in [(m,'original','Unity'),(m,'restored','Unity'),(h,'mutation','Unreal')]:
  name=mode+'-'+case;equal(original,rows(home,name,24,24),name);bind(home,name,kind,'EMLateMutationProbe.Run')
 floats={6,7,8,9,10}|{start+offset for start in [15,29,43]for offset in [8,9,10,11,12]}
 original=rows(h,'original-'+case,32,57,floats)
 require([(row[0],row[1])for row in original]==[(stage,f)for stage,n in [(0,12),(1,8),(2,12)]for f in range(1,n+1)],'Missing late scene transition frames')
 require({row[8]for row in original}=={0,.5,1,2},'Late scene time-scale transitions not covered')
 for mode in ['original','restored','native']:
  name=mode+'-'+case;equal(original,rows(h,name,32,57,floats),name,floats);bind(h,name,'Unreal'if mode=='native'else'Unity')
 for home,names,folder in [(m,['LateMutationController','LateMutationLedger','LateMutationWorker'],'EngineMigratorLateMutationProbe'),(h,['LateTravel','LateTravelWorker'],'EngineMigratorLateTravelProbe')]:
  for file in(home/'restored').glob('*.cs'):
   s=file.read_text()
   for name in sorted(names,key=len,reverse=True):s=re.sub(r'\b'+name+r'\b','Restored'+name,s)
   require((u/'UnitySource/Assets'/folder/('Restored'+file.name)).read_text()==s,'Executed restored class changed '+file.name)
for path,digest in load(h/'build-hashes.json').items():require(sha(Path(path)).lower()==digest.lower(),'Final build source/binary changed '+path)
bind(h,'build','Compiler',capture=False)
require(sha(r/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll')==sha(h/'runner/bin/Release/net9.0/EngineMigrator.Core.dll'),'Generator differs from tested core')
source=load(h/'source-verification.json');require(source['status']=='CURRENT_GENERATED_NATIVE_AND_EXECUTED_ORIGINAL_RESTORED_MATCH','Actual source identity gate missing')
require(source['coreSha256'].lower()==sha(r/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'),'Source verifier core differs')
for entry in source['originalClasses']:require(sha(r/entry['source']).lower()==entry['sha256'].lower(),'Executed original source changed '+entry['source'])
proof={'status':'ACTUAL_LATE_MUTATION_AND_TRAVEL_ROUNDTRIP_PASS','goalComplete':False,'mutationCaptures':6,'mutationFrames':24,'mutationValues':24,'travelCaptures':6,'travelFrames':32,'travelValues':57,'sceneCount':3,'engineVersions':['Unity 6000.0.83f1','Unreal 5.8'],'bindings':bindings,'fullProjectCliFreshRun':False};(h/'verification.json').write_text(json.dumps(proof,indent=2));print(proof['status'])

