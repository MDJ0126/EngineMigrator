"""Record the verified model defect; never claims an actual UE GC run."""
from pathlib import Path
import json
from object_equality_oracle import expected_rows,require_rows

home=Path('artifacts/object-equality-check');expected=expected_rows();findings=[]
for profile in ('Od','O2'):
 data=json.loads((home/f'native-gc-model-{profile}.json').read_text());require_rows(data['before'],profile+' pre-GC model')
 after=data['afterReferenceElimination']
 if type(after)is not list or len(after)!=20:raise RuntimeError('Incomplete elimination model')
 mismatches=[{'case':want[:2],'columns':[i for i,(a,b)in enumerate(zip(want,got))if a!=b],'expected':want,'observed':got}for want,got in zip(expected,after)if want!=got]
 if len(mismatches)!=1 or mismatches[0]['case']!=[2,3]or mismatches[0]['columns']!=[2,3,8,9]:raise RuntimeError('Elimination model result changed; inspect instead of assuming the defect')
 findings.append({'profile':profile,'mismatches':mismatches})
result={'status':'IDENTITY_LOSS_REPRODUCED_IN_SOURCE_BACKED_MODEL','actualEngineExecuted':False,'actualEngineGCFailureVerified':False,'releaseReady':False,'condition':'Both distinct destroyed actor references are eliminated to nullptr. The existing pointer equality helper then incorrectly considers them identical.','requiredFix':'Preserve object identity independently of GC-cleared native references; validate in actual Unity/UE before publishing.','sourceEvidence':'gc-source-evidence.json','profiles':findings}
(home/'gc-model-verification.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
