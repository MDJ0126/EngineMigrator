"""Scene instances of the closed, natively compiled MonoBehaviour/Actor subset."""
import copy, json, os, math
import unreal
import project_assets as shared
import runtime_templates

KEY="EngineMigratorBehaviour"
SOURCES="EngineMigratorScriptSources"
GRAPHS="EngineMigratorActorGraphs"

def native_field(actor,name):
    # New native headers avoid AActor.Owner; older archives and authored Blueprint
    # classes keep their own property schema. Never rename serialized source keys.
    if name=="Owner":
        try:
            actor.get_editor_property("EM_Field_Owner")
            return "EM_Field_Owner"
        except Exception:pass
    return name
def class_name(actor):return actor.get_class().get_name().removesuffix("_C") if actor.get_class().get_path_name().startswith("/Game/") else actor.get_class().get_name()

def is_behaviour(actor):return bool(unreal.EditorAssetLibrary.get_metadata_tag(actor,KEY))
def native_blueprint(actor):
    if is_behaviour(actor):return False
    if any(unreal.EditorAssetLibrary.get_metadata_tag(actor,k) for k in ("EngineMigratorUI","EngineMigratorAnimation")):return False
    return actor.get_class().get_path_name().startswith("/Game/") and actor.get_class().get_path_name().endswith("_C")

def import_all(package,nodes,subsystem,base):
    actors=[]
    fixed_step=float(package.get("timing",{}).get("fixedDeltaTime",0.02))
    if not math.isfinite(fixed_step) or fixed_step<=0 or round(fixed_step*141120000)<1:raise RuntimeError("Invalid project fixedDeltaTime")
    with open(os.path.join(unreal.Paths.project_dir(),"EngineMigratorRun.json"),encoding="utf-8-sig") as stream:runtime_scenes=json.load(stream).get("runtimeScenes",{})
    graph_classes={}
    if package.get("actorGraphs"):
        data=copy.deepcopy(package["actorGraphs"]);data["assetRoot"]=(shared.root if shared.enabled else base)+"/ActorBlueprints"
        if shared.enabled and shared.graph is not None:graph=copy.deepcopy(shared.graph)
        else:
            result=unreal.EMBlueprintLibrary.actor_graph(json.dumps(data))
            if result.startswith("ERROR:"):raise RuntimeError(result)
            graph=json.loads(result)
            if shared.enabled:shared.graph=copy.deepcopy(graph)
        package["actorGraphs"]=graph
        graph_classes={c["name"]:c["asset"]+"_C" for c in graph["classes"]}
    for node in package["nodes"]:
        for order,source in enumerate(node.get("behaviours",[])):
            cls=unreal.load_class(None,graph_classes.get(source["className"],"/Script/EMActorPort."+source["className"]))
            if not cls:raise RuntimeError("Compiled scene class missing: "+source["className"])
            owner=nodes[node["id"]]
            actor=subsystem.spawn_actor_from_class(cls,owner.get_actor_location(),owner.get_actor_rotation())
            actor.set_actor_label(owner.get_actor_label()+" : "+source["className"])
            if cls.get_path_name().startswith("/Script/EMActorPort."):actor.set_editor_property("EM_BehaviourEnabled",source["enabled"])
            elif not source["enabled"]:raise RuntimeError("Disabled behaviour requires native lifecycle backend")
            elif not node.get("effectiveActive",node["active"]):raise RuntimeError("Inactive object requires native lifecycle backend")
            actor.attach_to_actor(owner,unreal.Name("None"),unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,False)
            actor.tags=[unreal.Name(source["id"]),unreal.Name("EMComponentOrder:"+str(order)),unreal.Name("EMFixedStep:"+format(fixed_step,".9g")),unreal.Name("EMFixedTicks:"+str(round(fixed_step*141120000)))]+[unreal.Name("EMScene:"+key+"="+value) for key,value in runtime_scenes.items()]+([unreal.Name(runtime_templates.TEMPLATE)] if node.get("runtimeTemplate") else [])
            script_orders={os.path.splitext(os.path.basename(s["name"]))[0]:s.get("executionOrder",0) for s in package.get("scripts",[])}
            actor.tags=list(actor.tags)+[unreal.Name("EMScriptOrder:"+str(script_orders.get(source["className"],0)))]+[unreal.Name("EMScriptClassOrder:"+name+"="+str(value)) for name,value in script_orders.items()]

            unreal.EditorAssetLibrary.set_metadata_tag(actor,KEY,json.dumps({"node":node["id"],"source":source},separators=(",",":")))
            actors.append((actor,node["id"],source))
    components={source["id"]:actor for actor,node,source in actors}
    for actor,node,source in actors:
        for field in source["fields"]:actor.set_editor_property(native_field(actor,field["name"]),object_field(field,nodes,components) if field["kind"] in ("node","prefab","transform","component") else field_value(field))
    return actors

def persist(package,world,actors):
    unreal.EditorAssetLibrary.set_metadata_tag(world,SOURCES,json.dumps(package.get("scripts",[]),separators=(",",":")))
    if package.get("actorGraphs"):unreal.EditorAssetLibrary.set_metadata_tag(world,GRAPHS,json.dumps(package["actorGraphs"]))
    for actor,node,source in actors:unreal.EditorAssetLibrary.set_metadata_tag(actor,KEY,json.dumps({"node":node,"source":source},separators=(",",":")))

def verify_all(package,nodes,actors):
    mapped={str(a.tags[0]):a for a in actors if a.tags and is_behaviour(a)}
    bindings=[]
    for node in package["nodes"]:
        for order,source in enumerate(node.get("behaviours",[])):
            actor=mapped.get(source["id"])
            if not actor or class_name(actor)!=source["className"] or actor.get_attach_parent_actor()!=nodes[node["id"]]:raise RuntimeError("Behaviour class/owner reference failed reload")
            if actor.get_class().get_path_name().startswith("/Script/EMActorPort.") and actor.get_editor_property("EM_BehaviourEnabled")!=source["enabled"]:raise RuntimeError("Behaviour enabled state failed reload")
            for field in source["fields"]:
                value=actor.get_editor_property(native_field(actor,field["name"]))
                if field["kind"] in ("node","prefab","transform","component"):
                    if value!=object_field(field,nodes,mapped):raise RuntimeError("Behaviour object reference failed reload: "+field["name"])
                elif field["kind"] in ("vector3","quaternion"):
                    expected=json.loads(field["value"])
                    if any(abs(value.get_editor_property(k.upper())-expected[k])>1e-6 for k in (("x","y","z","w") if field["kind"]=="quaternion" else ("x","y","z"))):raise RuntimeError("Behaviour vector state failed reload: "+field["name"])
                else:
                    expected=field_value(field)
                    if abs(value-expected)>1e-6*max(1,abs(expected)):raise RuntimeError("Behaviour serialized state failed reload: "+field["name"])
            bindings.append({"sourceId":source["id"],"target":actor.get_path_name(),"kind":source["className"],"references":[node["id"]]+[f["value"] for f in source["fields"] if f["kind"] in ("node","prefab","transform","component") and f["value"]]})
    if len(mapped)!=len(bindings):raise RuntimeError("Unmatched behaviour instances")
    return bindings

def export_all(world,actors,node_ids,package):
    raw=unreal.EditorAssetLibrary.get_metadata_tag(world,SOURCES);package["scripts"]=json.loads(raw) if raw else []
    raw=unreal.EditorAssetLibrary.get_metadata_tag(world,GRAPHS)
    assets=[c["asset"] for c in json.loads(raw)["classes"]] if raw else []
    native=[a for a in actors if native_blueprint(a)]
    assets+= [a.get_class().get_path_name()[:-2] for a in native]
    if assets:
        result=unreal.EMBlueprintLibrary.actor_graph(json.dumps({"assets":list(dict.fromkeys(assets))}))
        if result.startswith("ERROR:"):raise RuntimeError(result)
        package["actorGraphs"]=json.loads(result)
    values={}
    for actor in actors:
        raw=unreal.EditorAssetLibrary.get_metadata_tag(actor,KEY)
        if not raw:continue
        data=json.loads(raw);source=copy.deepcopy(data["source"]);parent=actor.get_attach_parent_actor()
        try:source["enabled"]=actor.get_editor_property("EM_BehaviourEnabled")
        except Exception:pass
        if parent not in node_ids:raise RuntimeError("Behaviour lost mapped owner")
        if class_name(actor)!=source["className"]:raise RuntimeError("Behaviour native class changed")
        for field in source["fields"]:
            value=actor.get_editor_property(native_field(actor,field["name"]))
            if field["kind"]=="prefab":
                identity=runtime_templates.prefab_id(value) if value is not None else ""
                if value is not None and (not identity or runtime_templates.TEMPLATE not in [str(t) for t in value.tags]):raise RuntimeError("Prefab field no longer references a runtime prototype")
                field["value"]=identity
            elif field["kind"]=="component":
                if value is not None and (value not in actors or not is_behaviour(value)):raise RuntimeError("Unmapped component reference")
                field["value"]=json.loads(unreal.EditorAssetLibrary.get_metadata_tag(value,KEY))["source"]["id"] if value is not None else ""
            elif field["kind"] in ("node","transform"):
                if value is not None and value not in node_ids:raise RuntimeError("Behaviour references an unmapped actor")
                field["value"]=node_ids[value] if value is not None else ""
            elif field["kind"] in ("vector3","quaternion"):field["value"]=json.dumps({k:value.get_editor_property(k.upper()) for k in (("x","y","z","w") if field["kind"]=="quaternion" else ("x","y","z"))})
            else:field["value"]=("true" if value else "false") if field["kind"]=="bool" else repr(value)
        values.setdefault(node_ids[parent],[]).append(source)
    if native:
        classes={c["name"]:c for c in package["actorGraphs"]["classes"]}
        for actor in native:
            if actor not in node_ids:raise RuntimeError("Native Blueprint actor has no mapped GameObject")
            name=class_name(actor);fields=[];current=name
            while current!="MonoBehaviour":
                definition=classes[current]
                for field in definition["fields"]:
                    if field.get("access","public")!="public" and not field.get("serialized",False):continue
                    value=actor.get_editor_property(native_field(actor,field["name"]))
                    fields.append({"name":field["name"],"kind":"bool" if field["type"]=="bool" else "float","value":("true" if value else "false") if field["type"]=="bool" else repr(value)})
                current=definition["parent"]
            identity=node_ids[actor];values.setdefault(identity,[]).append({"id":identity+"_behaviour_0","className":name,"enabled":True,"fields":fields})
    for node in package["nodes"]:node["behaviours"]=values.get(node["id"],[])

def field_value(field):
    if field["kind"] in ("vector3","quaternion"):
        value=json.loads(field["value"]);result=unreal.Vector4f() if field["kind"]=="quaternion" else unreal.Vector3f()
        for axis in (("x","y","z","w") if field["kind"]=="quaternion" else ("x","y","z")):result.set_editor_property(axis.upper(),value[axis])
        return result
    return field["value"]=="true" if field["kind"]=="bool" else int(field["value"]) if field["kind"]=="int" else float(field["value"])

def object_field(field,nodes,components=None):
    if not field["value"]:return None
    if field["kind"]=="component":return components[field["value"]]
    if field["kind"] in ("node","transform"):return nodes[field["value"]]
    values=[a for a in nodes.values() if runtime_templates.prefab_id(a)==field["value"]]
    if len(values)!=1:raise RuntimeError("Runtime prefab prototype missing or duplicated: "+field["value"])
    return values[0]

