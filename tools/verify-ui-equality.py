"""Validate component identity captures with audited execution provenance; never launches engines."""
from pathlib import Path
import argparse,json
from ui_equality_oracle import require_rows,expected_rows
from object_equality_evidence import validate_receipt,sha,require

def verify(root,unity_only=False):
 root=Path(root).resolve();home=root/'artifacts/ui-equality-check'
 core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
 receipt_dirs=(root/'artifacts/runtime-ui-api-tests',core)
 bindings={}
 baseline=json.loads((home/'native-before-fix.json').read_text())
 for key in('rows','afterGarbageCollectionRows'):
  rows=baseline.get(key)
  require(type(rows)is list and len(rows)==30 and all(type(r)is list and len(r)==18 and all(type(v)is int for v in r)for r in rows),'Malformed native UI baseline')
  require(sum(a!=b for a,b in zip(rows,expected_rows()))==28,'Actual UI kind/liveness baseline mismatch count changed')
 captures=[('unity-original','Unity'),('unity-restored','Unity')]
 if not unity_only:captures.append(('native-execution','Unreal'))
 for name,kind in captures:
  capture=home/(name+'.json');data=json.loads(capture.read_text(encoding='utf-8-sig'))
  require(data.get('status')==('PASS'if kind=='Unity'else'CAPTURED'),'Capture failed: '+name)
  for key in ('rows','afterGarbageCollectionRows'):require_rows(data.get(key),name+'/'+key)
  if kind=='Unreal':
   gc=data.get('synchronousGCCompleted')
   require(type(gc)is list and len(gc)==30 and all(x is True for x in gc),'Actual synchronous native GC completion required')
  log=home/('native.log'if kind=='Unreal'else name+'.log');matches=[]
  for folder in receipt_dirs:
   for path in folder.glob('engine-preflight-*.json'):
    receipt=json.loads(path.read_text(encoding='utf-8-sig'))
    try:validate_receipt(receipt,kind,log,capture,root,unity_method='EMUIEqualityProbe.Run')
    except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
    matches.append((receipt['finishedUtc'],path))
  require(bool(matches),'Successful matching audited engine receipt missing: '+name)
  receipt=sorted(matches)[-1][1]
  bindings[name]={'captureSha256':sha(capture),'receipt':str(receipt),'receiptSha256':sha(receipt)}
 if not unity_only:
  matches=[]
  for path in core.glob('engine-preflight-*.json'):
   receipt=json.loads(path.read_text(encoding='utf-8-sig'))
   try:validate_receipt(receipt,'Compiler',home/'native-build.log',root=root)
   except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
   matches.append((receipt['finishedUtc'],path))
  require(bool(matches),'Successful native build receipt missing')
  bindings['compiler']={'receipt':str(sorted(matches)[-1][1]),'receiptSha256':sha(sorted(matches)[-1][1])}
 return {'status':'UNITY_SOURCE_RESTORED_PASSED'if unity_only else'UI_ENGINE_ROUNDTRIP_PASSED','goalComplete':False,'unrealRoundtripVerified':not unity_only,'cases':30,'bindings':bindings}

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--unity-only',action='store_true');args=parser.parse_args()
 root=Path(__file__).resolve().parent.parent;result=verify(root,args.unity_only)
 output=root/'artifacts/ui-equality-check'/('unity-verification.json'if args.unity_only else'engine-verification.json')
 output.write_text(json.dumps(result,indent=2));print(json.dumps(result))
