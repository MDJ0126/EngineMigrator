"""Reproduce the identity-loss condition using installed UE source, without UE execution."""
from pathlib import Path
import hashlib,json,re

home=Path('artifacts/object-equality-check')
gc=Path('C:/Program Files/Epic Games/UE_5.8/Engine/Source/Runtime/CoreUObject/Private/UObject/GarbageCollection.cpp')
text=gc.read_text();kill=re.search(r'FORCEINLINE_DEBUGGABLE void KillReference\(UObject\*& Object\) \{ Object = nullptr; \}',text)
if kill is None:raise RuntimeError('Installed GC elimination helper changed; re-audit required')
header=(home/'AuditObjectEquality.h').read_text();equal=re.search(r'static bool EM_ComponentEqual\([^\n]+',header)
if equal is None:raise RuntimeError('Generated equality helper missing')
cpp='''#include <iostream>
struct AActor{bool valid;};using UObject=AActor;
static bool IsValid(const AActor* actor){return actor&&actor->valid;}
#define FORCEINLINE_DEBUGGABLE
KILL_HELPER
EQUAL_HELPER
void Dump(bool eliminate){
 std::cout<<"[";bool first=true;
 for(int ls=0;ls<4;++ls)for(int mode=0;mode<5;++mode){
  AActor l{ls!=2},r{mode!=3};AActor* left=ls?&l:nullptr;AActor* right=mode==1?left:mode>=2?&r:nullptr;
  if(eliminate){if(left&&!left->valid)KillReference(left);if(right&&!right->valid)KillReference(right);}
  bool eq=EM_ComponentEqual(left,right),ne=!EM_ComponentEqual(left,right),lm=!IsValid(left),rm=!IsValid(right),la=IsValid(left),ra=IsValid(right);
  if(!first)std::cout<<",";first=false;
  std::cout<<"["<<ls<<","<<mode<<","<<eq<<","<<ne<<","<<lm<<","<<rm<<","<<la<<","<<ra<<","<<EM_ComponentEqual(left,right)<<","<<!eq<<"]";
 }std::cout<<"]";
}
int main(){std::cout<<"{\\"before\\":";Dump(false);std::cout<<",\\"afterReferenceElimination\\":";Dump(true);std::cout<<"}";}
'''.replace('KILL_HELPER',kill.group()).replace('EQUAL_HELPER',equal.group())
(home/'native-gc-model.cpp').write_text(cpp)
(home/'gc-source-evidence.json').write_text(json.dumps({'engineVersion':'UE5.8 installed source','gcSource':str(gc),'gcSourceSha256':hashlib.sha256(gc.read_bytes()).hexdigest(),'killReferenceLine':text[:kill.start()].count('\n')+1,'condition':'Garbage flag with killable reference in ProcessReferenceDirectly','actualEngineExecuted':False,'modelSourceSha256':hashlib.sha256((home/'native-gc-model.cpp').read_bytes()).hexdigest()},indent=2))
print('GC_IDENTITY_MODEL_PREPARED_NO_ENGINE_STARTED')
