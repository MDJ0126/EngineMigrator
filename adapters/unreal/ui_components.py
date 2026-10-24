"""Overlay uGUI -> saved UMG WidgetBlueprint + runtime Blueprint presenter."""
import copy,json,math,hashlib
import unreal
KEY="EngineMigratorUI"
def helper(actor):return bool(unreal.EditorAssetLibrary.get_metadata_tag(actor,KEY))

def members(package,root):
    nodes={n["id"]:n for n in package["nodes"]}
    def belongs(n):
        seen=set()
        while n:
            if n["id"] in seen:raise RuntimeError("UI hierarchy cycle")
            seen.add(n["id"])
            if n["id"]==root:return True
            n=nodes.get(n.get("parent"))
        return False
    return [n for n in package["nodes"] if n.get("hasUI") and belongs(n)]

def import_all(package,subsystem,base,textures=None):
    actors=[]
    nodes={str(a.tags[0]):a for a in subsystem.get_all_level_actors() if a.tags and str(a.tags[0]).startswith("node_")}
    for source in package["nodes"]:
        if not source.get("hasUI"):continue
        owner=nodes[source["id"]];data=source["ui"];tags=list(owner.tags)
        if data["canvas"]:
            tags.append(unreal.Name("EMUI:Canvas"))
            if not data["enabled"]:tags.append(unreal.Name("EMUICanvasDisabled"))
        if data["visual"]=="button":
            tags.append(unreal.Name("EMUI:Button"))
            if not data["interactable"]:tags.append(unreal.Name("EMUIButtonNonInteractable"))
            if not data.get("buttonEnabled",True):tags.append(unreal.Name("EMUIButtonDisabled"))
        owner.tags=tags
    for node in package["nodes"]:
        if not node.get("hasUI") or not node["ui"]["canvas"]:continue
        elements=copy.deepcopy(members(package,node["id"]));classes={c["name"]:c["asset"]+"_C" for c in package.get("actorGraphs",{}).get("classes",[])}
        for element in elements:
            identity=element["ui"].get("texture","")
            if identity and (textures is None or identity not in textures):raise RuntimeError("UI texture asset reference missing: "+identity)
            element["ui"]["texturePath"]=textures[identity].get_path_name() if identity else ""
            for callback in element["ui"].get("onClick",[]):callback["targetClass"]=classes.get(callback["className"],"/Script/EMActorPort."+callback["className"])
        result=unreal.EMBlueprintLibrary.map_ui(json.dumps({"mode":"create","asset":base+"/UI/"+node["id"],"nodes":elements}))
        if result.startswith("ERROR:"):raise RuntimeError(result)
        data=json.loads(result);data["owner"]=node["id"]
        runtime=node.get("runtimeTemplate",False)
        actor=subsystem.spawn_actor_from_class(unreal.load_class(None,"/Script/EMUIRuntime.EMPortUIPresenter"),unreal.Vector());actor.tags=[unreal.Name("EM_UI"),unreal.Name(node["id"]),unreal.Name("EMRuntimeUI")]+([unreal.Name("EMRuntimeTemplate")] if runtime else []);actor.set_actor_label("UI "+node["name"])
        actor.set_owner(nodes[node["id"]]);actor.set_editor_property("EM_WidgetClass",unreal.load_class(None,data["widget"]+"_C"))
        actors.append((actor,data));unreal.EditorAssetLibrary.set_metadata_tag(actor,KEY,json.dumps(data))
    return actors

def persist(actors):
    for actor,data in actors:unreal.EditorAssetLibrary.set_metadata_tag(actor,KEY,json.dumps(data))

def export_all(actors,package,texture_export=None):
    for actor in actors:
        raw=unreal.EditorAssetLibrary.get_metadata_tag(actor,KEY)
        if not raw:continue
        data=json.loads(raw);nodes=members(package,data["owner"])
        result=unreal.EMBlueprintLibrary.map_ui(json.dumps({"mode":"read","asset":data["widget"],"nodes":nodes}))
        if result.startswith("ERROR:"):raise RuntimeError(result)
        restored={n["id"]:n["ui"] for n in json.loads(result)["nodes"]}
        behaviours={b["id"]:(n,b) for n in package["nodes"] for b in n.get("behaviours",[])}
        for node in nodes:
            original=node["ui"];node["ui"]=restored[node["id"]];actual=node["ui"];actual.setdefault("buttonEnabled",True);path=actual.get("texturePath","")
            if path:
                texture=unreal.load_asset(path)
                if not isinstance(texture,unreal.Texture2D):raise RuntimeError("UI brush texture is not a Texture2D")
                identity=texture_export(texture) if texture_export else unreal.EditorAssetLibrary.get_metadata_tag(texture,"EngineMigratorSourceId")
                if not identity or texture_export is None and identity!=original.get("texture",""):raise RuntimeError("UI native texture reference changed after reload")
                actual["texture"]=identity
                if actual.get("sprite"):
                    changed=identity!=original.get("texture") or any(abs(actual["uv"][key]-original["uv"][key])>1e-7 for key in ("x","y","z","w"))
                    if changed:
                        uv=actual["uv"];width=actual["textureWidth"];height=actual["textureHeight"];actual["spriteRect"]={"x":uv["x"]*width,"y":uv["y"]*height,"z":uv["z"]*width,"w":uv["w"]*height}
                        descriptor={key:actual[key] for key in ("texture","spriteRect","spritePivot","pixelsPerUnit")};actual["sprite"]=hashlib.sha256(json.dumps(descriptor,sort_keys=True).encode()).hexdigest()[:32]
            else:
                actual["texture"]=""
                if actual.get("sprite"):actual.update(sprite="",spriteName="",spriteRect={"x":0,"y":0,"z":0,"w":0},spritePivot={"x":0,"y":0},pixelsPerUnit=100)
            for callback in node["ui"].get("onClick",[]):
                target=behaviours.get(callback["behaviour"])
                if not target:raise RuntimeError("Runtime UI callback references an unmapped component")
                callback["target"]=target[0]["id"];callback["className"]=target[1]["className"]

def verify_all(actors,package):
    restored=copy.deepcopy(package);export_all(actors,restored)
    def same(a,b):
        if isinstance(a,dict):return all(k in b and same(v,b[k]) for k,v in a.items())
        if isinstance(a,list):return len(a)==len(b) and all(same(x,y) for x,y in zip(a,b))
        if isinstance(a,(int,float)) and not isinstance(a,bool):return math.isclose(a,b,abs_tol=1e-5,rel_tol=1e-6)
        return a==b
    for a,b in zip(package["nodes"],restored["nodes"]):
        if a.get("hasUI") and not same(a["ui"],b["ui"]):raise RuntimeError("Native UMG value/layout mismatch after reload: "+json.dumps(b["ui"]))
