"""Verify actual clone/destruction/world travel captures for computed scene indices."""
from pathlib import Path
import hashlib
import importlib.util
import json

if not __debug__:raise RuntimeError('Assertions must remain enabled')
root=Path(__file__).resolve().parent.parent
home=root/'artifacts/dynamic-scene-check'
spec=importlib.util.spec_from_file_location('world_comparison',root/'tools/compare-runtime-world.py')
world=importlib.util.module_from_spec(spec);spec.loader.exec_module(world)
prefab_spec=importlib.util.spec_from_file_location('prefab_comparison',root/'tools/compare-runtime-prefab.py')
prefab=importlib.util.module_from_spec(prefab_spec);prefab_spec.loader.exec_module(prefab)
output=home/'roundtrip-verification.json'

def sha(path):
 with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest().upper()

try:
 paths=[home/'unity-original.json',home/'native-execution.json',home/'unity-restored.json']
 summaries=[];calls=[]
 for path in paths:
  data=world.load(path)
  if data['status']!='CAPTURED':raise RuntimeError('Incomplete actual engine capture: '+str(path))
  summaries.append(prefab.summarize(path))
  observed=[]
  for frame in data['frames']:
   for actor in frame['actors']:
    if actor['class']!='RuntimeSpawner':continue
    state=actor['state'];tick=state['Frame'];count=state['NextCalls']
    if type(tick) is not int or type(count) is not int or type(state['SceneOffset']) is not int or state['SceneOffset']!=0 or count!=(0 if tick<40 else 1):raise RuntimeError('Dynamic index function evaluation differs')
    observed.append([tick,count])
  if not observed:raise RuntimeError('Source driver execution missing')
  calls.append(observed)
 if not summaries[0]==summaries[1]==summaries[2]:raise RuntimeError('Clone/destruction/lifecycle/reference/world-travel summaries differ')
 target=root/'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Target/Plugins/EMActorPort/Source/EMActorPort/Public/RuntimeSpawner.h'
 if sha(target)!=sha(home/'RuntimeSpawner.h'):raise RuntimeError('Compiled input header changed')
 restored_files=list((home/'full-restored-scripts').glob('*.cs'))
 if len(restored_files)!=49:raise RuntimeError('Full project reverse script set incomplete')
 if sha(home/'full-restored-scripts/RuntimeSpawner.cs')!=sha(home/'runtime-spawner-restored.cs.txt'):raise RuntimeError('Full-project reverse output differs from actual restored Unity input')
 baseline=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
 if not all(Path(p).is_file() and sha(p)==h.upper() for p,h in baseline.items()):raise RuntimeError('Original project changed')
 result={'status':'PASS','actualUnityOriginal':True,'actualUnrealWorldTravel':True,'actualPrefabInstantiation':True,'actualUnityRestored':True,'checks':summaries[0],'dynamicIndexCalls':calls,'fullProjectHeadersRestored':len(restored_files),'restoredExecutedSourceSha256':sha(home/'runtime-spawner-restored.cs.txt'),'sourceCompiledSha256':sha(target),'captureHashes':{str(p):sha(p) for p in paths},'originalFilesUnchanged':len(baseline),'wholeProjectComplete':False}
except Exception as error:
 output.write_text(json.dumps({'status':'FAIL','error':str(error)},indent=2),encoding='utf-8');raise
output.write_text(json.dumps(result,indent=2),encoding='utf-8')
print('DYNAMIC_SCENE_ACTUAL_ROUNDTRIP_PASS: clone/destruction/reference checkpoints and three-scene travel')
