from pathlib import Path
import re,json,hashlib
root=Path('artifacts/reference-conditional-check'); header=(root/'ReferenceConditional.h').read_text(encoding='utf-8-sig')
blocks=re.findall(r'// EM_METHOD ([^\n]+)\n(.*?)// EM_END_METHOD',header,re.S)
methods=[]
for meta,block in blocks:
    block=re.sub(r'^public:\s*\n','',block)
    block=re.sub(r'^UFUNCTION\([^\n]+\)\s*\n','',block,flags=re.M)
    methods.append(block)
helpers=re.findall(r'^static (?:int32 EM_Int(?:Add|Sub|Mul|Neg)|AActor\* EM_ReferenceValue)\([^\n]+$',header,re.M)
fields=re.findall(r'^(?:TObjectPtr<AActor> (?:Left|Right) = nullptr;|int32 Calls = 0;)',header,re.M)
assert len(methods)==9 and len(fields)==3 and len(helpers)==5
source='''#include <cstdint>
#include <cstring>
#include <iostream>
using int32=std::int32_t;using uint32=std::uint32_t;
struct AActor {};
using EM_TransformRef=AActor*;
// Portable tracked-pointer conversion stand-in; this does not verify Unreal GC.
template<class T> struct TObjectPtr {T* value;TObjectPtr(T* p):value(p){}operator T*()const{return value;}};
struct FMemory{static void Memcpy(void* d,const void* s,std::size_t n){std::memcpy(d,s,n);}};
struct ReferenceConditional {
'''+ '\n'.join(helpers+fields+methods)+'''
};
int main(){ReferenceConditional a;AActor l,r;a.Left=&l;a.Right=&r;
auto emit=[&](AActor* p){std::cout<<(p==&l?"L":p==&r?"R":p==nullptr?"N":"?")<<"|";};
for(bool c:{true,false})emit(a.Pick(c));
for(bool c:{true,false})emit(a.Optional(c));
for(bool c:{true,false})emit(a.Reverse(c));
for(bool c:{true,false})emit(a.Lazy(c));
for(bool c:{true,false})emit(a.Local(c));
for(bool c:{true,false})for(bool other:{true,false})emit(a.Nested(c,other));
if(a.Empty(true)!=nullptr||a.Empty(false)!=nullptr)return 3;
if(a.TransformPick(true,&l,&r)!=&l||a.TransformPick(false,&l,&r)!=&r)return 2;
std::cout<<a.Calls<<"\\n";}
'''
expected='L|R|L|N|N|R|L|R|L|R|L|N|R|R|2'
assert (root/'managed-states.txt').read_text(encoding='utf-8-sig').splitlines()==[expected,expected]
(root/'native-main.cpp').write_text(source,encoding='utf-8');(root/'expected.txt').write_text(expected+'\n',encoding='utf-8')
(root/'native-source-evidence.json').write_text(json.dumps({'sourceHeaderSha256':hashlib.sha256(header.encode()).hexdigest(),'methods':[json.loads(m)['Name'] for m,b in blocks],'engineExecution':False,'trackedPointer':'portable stand-in; actual Unreal tracked-pointer/GC remains unverified'},indent=2))
