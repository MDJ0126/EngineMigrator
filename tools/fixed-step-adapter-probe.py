"""Verify real project fixed-step tags and component references survive map reload."""
import sys,os,json,copy,unreal
root=r'C:\Projects\EngineMigrator';sys.path.insert(0,os.path.join(root,'adapters/unreal'))
import script_components as scripts
world=unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
sub=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
nodes={name:sub.spawn_actor_from_class(unreal.StaticMeshActor,unreal.Vector(),unreal.Rotator()) for name in ('LedgerNode','WorkerNode')}
for name,actor in nodes.items():actor.tags=[unreal.Name(name)]
package={'hasTiming':True,'timing':{'timeScale':1,'fixedDeltaTime':0.009999993,'maximumDeltaTime':0.33333334},'scripts':[{'name':'FixedLedger.cs','executionOrder':-200},{'name':'FixedWorker.cs','executionOrder':0}], 'nodes':[{'id':'LedgerNode','active':True,'behaviours':[{'id':'LedgerBehaviour','className':'FixedLedger','enabled':True,'fields':[]}]},{'id':'WorkerNode','active':True,'behaviours':[{'id':'WorkerBehaviour','className':'FixedWorker','enabled':True,'fields':[{'name':'Ledger','kind':'component','value':'LedgerBehaviour'}]}]}]}
actors=scripts.import_all(package,nodes,sub,'/Game/FixedStepAdapterProbe')
scripts.persist(package,world,actors)
unreal.EditorAssetLibrary.set_metadata_tag(world,'EngineMigratorTiming',json.dumps(package['timing']))
map_path='/Game/FixedStepAdapterProbe/Mapping'
assert unreal.EditorLoadingAndSavingUtils.save_map(world,map_path)
unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True,True)
world=unreal.EditorLoadingAndSavingUtils.load_map(map_path)
assert world
all_actors=sub.get_all_level_actors()
reloaded={str(a.tags[0]):a for a in all_actors if a.tags and str(a.tags[0]) in nodes}
bindings=scripts.verify_all(package,reloaded,all_actors)
behaviours=[a for a in all_actors if scripts.is_behaviour(a)]
assert len(behaviours)==2
for actor in behaviours:
 tags=[str(t) for t in actor.tags]
 assert 'EMFixedTicks:1411199' in tags
 assert any(t.startswith('EMFixedStep:') and abs(float(t.split(':')[1])-0.009999993)<1e-10 for t in tags)
ledger=next(a for a in behaviours if scripts.class_name(a)=='FixedLedger')
assert 'EMScriptOrder:-200' in [str(t) for t in ledger.tags]
assert json.loads(unreal.EditorAssetLibrary.get_metadata_tag(world,'EngineMigratorTiming'))==package['timing']
before=len(all_actors)
for invalid in (0.0,-1.0,float('nan'),float('inf'),1e-20):
 bad=copy.deepcopy(package);bad['timing']['fixedDeltaTime']=invalid
 try:scripts.import_all(bad,reloaded,sub,'/Game/FixedStepAdapterProbe');raise AssertionError('Invalid fixed step was accepted')
 except RuntimeError as error:assert 'fixedDeltaTime' in str(error)
 assert len(sub.get_all_level_actors())==before
result={'status':'PASS','mapReloadVerified':True,'componentReferencesPreserved':True,'fixedStep':0.009999993,'fixedTicks':1411199,'scriptOrderPreserved':True,'invalidInputsRejectedBeforeSpawn':5,'bindings':bindings}
with open(os.path.join(root,'artifacts/fixed-update-check/adapter-verification.json'),'w',encoding='utf-8') as stream:json.dump(result,stream,indent=2)
print('ACTUAL_FIXED_STEP_ADAPTER_PASS')
