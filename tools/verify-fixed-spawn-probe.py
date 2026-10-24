"""Gate actual four-condition fixed-stage spawn/clone roundtrips and their execution receipts."""
from pathlib import Path
import json,math
from object_equality_evidence import require,validate_receipt,sha
r=Path(__file__).resolve().parent.parent;a=r/'artifacts';h=a/'fixed-spawn-check';unity=a/'runtime-ui-api-tests';core=Path(json.loads((a/'core-engine-launch-tests/paths.json').read_text())['run']);bindings={}
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
columns=load(h/'columns.json');require(len(columns)==97 and len(set(columns))==97,'Fixed-spawn state layout changed')
def bind(name,kind,capture=True):
 p=h/(name+'.json')if capture else None;found=[]
 for rp in(unity if kind=='Unity'else core).glob('engine-preflight-*.json'):
  data=load(rp)
  try:validate_receipt(data,kind,h/(name+'.log'),p,r,unity_method='EMFixedSpawnProbe.Run')
  except(RuntimeError,ValueError,TypeError,KeyError,IndexError):continue
  found.append((data['finishedUtc'],rp))
 require(bool(found),'Matching safe execution missing '+name);rp=sorted(found)[-1][1];bindings[name]={'receipt':str(rp),'sha256':sha(rp),'captureSha256':sha(p)if p else None}
def rows(name):
 d=load(h/(name+'.json'));fps=int(name.split('-')[1]);v=d.get('rows');dt=d.get('deltas')
 require(d.get('status')=='CAPTURED'and isinstance(v,list)and len(v)==24,'Incomplete '+name)
 require(isinstance(dt,list)and len(dt)==24 and all(math.isfinite(x)and abs(x-1/fps)<1e-7 for x in dt),'Actual timing differs '+name)
 for i,row in enumerate(v,1):require(len(row)==97 and row[0]==i and all(type(x)in(int,float)and math.isfinite(x)and x==int(x)for x in row),'Malformed integer state '+name)
 def c(row,key):return row[columns.index(key)]
 require(all(c(row,'ReturnedMainStart')==0 and c(row,'ReturnedMainFixed')==0 for row in v),'Synchronous new-component callback regression '+name)
 require(any(c(row,'Clone.Starts')==2 and c(row,'CloneSameChild')==1 and c(row,'ReturnedCloneStart')==1 for row in v),'Copied instance state/reference or new Start not covered '+name)
 require(v[-1][1]>=10 and c(v[-1],'Main.Alive')==0 and c(v[-1],'Clone.Alive')==0 and c(v[-1],'SavedMainChild.Alive')==1 and c(v[-1],'SavedCloneChild.Alive')==1,'Fixed destruction/external child ownership not covered '+name)
 for row in v:
  for path in ['Main','Clone']:
   if c(row,path+'.Alive'):require(c(row,path+'.Fixeds')==c(row,path+'.DerivedFixed'),'Inherited parent/override count differs '+name)
 return v
for case in ['100-20','50-20','30-20','30-10']:
 original=rows('original-'+case)
 for mode in ['original','native','restored']:
  name=mode+'-'+case;got=rows(name)
  for i,(left,right)in enumerate(zip(original,got),1):
   for column,x,y in zip(columns,left,right):require(x==y,f'{name} frame={i} {column}: {x}/{y}')
  bind(name,'Unreal'if mode=='native'else'Unity')
for p,digest in load(h/'build-hashes.json').items():require(sha(Path(p)).lower()==digest.lower(),'Compiled source/binary changed '+p)
bind('build','Compiler',False)
source=load(h/'source-verification.json');require(source['status']=='CURRENT_FIXED_SPAWN_SOURCES_MATCH'and source['classes']==6,'Current generator source identity missing')
require(source['coreSha256']==sha(r/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'),'Current core differs from checked core')
require(sha(h/'runner/bin/Release/net9.0/EngineMigrator.Core.dll')==source['coreSha256'],'Executed runner core differs')
for p,digest in source['files'].items():require(sha(Path(p))==digest.lower(),'Executed source changed '+p)
report={'status':'ACTUAL_FIXED_SPAWN_ROUNDTRIP_PASS','goalComplete':False,'captures':12,'conditions':['100 FPS / .02','50 FPS / .02','30 FPS / .02','30 FPS / .01'],'frames':24,'values':97,'actualFrameTimingVerified':True,'inheritedFixedCallbacks':True,'clonedLiveStateAndExternalReferences':True,'fixedActivationBoundaries':True,'sameClassInstanceOrderGuaranteed':False,'bindings':bindings}
(h/'verification.json').write_text(json.dumps(report,indent=2));print(report['status'])
