"""Import/export tracked scene and prefab-definition maps with shared actual assets."""
import copy,json,os,sys
import unreal
sys.path.insert(0,os.path.dirname(__file__))
import project_assets as shared
import runtime_templates
import runtime_prototype_guard
project=unreal.Paths.project_dir();config_file=os.path.join(project,"EngineMigratorRun.json")
with open(config_file,encoding="utf-8-sig") as f:job=json.load(f)
root=os.path.abspath(job["projectPackage"])
manifest_file=os.path.join(root,"project-package.json")
if job["mode"]=="import":
    with open(manifest_file,encoding="utf-8-sig") as f:manifest=json.load(f)
    shared.enabled=True;shared.root=job["assetRoot"]+"/Shared"
    for cache in (shared.textures,shared.materials,shared.meshes):cache.clear()
    shared.graph=None
else:
    with open(os.path.join(project,".EngineMigratorProject.json"),encoding="utf-8-sig") as f:manifest=json.load(f)
    manifest=copy.deepcopy(manifest)
results=[]
referenced_prefabs=set()
if job["mode"]=="import":
    for entry in manifest["units"]:
        with open(os.path.join(root,entry["package"],"package.json"),encoding="utf-8-sig") as stream:
            data=json.load(stream)
        referenced_prefabs.update(f["value"] for n in data["nodes"] for b in n.get("behaviours",[]) for f in b["fields"] if f["kind"]=="prefab" and f["value"])
runtime_scenes={}
if job["mode"]=="import":
    active=[u for u in manifest["units"] if u["kind"]=="scene" and u["enabled"]]
    for index,u in enumerate(active):
        target=job["assetRoot"]+"/Units/"+u["id"]+"/Maps/"+u["id"]
        runtime_scenes["#"+str(index)]=target
        for source in (u["path"],u.get("actualPath","")):
            if source:runtime_scenes[os.path.splitext(source)[0]]=target
        if sum(v["name"].casefold()==u["name"].casefold() for v in active)==1:runtime_scenes[u["name"]]=target
for unit in manifest["units"]:
    folder=os.path.abspath(os.path.join(root,unit["package"]))
    if os.path.commonpath((root,folder))!=root:raise RuntimeError("Project package path escape")
    os.makedirs(folder,exist_ok=True)
    with open(os.path.join(root,"project-progress.json"),"w",encoding="utf-8") as f:json.dump({"current":unit,"verifiedUnits":len(results),"projectPortComplete":False},f,indent=2)
    settings={"package":folder,"assetRoot":job.get("assetRoot","")+"/Units/"+unit["id"],"projectUnit":unit} if job["mode"]=="import" else {"reversePackage":folder,"map":unit["map"]}
    if job["mode"]=="import":settings["runtimeScenes"]=runtime_scenes
    with open(config_file,"w",encoding="utf-8") as f:json.dump(settings,f)
    script=os.path.join(os.path.dirname(__file__),"resource_bridge.py" if job["mode"]=="import" else "export_resources.py")
    namespace={"__file__":script,"__name__":"__main__"}
    original=runtime_templates.stage(folder,manifest,root) if job["mode"]=="import" else None
    try:exec(compile(open(script,encoding="utf-8-sig").read(),script,"exec"),namespace)
    finally:
        if original is not None:
            with open(os.path.join(folder,"package.json"),"wb") as f:f.write(original)
    if job["mode"]=="import":
        with open(os.path.join(folder,"unreal-import-result.json"),encoding="utf-8-sig") as f:result=json.load(f)
        if not result["savedAndReloaded"] or not result["referencesVerified"]:raise RuntimeError("Unit save/reference verification failed")
        unit["map"]=result["savedMap"];results.append(result)
        if unit["kind"]=="scene" or unit["id"] in referenced_prefabs:
            current=unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
            runtime_prototype_guard.persist(current,unreal.get_editor_subsystem(unreal.EditorActorSubsystem),unit["map"],unit["kind"]=="prefab")
    else:
        package=namespace["package"]
        if unit.get("nodeCount",0)>0 and not package["nodes"]:raise RuntimeError("Tracked map lost all scene nodes: "+unit["map"])
        package["scene"]=unit["id"]
        with open(os.path.join(folder,"package.json"),"w",encoding="utf-8") as f:json.dump(package,f,indent=2)
        results.append({"map":unit["map"]})
with open(manifest_file,"w",encoding="utf-8") as f:json.dump(manifest,f,indent=2)
if job["mode"]=="import":
    with open(os.path.join(project,".EngineMigratorProject.json"),"w",encoding="utf-8") as f:json.dump(manifest,f,indent=2)
    bindings=[]
    for unit,result in zip(manifest["units"],results):
        prototype_ids={p["sourceId"] for p in result.get("runtimePrefabPrototypes",[])}
        for binding in result["bindings"]:
            binding=copy.deepcopy(binding);binding["sourceId"]=unit["id"]+"::"+binding["sourceId"];binding["references"]=[unit["id"]+"::"+r if r.startswith("node_") or r in prototype_ids else r for r in binding["references"]];bindings.append(binding)
    combined={"savedAndReloaded":True,"referencesVerified":True,"projectPortComplete":False,"renderingEquivalent":False,"savedMap":next(u["map"] for u in manifest["units"] if u["kind"]=="scene" and u["enabled"]),"maps":manifest["units"],"bindings":bindings,"sharedAssets":{"textures":len(shared.textures),"materials":len(shared.materials),"meshes":len(shared.meshes)},"prefabRepresentation":"definition maps plus explicit instance links; scene actors are expanded"}
    combined["warnings"]=list(dict.fromkeys(message for result in results for message in result.get("warnings",[])))
    with open(os.path.join(root,"unreal-import-result.json"),"w",encoding="utf-8") as f:json.dump(combined,f,indent=2)
unreal.log("ENGINE_MIGRATOR_PROJECT_VERIFIED")

