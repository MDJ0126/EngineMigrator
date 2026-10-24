"""Cookable, dormant per-level prefab prototypes. Export never mistakes these helpers for scene objects."""
import copy,json,os,shutil
import unreal
TEMPLATE="EMRuntimeTemplate"
PREFAB="EMRuntimePrefab:"

def prefab_id(actor):
    return next((str(t)[len(PREFAB):] for t in actor.tags if str(t).startswith(PREFAB)),"")

def stage(folder,manifest,root):
    path=os.path.join(folder,"package.json")
    with open(path,"rb") as f:original=f.read()
    package=json.loads(original.decode("utf-8-sig"))
    references={f["value"] for n in package["nodes"] for b in n.get("behaviours",[]) for f in b["fields"] if f["kind"]=="prefab" and f["value"]}
    if not references:return None
    definitions={u["id"]:u for u in manifest["units"] if u["kind"]=="prefab"}
    # Build the transitive catalog before materializing nodes. A visited set
    # terminates self and mutual references without duplicating prototypes.
    pending=list(sorted(references));loaded={}
    while pending:
        identity=pending.pop(0)
        if identity in loaded:continue
        if identity not in definitions:raise RuntimeError("Runtime prefab definition missing: "+identity)
        source=os.path.join(root,definitions[identity]["package"])
        with open(os.path.join(source,"package.json"),encoding="utf-8-sig") as f:data=json.load(f)
        loaded[identity]=data
        pending.extend(sorted({f["value"] for n in data["nodes"] for b in n.get("behaviours",[]) for f in b["fields"] if f["kind"]=="prefab" and f["value"]}-loaded.keys()))
    references=set(loaded)
    for identity in sorted(references):
        unit=definitions[identity];source=os.path.join(root,unit["package"])
        with open(os.path.join(source,"package.json"),encoding="utf-8-sig") as f:data=json.load(f)
        if len([n for n in data["nodes"] if not n.get("parent")])!=1:raise RuntimeError("Runtime prefab must have one root")
        for n in data["nodes"]:
            if any(n.get(k,False) for k in ("hasCamera","hasLight","hasVolume")):raise RuntimeError("Runtime prefab camera/light/volume is unsupported: "+identity+" / "+n["name"])
        for kind in ("textures","meshes","materials","scripts"):
            key="name" if kind=="scripts" else "id";known={v[key]:v for v in package[kind]}
            for value in data[kind]:
                if value[key] in known:
                    if value!=known[value[key]]:raise RuntimeError("Runtime prefab resource identity collision")
                    continue
                package[kind].append(copy.deepcopy(value))
                if kind in ("textures","meshes"):
                    destination=os.path.abspath(os.path.join(folder,value["file"]));source_file=os.path.abspath(os.path.join(source,value["file"]))
                    if os.path.commonpath((os.path.abspath(folder),destination))!=os.path.abspath(folder) or os.path.commonpath((os.path.abspath(source),source_file))!=os.path.abspath(source):raise RuntimeError("Prefab resource path escape")
                    os.makedirs(os.path.dirname(destination),exist_ok=True)
                    if os.path.exists(destination):
                        if open(destination,"rb").read()!=open(source_file,"rb").read():raise RuntimeError("Runtime prefab file collision")
                    else:shutil.copyfile(source_file,destination)
        ids={n["id"]:"node_rt_"+identity+"_"+n["id"] for n in data["nodes"]}
        components={b["id"]:"node_rt_"+identity+"_"+b["id"] for n in data["nodes"] for b in n.get("behaviours",[])}
        for source_node in data["nodes"]:
            n=copy.deepcopy(source_node);n["id"]=ids[n["id"]];n["parent"]=ids.get(n.get("parent"));n["prefab"]=None;n["runtimeTemplate"]=True;n["runtimePrefab"]=identity if not n["parent"] else ""
            if n.get("hasUI"):
                message="런타임 UI는 UMG 화면 레이아웃으로 변환됩니다. UI Transform의 화면 좌표·배율을 일반 Actor Transform과 동등하게 사용하는 스크립트 API 변환은 아직 미지원입니다."
                if message not in package.setdefault("warnings",[]):package["warnings"].append(message)
                for call in n["ui"].get("onClick",[]):
                    call["target"]=ids[call["target"]];call["behaviour"]=components[call["behaviour"]]
            if n.get("hasAnimation"):
                for clip in n["animation"]["clips"]:
                    for curve in clip["curves"]:curve["target"]=ids[curve["target"]]
            for b in n.get("behaviours",[]):
                b["id"]="node_rt_"+identity+"_"+b["id"]
                for f in b["fields"]:
                    if f["kind"] in ("node","transform") and f["value"]:f["value"]=ids[f["value"]]
                    if f["kind"]=="component" and f["value"]:f["value"]=components[f["value"]]
            package["nodes"].append(n)
    with open(path,"w",encoding="utf-8") as f:json.dump(package,f,indent=2)
    return original

def suppress(world,actors,subsystem,map_path):
    marked=[a for a in subsystem.get_all_level_actors() if TEMPLATE in [str(t) for t in a.tags]]
    if not marked:return
    for actor in marked:
        shapes=[c for c in actor.get_components_by_class(unreal.ShapeComponent) if c.get_name()=="EM_Physics"]
        for shape in shapes:
            state=json.loads(unreal.EMBlueprintLibrary.read_physics(actor))
            if not state["kinematic"]:
                actor.tags=list(actor.tags)+[unreal.Name("EMRuntimePhysicsDynamic")]
                shape.set_simulate_physics(False)
        actor.set_actor_hidden_in_game(True);actor.set_actor_enable_collision(False);actor.set_actor_tick_enabled(False)
    if not unreal.EditorLoadingAndSavingUtils.save_map(world,map_path):raise RuntimeError("Runtime template save failed")
    if not unreal.EditorLoadingAndSavingUtils.load_map(map_path):raise RuntimeError("Runtime template reload failed")
    reloaded=[a for a in subsystem.get_all_level_actors() if TEMPLATE in [str(t) for t in a.tags]]
    if len(marked)!=len(reloaded) or any(not a.get_editor_property("hidden") or a.get_actor_enable_collision() for a in reloaded):raise RuntimeError("Runtime template suppression did not survive reload")

