"""Compare actual imported/reexported pixels, triangles, UVs, normals and shared references."""
import argparse,json,math
from pathlib import Path
from PIL import Image

def read(path):return json.loads(Path(path).read_text(encoding="utf-8-sig"))
def close(a,b):return math.isclose(a,b,rel_tol=1e-5,abs_tol=1e-5)
def geometry(mesh):
    vertices=mesh["vertices"];uv=mesh.get("uv",[]);normals=mesh.get("normals",[]);triangles=mesh["triangles"]
    def corner(index):
        v=vertices[index];values=[v[k] for k in ("x","y","z")]
        if len(uv)==len(vertices):values.extend(uv[index][k] for k in ("x","y"))
        if len(normals)==len(vertices):values.extend(normals[index][k] for k in ("x","y","z"))
        return tuple(round(v,4) for v in values)
    faces=[]
    for i in range(0,len(triangles),3):
        face=tuple(corner(index) for index in triangles[i:i+3]);faces.append(min(face,face[1:]+face[:1],face[2:]+face[:2]))
    return sorted(faces)

def run(source_path,target_path):
    source=read(source_path);target=read(target_path);errors=[];counts={"textures":0,"materials":0,"meshes":0,"nodes":0};mapped={kind:{} for kind in ("textures","materials","meshes")}
    arrays={side:{kind:{entry["id"]:entry for entry in package[kind]} for kind in mapped} for side,package in (("source",source),("target",target))}
    def fail(path,a,b):errors.append({"path":path,"expected":a,"actual":b})
    def resource(kind,left,right):
        existing=mapped[kind].get(left)
        if existing is not None:
            if existing!=right:fail(kind+".sharedReference",existing,right)
            return
        mapped[kind][left]=right
        if left not in arrays["source"][kind] or right not in arrays["target"][kind]:fail(kind+".missing",left,right);return
        a=arrays["source"][kind][left];b=arrays["target"][kind][right];counts[kind]+=1;label=kind+"."+a["name"]
        if a["name"]!=b["name"]:fail(label+".name",a["name"],b["name"])
        if kind=="textures":
            for key in ("srgb","filter","wrapU","wrapV","mipmaps"):
                if a[key]!=b[key]:fail(label+"."+key,a[key],b[key])
            with Image.open(Path(source_path).parent/a["file"]) as first,Image.open(Path(target_path).parent/b["file"]) as last:
                first=first.convert("RGBA");last=last.convert("RGBA")
                if first.size!=last.size or first.tobytes()!=last.tobytes():fail(label+".pixels",{"size":first.size},{"size":last.size,"equal":False})
        elif kind=="meshes":
            if geometry(a)!=geometry(b):fail(label+".trianglesUVNormals",geometry(a)[:4],geometry(b)[:4])
        elif kind=="materials":
            if a["shader"]!=b["shader"]:fail(label+".shader",a["shader"],b["shader"])
            for key in ("r","g","b","a"):
                if not close(a["color"][key],b["color"][key]):fail(label+".color."+key,a["color"][key],b["color"][key])
            if a["shader"]=="standard":
                for key in ("metallic","smoothness"):
                    if not close(a[key],b[key]):fail(label+"."+key,a[key],b[key])
            if bool(a.get("texture"))!=bool(b.get("texture")):fail(label+".texture",a.get("texture"),b.get("texture"))
            elif a.get("texture"):resource("textures",a["texture"],b["texture"])
    identities={node["id"]:node for node in target["nodes"]};use_ids=len(identities)==len(target["nodes"]) and all(node["id"] in identities for node in source["nodes"])
    nodes=identities if use_ids else {node["name"]:node for node in target["nodes"]};sprites={}
    if len(nodes)!=len(target["nodes"]):raise ValueError("Comparison requires stable node identities when names are duplicated")
    for node in source["nodes"]:
        other=nodes.get(node["id"] if use_ids else node["name"])
        if not other:fail("nodes.missing",node["name"],None);continue
        counts["nodes"]+=1
        if bool(node.get("mesh"))!=bool(other.get("mesh")):fail(node["name"]+".mesh",node.get("mesh"),other.get("mesh"))
        elif node.get("mesh"):resource("meshes",node["mesh"],other["mesh"])
        if len(node["materials"])!=len(other["materials"]):fail(node["name"]+".materialSlots",len(node["materials"]),len(other["materials"]))
        for a,b in zip(node["materials"],other["materials"]):resource("materials",a,b)
        if node.get("hasUI") and node["ui"].get("texture"):
            a=node["ui"];b=other.get("ui",{})
            if not b.get("texture"):fail(node["name"]+".ui.texture",a["texture"],None)
            else:resource("textures",a["texture"],b["texture"])
            if a.get("sprite"):
                previous=sprites.setdefault(a["sprite"],b.get("sprite"))
                if not b.get("sprite") or previous!=b["sprite"]:fail(node["name"]+".ui.sharedSprite",previous,b.get("sprite"))
                for key in ("spriteRect","spritePivot","pixelsPerUnit","textureWidth","textureHeight","uv"):
                    if isinstance(a[key],dict):
                        for axis,value in a[key].items():
                            if not close(value,b.get(key,{}).get(axis,float('inf'))):fail(node["name"]+".ui."+key+"."+axis,value,b.get(key,{}).get(axis))
                    elif not close(a[key],b.get(key,float('inf'))):fail(node["name"]+".ui."+key,a[key],b.get(key))
    for kind in mapped:
        if len(mapped[kind])!=len(source[kind]) or len(set(mapped[kind].values()))!=len(target[kind]):fail(kind+".unmatchedOrMerged",len(source[kind]),len(target[kind]))
    return {"status":"PASS" if not errors else "FAIL","compared":counts,"errors":errors,"renderingEquivalent":False,"projectPortComplete":False}

if __name__=="__main__":
    parser=argparse.ArgumentParser();parser.add_argument("source");parser.add_argument("target");parser.add_argument("output");args=parser.parse_args();result=run(args.source,args.target);Path(args.output).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8");print(json.dumps(result,ensure_ascii=False,indent=2));raise SystemExit(result["status"]!="PASS")
