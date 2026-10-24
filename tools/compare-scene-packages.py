"""Compare actual editor-exported scene data. Does not claim rendered or gameplay equivalence."""
import argparse, json, math
from pathlib import Path

def read(path):return json.loads(Path(path).read_text(encoding="utf-8-sig"))

def compare(a,b,path,errors):
    if isinstance(a,str) and a[:1] in ("{","["):
        try:a=json.loads(a);b=json.loads(b)
        except (ValueError,TypeError):pass
    if isinstance(a,bool) or isinstance(b,bool):
        if a!=b:errors.append({"path":path,"expected":a,"actual":b})
    elif isinstance(a,(float,int)) and isinstance(b,(float,int)):
        if not math.isclose(a,b,rel_tol=1e-6,abs_tol=1e-6):errors.append({"path":path,"expected":a,"actual":b})
    elif isinstance(a,dict) and isinstance(b,dict):
        for key in a:
            if key in ("id","profileId"):continue
            if key not in b:errors.append({"path":path+"."+key,"expected":a[key],"actual":"MISSING"})
            elif key=="value" and a.get("kind") in ("float","int"):
                try:compare(float(a[key]),float(b[key]),path+"."+key,errors)
                except (ValueError,TypeError):errors.append({"path":path+"."+key,"expected":a[key],"actual":b[key]})
            else:compare(a[key],b[key],path+"."+key,errors)
    elif isinstance(a,list) and isinstance(b,list):
        if len(a)!=len(b):errors.append({"path":path,"expectedLength":len(a),"actualLength":len(b)})
        for i,(x,y) in enumerate(zip(a,b)):compare(x,y,path+"["+str(i)+"]",errors)
    elif a!=b and not (a in (None,"") and b in (None,"")):errors.append({"path":path,"expected":a,"actual":b})

def run(original,restored):
    errors=[]
    if len(original["nodes"])!=len(restored["nodes"]):errors.append({"path":"nodes","expectedLength":len(original["nodes"]),"actualLength":len(restored["nodes"])})
    restored_ids={n["id"]:n for n in restored["nodes"]}
    for i,a in enumerate(original["nodes"]):
        b=restored_ids.get(a["id"])
        if b is None:errors.append({"path":"nodes.identity","expected":a["id"],"actual":"MISSING"});continue
        for field in ("name","parent","position","rotation","scale","active","tag","layer","effectiveActive","hasCamera","hasLight","hasVolume","hasRenderer","rendererEnabled","castShadows"):
            if a.get("hasUI") and field in ("position","rotation","scale"):continue # canvas viewport drives physical transforms; compare anchors/offsets below
            if field in a:compare(a[field],b.get(field),f"nodes[{i}].{field}",errors)
        for flag,field in (("hasCamera","camera"),("hasLight","light"),("hasVolume","volume")):
            if a.get(flag):compare(a[field],b.get(field),f"nodes[{i}].{field}",errors)
        if a.get("behaviours"):compare(a["behaviours"],b.get("behaviours"),f"nodes[{i}].behaviours",errors)
        if a.get("hasPhysics"):compare(a["physics"],b.get("physics"),f"nodes[{i}].physics",errors)
        if a.get("hasAnimation"):compare(a["animation"],b.get("animation"),f"nodes[{i}].animation",errors)
        if a.get("hasUI"):
            left=dict(a["ui"]);right=dict(b.get("ui",{}))
            if left.get("canvas"):
                for key in ("anchorMin","anchorMax","pivot","position","size"):left.pop(key,None);right.pop(key,None)
            compare(left,right,f"nodes[{i}].ui",errors)
    if "gravity" in original:compare(original["gravity"],restored.get("gravity"),"gravity",errors)
    if original.get("hasEnvironment"):compare(original["environment"],restored.get("environment"),"environment",errors)
    if original.get("hasTiming"):
        for field in ("timeScale","fixedDeltaTime","maximumDeltaTime"):compare(original["timing"][field],restored.get("timing",{}).get(field),"timing."+field,errors)
    return {"status":"PASS" if not errors else "FAIL","nodesCompared":len(original["nodes"]),"errors":errors,"renderingEquivalent":False,"gameplayVerified":False,"projectPortComplete":False}

if __name__=="__main__":
    parser=argparse.ArgumentParser();parser.add_argument("original");parser.add_argument("restored");parser.add_argument("output");args=parser.parse_args()
    result=run(read(args.original),read(args.restored));Path(args.output).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps(result,ensure_ascii=False,indent=2));raise SystemExit(0 if result["status"]=="PASS" else 1)
