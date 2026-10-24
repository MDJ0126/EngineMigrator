"""Engine-free semantic oracle. Never labels a model as actual engine execution."""
from pathlib import Path
import hashlib
import json
import re
import sys
from object_equality_oracle import expected_rows,require_rows

home=Path('artifacts/object-equality-check')
expected=expected_rows()

header=(home/'AuditObjectEquality.h').read_text()
helper=re.search(r'static bool EM_ComponentEqual\([^\n]+',header)
if helper is None:raise RuntimeError('Generated equality helper missing')
cpp='''#include <iostream>
struct AActor{bool valid;};
static bool IsValid(const AActor* actor){return actor&&actor->valid;}
HELPER
int main(){
 std::cout<<"[";bool first=true;
 for(int ls=0;ls<4;++ls)for(int mode=0;mode<5;++mode){
  AActor l{ls!=2},r{mode!=3};AActor* left=ls?&l:nullptr;AActor* right=mode==1?left:mode>=2?&r:nullptr;
  bool eq=EM_ComponentEqual(left,right),ne=!EM_ComponentEqual(left,right),lm=!IsValid(left),rm=!IsValid(right),la=IsValid(left),ra=IsValid(right);
  if(!first)std::cout<<",";first=false;
  std::cout<<"["<<ls<<","<<mode<<","<<eq<<","<<ne<<","<<lm<<","<<rm<<","<<la<<","<<ra<<","<<EM_ComponentEqual(left,right)<<","<<!eq<<"]";
 }std::cout<<"]";
}
'''.replace('HELPER',helper.group())
(home/'native-model.cpp').write_text(cpp)
managed=json.loads((home/'managed-rows.json').read_text())
if len(managed)!=2 or any(rows!=expected for rows in managed):raise RuntimeError('Managed source/restored differs from independent oracle')
for i,rows in enumerate(managed):require_rows(rows,'managed '+str(i))
if '--native' in sys.argv:
 for variant in ('Od','O2'):
  require_rows(json.loads((home/f'native-model-{variant}.json').read_text()),'native model '+variant)
baseline=json.loads(Path('artifacts/user-project-baseline/original-hashes.json').read_text())
for path,wanted in baseline.items():
 if hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()!=wanted.upper():raise RuntimeError('User original changed: '+path)
result={'status':'ENGINE_FREE_PASS','cases':20,'managedCaptures':2,'nativeModelProfiles':['Od','O2'] if '--native' in sys.argv else [],'actualUnityExecuted':False,'actualUnrealExecuted':False,'engineRoundtripVerified':False,'garbageCollectionIdentityVerified':False,'originalFilesUnchanged':len(baseline),'oracleReference':'https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Runtime/Export/Scripting/UnityEngineObject.bindings.cs','referenceVersion':'6000.0 branch; exact 6000.0.83f1 execution pending','releaseUpdated':False}
result.update(scope='Pre-reference-elimination model only',releaseReady=False)
gap=home/'gc-model-verification.json'
if gap.exists():result['gcEliminationModelStatus']=json.loads(gap.read_text())['status']
(home/'model-verification.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result))
