"""Verify real saved physics components, sequence bindings/channels, UMG and Actor graphs."""
import json,os,sys
import unreal
sys.path.insert(0,os.path.dirname(__file__))
import scene_components as scene,script_components as scripts,physics_components as physics,animation_components as animations,ui_components as ui
with open(os.path.join(unreal.Paths.project_dir(),"ExtensionVerify.json"),encoding="utf-8-sig") as f:config=json.load(f)
with open(os.path.join(config["package"],"package.json"),encoding="utf-8-sig") as f:package=json.load(f)
world=unreal.EditorLoadingAndSavingUtils.load_map(config["map"])
actors=unreal.get_editor_subsystem(unreal.EditorActorSubsystem).get_all_level_actors()
nodes={str(a.tags[0]):a for a in actors if a.tags and str(a.tags[0]).startswith("node_") and not scripts.is_behaviour(a)}
for node in package["nodes"]:physics.verify(nodes[node["id"]],node)
scripts.verify_all(package,nodes,actors);animations.verify_all(actors,{a:i for i,a in nodes.items()},package);ui.verify_all(actors,package)
scripts.export_all(world,actors,{a:i for i,a in nodes.items()},package)
os.makedirs(config["output"],exist_ok=True)
with open(os.path.join(config["output"],"actor-graph.json"),"w",encoding="utf-8") as f:json.dump(package["actorGraphs"],f,indent=2)
with open(os.path.join(config["output"],"verification.json"),"w",encoding="utf-8") as f:json.dump({"status":"PASS","actualMapReloaded":True,"physicsComponents":sum(n.get("hasPhysics",False) for n in package["nodes"]),"animationComponents":sum(n.get("hasAnimation",False) for n in package["nodes"]),"uiNodes":sum(n.get("hasUI",False) for n in package["nodes"]),"actualActorBlueprintClasses":len(package["actorGraphs"]["classes"]),"projectPortComplete":False},f,indent=2)
unreal.log("ENGINE_MIGRATOR_EXTENSIONS_RELOAD_VERIFIED")
