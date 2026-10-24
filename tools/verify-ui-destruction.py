"""Verify real frame-based UI removal and audited engine provenance."""
from pathlib import Path
import json
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/ui-equality-check'
core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run']);unity=root/'artifacts/runtime-ui-api-tests'
expected=[[f,int(f>=2),int(f>=3),int(f>=3),int(f>=4),int(f>=5),int(f>=3),int(f>=3),int(f>=5)]for f in range(1,8)]
original=json.loads((home/'destruction-original.json').read_text(encoding='utf-8-sig'))
original_rows=original.get('rows')
require(original.get('status')=='CAPTURED'and type(original_rows)is list and len(original_rows)==7 and all(type(r)is list and len(r)==17 and all(type(v)is int for v in r)and all(v in(0,1)for v in r[1:])for r in original_rows),'Actual original Unity clone/removal capture required')
require([r[:9]for r in original_rows]==expected,'Original Unity UI removal timing differs')
bindings={}
for name,kind in [('original','Unity'),('native','Unreal'),('restored','Unity')]:
 capture=home/('destruction-'+name+'.json');data=json.loads(capture.read_text(encoding='utf-8-sig'));rows=data.get('rows')
 require(data.get('status')=='CAPTURED'and type(rows)is list and len(rows)==7 and all(type(r)is list and len(r)==17 and all(type(v)is int for v in r)for r in rows),'Malformed frame-based UI removal/clone capture')
 require(rows==original_rows,'UI removal timing, clone reference identity, owner preservation or lookup differs: '+name)
 matches=[]
 for folder in(core,unity):
  for path in folder.glob('engine-preflight-*.json'):
   receipt=json.loads(path.read_text(encoding='utf-8-sig'))
   try:validate_receipt(receipt,kind,home/('destruction-'+name+'.log'),capture,root,unity_method='EMUIDestructionProbe.Run')
   except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
   matches.append((receipt['finishedUtc'],path))
 require(bool(matches),'Successful UI destruction execution receipt missing: '+name)
 receipt=sorted(matches)[-1][1];bindings[name]={'sha256':sha(capture),'receipt':str(receipt),'receiptSha256':sha(receipt)}
compilers=[]
for path in core.glob('engine-preflight-*.json'):
 receipt=json.loads(path.read_text(encoding='utf-8-sig'))
 try:validate_receipt(receipt,'Compiler',home/'destruction-build.log',root=root)
 except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
 compilers.append((receipt['finishedUtc'],path))
require(bool(compilers),'Successful UI destruction build missing')
record={'status':'ACTUAL_UI_COMPONENT_DESTRUCTION_ROUNDTRIP_PASSED','frames':7,'cloneReferencesComparedAgainstActualUnity':True,'duplicateRemovalSafe':True,'sameFrameReferenceAlive':True,'nextFrameComponentMissing':True,'ownerPreserved':True,'lookupReflectsRemoval':True,'goalComplete':False,'bindings':bindings,'compilerReceipt':str(sorted(compilers)[-1][1])}
(home/'destruction-verification.json').write_text(json.dumps(record,indent=2));print('ACTUAL_UI_COMPONENT_DESTRUCTION_ROUNDTRIP_PASS')
