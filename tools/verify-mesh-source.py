import json,os,math,unreal
mesh=unreal.load_asset('/Engine/BasicShapes/Cube.Cube')
text=unreal.EMBlueprintLibrary.read_mesh_source(mesh)
if text.startswith('ERROR:'):raise RuntimeError(text)
data=json.loads(text)
if not data['vertices'] or len(data['triangles'])%3 or len(data['normals'])!=len(data['vertices']):raise RuntimeError('Invalid actual mesh description topology')
for normal in data['normals']:
    if abs(math.sqrt(sum(normal[a]**2 for a in ('x','y','z')))-1)>1e-5:raise RuntimeError('Actual source normal is not unit length')
with open(os.path.join(unreal.Paths.project_dir(),'mesh-source-verification.json'),'w',encoding='utf-8') as f:json.dump({'status':'PASS','engine':unreal.SystemLibrary.get_engine_version(),'actualSourceMeshDescription':True,'triangles':len(data['triangles'])//3,'vertices':len(data['vertices'])},f,indent=2)
unreal.log('ACTUAL_MESH_SOURCE_PASS')
