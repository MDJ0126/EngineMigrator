"""Require real naming captures, scoped successful receipts and unchanged reused inputs."""
from pathlib import Path
import json
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/ui-name-check';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
def load(path):
 data=path.read_bytes();return json.loads(data.decode('utf-16') if data.startswith((b'\xff\xfe',b'\xfe\xff')) else data.decode('utf-8-sig'))
expected=[]
for existing in (False,True):
 for stage,name in [('InitUI','Initial UI'),('RenameCanvas','캔버스 UI'),('RenameButton','버튼 UI'),('CloneOwner','버튼 UI')]:expected.append({'existing':existing,'stage':stage,'owner':name,'copy':'복제 UI' if stage=='CloneOwner' else '', 'panelOwner':True,'buttonOwner':True})
unity=load(home/'unity.json');require(unity.get('status')=='CAPTURED' and len(unity['rows'])==16,'Actual Unity source/restored naming capture missing')
for source in ('AuditUiName','RestoredAuditUiName'):
 rows=[{k:v for k,v in row.items() if k!='source'} for row in unity['rows'] if row['source']==source]
 require(rows==expected,'Actual Unity behavior differs: '+source)
before=load(home/'native-before.json');native=load(home/'native.json')
require(before.get('status')=='CAPTURED' and len(before['rows'])==8,'Before-fix game-mode capture missing')
require(sum(x!=y for x,y in zip(before['rows'],expected))==3,'Original naming counterexample changed')
require(native.get('status')=='CAPTURED' and native['rows']==expected,'Corrected native naming differs')
require((home/'restored.cs').read_bytes()==(home/'before-fix-restored.cs.txt').read_bytes(),'Unity restored source changed after its actual run')
bindings={}
def bind(name,kind,folder,compiler=False):
 capture=None if compiler else home/(name+'.json');matches=[]
 for path in folder.glob('engine-preflight-*.json'):
  receipt=load(path)
  try:validate_receipt(receipt,kind,home/(name+'.log'),capture,root,unity_method='EMUiNameProbe.Run')
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  matches.append((receipt['finishedUtc'],path))
 require(bool(matches),'Successful execution receipt missing: '+name)
 path=sorted(matches)[-1][1];bindings[name]={'receipt':str(path),'receiptSha256':sha(path)}
 if capture:bindings[name]['captureSha256']=sha(capture)
bind('unity','Unity',root/'artifacts/runtime-ui-api-tests');bind('native-before','Unreal',core);bind('native','Unreal',core);bind('build','Compiler',core,True)
prior=load(root/'artifacts/dynamic-ui-check/checkpoint.json')
fixture='tests/EngineMigrator.Checks/Fixtures/dynamic-ui.cs.txt'
prior_hashes={k.replace('\\','/'):v for k,v in prior['sourceHashes'].items()}
require(sha(root/fixture)==prior_hashes[fixture],'Reused dynamic UI source changed')
for name,suffix in [('dynamic',''),('dynamic-static','-static')]:
 data=load(home/(name+'.json'));original=load(root/('artifacts/dynamic-ui-check/original'+suffix+'.json'))
 require(data.get('status')=='CAPTURED' and data['rows']==original['rows'],'Actual dynamic UI regression differs: '+name)
 bind(name,'Unreal',core)
manifest=load(home/'build-hashes.json')
for path,digest in manifest.items():require(sha(Path(path)).lower()==digest.lower(),'Successful native build changed: '+path)
record={'status':'ACTUAL_UI_OWNER_NAME_ROUNDTRIP_PASS','goalComplete':False,'scenarios':2,'nameStagesPerScenario':4,'beforeDifferentRows':3,'unitySourceAndRestoredExecuted':True,'unityRestoredSourceUnchangedAfterFix':True,'unityProcesses':1,'nativeGameModeVerified':True,'dynamicRegressionScenarios':2,'bindings':bindings,'restoredSourceSha256':sha(home/'restored.cs'),'headerSha256':sha(home/'AuditUiName.h')}
(home/'verification.json').write_text(json.dumps(record,indent=2));print('ACTUAL_UI_OWNER_NAME_ROUNDTRIP_PASS')
