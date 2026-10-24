"""Gate fixed-event evidence on actual engine captures and exact discrete state."""
from pathlib import Path
import json,math
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/fixed-update-check';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
bindings={};cases={};floating={1,9,10,11}
def bind(name,kind,folder,capture=True):
 path=home/(name+'.json') if capture else None;matches=[]
 for receipt_path in folder.glob('engine-preflight-*.json'):
  receipt=load(receipt_path)
  try:validate_receipt(receipt,kind,home/(name+'.log'),path,root,unity_method='EMFixedProbe.Run')
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  matches.append((receipt['finishedUtc'],receipt_path))
 require(bool(matches),'Successful scoped receipt missing: '+name)
 receipt=sorted(matches)[-1][1];bindings[name]={'receipt':str(receipt),'receiptSha256':sha(receipt)}
 if path:bindings[name]['captureSha256']=sha(path)
def rows(name):
 data=load(home/(name+'.json'));values=data.get('rows')
 require(data.get('status')=='CAPTURED' and type(values)is list and len(values)==24,'Actual 24-frame capture missing: '+name)
 for index,row in enumerate(values):
  require(type(row)is list and len(row)==16 and row[0]==index+1 and all(type(v)in(int,float) and math.isfinite(v)for v in row),'Malformed fixed-event data')
  require(all(v==int(v)for j,v in enumerate(row)if j not in floating),'Non-integral discrete state')
  require(row[12]==1 and row[5]==1 and row[6]==row[7],'Start-before-fixed or inherited fixed call mismatch')
 return values
def compare(a,b,name):
 for frame,(left,right) in enumerate(zip(a,b),1):
  for column,(x,y) in enumerate(zip(left,right)):
   require(abs(x-y)<=(2e-6 if column in floating else 0),f'Fixed runtime differs {name} frame={frame} column={column}: {x} / {y}')
for case in ('100-20','50-20','30-20','30-10'):
 values={name:rows(name+'-'+case)for name in ('original','native','restored')}
 compare(values['original'],values['native'],'native-'+case);compare(values['original'],values['restored'],'restored-'+case)
 for name,kind,folder in [('original','Unity',root/'artifacts/runtime-ui-api-tests'),('native','Unreal',core),('restored','Unity',root/'artifacts/runtime-ui-api-tests')]:bind(name+'-'+case,kind,folder)
 cases[case]={'frames':24,'finalFixedCalls':values['original'][-1][6],'finalAddedFixed':values['original'][-1][15],'finalEnableCount':values['original'][-1][3],'finalDisableCount':values['original'][-1][4]}
require(rows('original-100-20')[0][6]==1 and rows('original-100-20')[1][6]==1,'Zero-fixed-step rendered frames not covered')
require(rows('original-30-10')[0][6]==4,'Multi-fixed-step rendered frames not covered')
clock=load(home/'original-30-10.json.clock.json')
require(len(clock)==24 and clock[0][:3]==[4704000,141120000,1] and clock[0][5]==4704000,'Actual integer clock rate/capture observation missing')
require(abs(clock[0][4]*141120000-3*1411199)<1e-6,'Observed fixed quantum differs')
bind('build','Compiler',core,False);bind('adapter-unreal','Unreal',core,False)
adapter=load(home/'adapter-verification.json')
require(adapter.get('status')=='PASS' and adapter.get('mapReloadVerified')is True and adapter.get('fixedTicks')==1411199 and adapter.get('invalidInputsRejectedBeforeSpawn')==5,'Actual timing adapter persistence incomplete')
for path,digest in load(home/'build-hashes.json').items():require(sha(Path(path)).lower()==digest.lower(),'Native build changed: '+path)
prior=load(home/'pre-integer-restored-hashes.json');current={p.name:sha(p)for p in(home/'restored').glob('*.cs')}
require(prior==current and len(current)==4,'Actual restored source changed')
record={'status':'ACTUAL_FIXED_UPDATE_ROUNDTRIP_PASS','goalComplete':False,'scenarios':cases,'actualCaptures':12,'framesPerScenario':24,'valuesPerFrame':16,'timeRate':141120000,'firstFixedTimeZeroObserved':True,'originalRestoredSourceHashes':current,'bindings':bindings,'adapter':adapter,'physicsSubstepEquivalence':False,'sceneTravelClockContinuityVerified':False}
(home/'verification.json').write_text(json.dumps(record,indent=2));print('ACTUAL_FIXED_UPDATE_ROUNDTRIP_PASS')
