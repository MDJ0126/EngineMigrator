"""Validate component identity captures with audited execution provenance; never launches engines."""
from pathlib import Path
import argparse,json
from transform_equality_oracle import require_rows,expected_rows
from object_equality_evidence import validate_receipt,sha,require

def verify(root,unity_only=False):
 root=Path(root).resolve();home=root/'artifacts/transform-equality-check'
 core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
 receipt_dirs=(root/'artifacts/runtime-ui-api-tests',core)
 bindings={}
 baseline=json.loads((home/'native-before-fix.json').read_text())
 for key in ('rows','afterGarbageCollectionRows'):
  rows=baseline.get(key)
  require(type(rows)is list and len(rows)==20 and all(type(r)is list and len(r)==16 and all(type(v)is int for v in r)for r in rows),'Malformed pre-fix native capture')
 expected=[r[:16]for r in expected_rows()]
 original_before=json.loads((home/'unity-original-before-fix.json').read_text())
 require(original_before.get('rows')==original_before.get('afterGarbageCollectionRows')==expected,'Original Unity baseline differs')
 before_bad=[{'case':a[:2],'columns':[i for i,(v,w)in enumerate(zip(a,b))if v!=w]}for a,b in zip(baseline.get('rows',[]),expected)if a!=b]
 require(before_bad==[{'case':[0,3],'columns':[2,3,8,9,12,13]},{'case':[2,0],'columns':[2,3,8,9,12,13]}],'Expected pre-GC destroyed/null Transform difference missing')
 bad=[{'case':a[:2],'columns':[i for i,(v,w)in enumerate(zip(a,b))if v!=w]}for a,b in zip(baseline.get('afterGarbageCollectionRows',[]),expected)if a!=b]
 require(bad==[{'case':[2,3],'columns':[2,3,8,9,12,13]}],'Expected actual Transform GC failure missing')
 captures=[('unity-original','Unity'),('unity-restored','Unity')]
 if not unity_only:captures.append(('native-execution','Unreal'))
 for name,kind in captures:
  capture=home/(name+'.json');data=json.loads(capture.read_text(encoding='utf-8-sig'))
  require(data.get('status')==('PASS'if kind=='Unity'else'CAPTURED'),'Capture failed: '+name)
  for key in ('rows','afterGarbageCollectionRows'):require_rows(data.get(key),name+'/'+key)
  if kind=='Unreal':
   gc=data.get('synchronousGCCompleted')
   require(type(gc)is list and len(gc)==20 and all(x is True for x in gc),'Actual synchronous native GC completion required')
  log=home/('native.log'if kind=='Unreal'else name+'.log');matches=[]
  for folder in receipt_dirs:
   for path in folder.glob('engine-preflight-*.json'):
    receipt=json.loads(path.read_text(encoding='utf-8-sig'))
    try:validate_receipt(receipt,kind,log,capture,root,unity_method='EMTransformEqualityProbe.Run')
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
 return {'status':'UNITY_SOURCE_RESTORED_PASSED'if unity_only else'TRANSFORM_ENGINE_ROUNDTRIP_PASSED','goalComplete':False,'unrealRoundtripVerified':not unity_only,'cases':20,'bindings':bindings}

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--unity-only',action='store_true');args=parser.parse_args()
 root=Path(__file__).resolve().parent.parent;result=verify(root,args.unity_only)
 output=root/'artifacts/transform-equality-check'/('unity-verification.json'if args.unity_only else'engine-verification.json')
 output.write_text(json.dumps(result,indent=2));print(json.dumps(result))
