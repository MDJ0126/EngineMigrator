"""Validate actual UI API roundtrip and restored-source execution provenance."""
from pathlib import Path
import importlib.util,json
from object_equality_evidence import require,validate_receipt,sha
require(__debug__,'UI semantic comparisons require Python assertions enabled')
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/ui-equality-check';work=root/'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da';unity=root/'artifacts/runtime-ui-api-tests'
spec=importlib.util.spec_from_file_location('ui_api_comparator',root/'tools/compare-runtime-ui-api.py');api=importlib.util.module_from_spec(spec);spec.loader.exec_module(api)
summaries=[];bindings={};counts={}
for name,kind in [('original','Unity'),('native','Unreal'),('restored','Unity')]:
 capture=home/('ui-api-'+name+'.json');data=api.load(capture);summary=api.summarize(capture);api.semantics(summary);summaries.append(summary);counts[name]=len(data['frames']);matches=[]
 for folder in(work,unity):
  for path in folder.glob('engine-preflight-*.json'):
   receipt=json.loads(path.read_text(encoding='utf-8-sig'))
   try:validate_receipt(receipt,kind,home/('ui-api-'+name+'.log'),capture,root,unity_method='EMRuntimeWorldProbe.Run')
   except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
   matches.append((receipt['finishedUtc'],path))
 require(bool(matches),'UI API actual engine receipt missing: '+name);receipt=sorted(matches)[-1][1];bindings[name]={'captureSha256':sha(capture),'receipt':str(receipt),'receiptSha256':sha(receipt)}
require(summaries[0]==summaries[1]==summaries[2],'UI typed references, independent instances, states or callbacks differ')
record={'status':'ACTUAL_UI_API_ROUNDTRIP_PASSED','goalComplete':False,'checkpoints':20,'worldFrameCounts':counts,'externalCallbacksPreserved':True,'typedReferencesRemapped':True,'instanceStateIndependent':True,'bindings':bindings}
(home/'ui-api-verification.json').write_text(json.dumps(record,indent=2));print('ACTUAL_UI_API_ROUNDTRIP_PASS')
