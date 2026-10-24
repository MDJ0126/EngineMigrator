"""Check actual captures and successful safety receipts; never starts an engine."""
from pathlib import Path
import json
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/dynamic-ui-check'
core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
unity=root/'artifacts/runtime-ui-api-tests'
fields=json.loads((home/'fields.json').read_text());require(len(fields)==17,'Unexpected scenario fields')
original=json.loads((home/'original.json').read_text(encoding='utf-8-sig'))
onsets=[2,3,3,4,5,5,5,5,5,5,5,6,6,7,9,9,9]
expected=[[f]+[int(f>=onset) for onset in onsets] for f in range(1,11)]
require(original.get('status')=='CAPTURED' and original.get('rows')==expected,'Observed Unity dynamic UI behavior changed')
bindings={}
for name,kind,folder in [(n+s,k,f) for s in ('','-static') for n,k,f in [('original','Unity',unity),('native','Unreal',core),('restored','Unity',unity)]]:
 capture=home/(name+'.json');data=json.loads(capture.read_text(encoding='utf-8-sig'))
 require(data.get('status')=='CAPTURED' and type(data.get('rows')) is list,'Missing actual capture')
 require(all(type(row) is list and len(row)==18 and all(type(v) is int for v in row) for row in data['rows']),'Malformed capture')
 require(data['rows']==expected,'Actual dynamic UI roundtrip differs: '+name)
 receipts=[]
 for path in folder.glob('engine-preflight-*.json'):
  receipt=json.loads(path.read_text(encoding='utf-8-sig'))
  try:validate_receipt(receipt,kind,home/(name+'.log'),capture,root,unity_method='EMDynamicUIProbe.Run')
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  receipts.append((receipt['finishedUtc'],path))
 require(bool(receipts),'Successful scoped execution receipt missing: '+name)
 path=sorted(receipts)[-1][1];bindings[name]={'captureSha256':sha(capture),'receipt':str(path),'receiptSha256':sha(path)}
compilers=[]
for path in core.glob('engine-preflight-*.json'):
 receipt=json.loads(path.read_text(encoding='utf-8-sig'))
 try:validate_receipt(receipt,'Compiler',home/'destruction-build.log',root=root)
 except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
 compilers.append((receipt['finishedUtc'],path))
require(bool(compilers),'Successful native build receipt missing')
manifest=json.loads((home/'destruction-build-hashes.json').read_text(encoding='utf-8-sig'))
for path,digest in manifest.items():require(sha(Path(path)).lower()==digest.lower(),'Engine-tested native build changed: '+path)
record={'status':'ACTUAL_DYNAMIC_UI_ROUNDTRIP_PASS','goalComplete':False,'scenarios':2,'actualCaptures':6,'framesPerScenario':10,'valuesPerFrame':18,'fields':fields,'bindings':bindings,'compilerReceipt':str(sorted(compilers)[-1][1]),'buildHashes':manifest}
(home/'verification.json').write_text(json.dumps(record,indent=2));print('ACTUAL_DYNAMIC_UI_ROUNDTRIP_PASS')
