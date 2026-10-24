"""Compare actual reexported project units, resource pixels and prefab memberships."""
import importlib.util,json,sys
from pathlib import Path
def module(name,file):
 spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(file));result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result
scene=module('scene_compare','compare-scene-packages.py');resource=module('resource_compare','compare-resource-packages.py')
source,target,output=map(Path,sys.argv[1:4]);read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
a=read(source/'project-package.json');b=read(target/'project-package.json');other={u['id']:u for u in b['units']};errors=[];results=[];script_identities_compared=set()
if set(other)!={u['id'] for u in a['units']}:errors.append('Project unit identities differ')
for unit in a['units']:
 if unit['id'] not in other:continue
 restored=other[unit['id']]
 if unit['kind']!=restored['kind'] or unit['enabled']!=restored['enabled']:errors.append('Unit kind/build-enabled flag differs: '+unit['id'])
 first=source/unit['package']/'package.json';last=target/restored['package']/'package.json';left=read(first);right=read(last)
 data=scene.run(left,right);pixels=resource.run(first,last);members={n['id']:n for n in right['nodes']}
 script_identities={Path(s['name']).name:s for s in right.get('scripts',[])}
 for script in left.get('scripts',[]):
  if script.get('guid'):
   script_identities_compared.add((Path(script['name']).name,script['guid'].lower(),script.get('executionOrder',0)))
   actual=script_identities.get(Path(script['name']).name,{})
   if actual.get('guid','').lower()!=script['guid'].lower() or actual.get('executionOrder',0)!=script.get('executionOrder',0):errors.append('Script GUID/execution order differs: '+unit['id']+'/'+script['name'])
 for node in left['nodes']:
  if (node.get('prefab') or {}).get('asset') and node.get('prefab')!=members.get(node['id'],{}).get('prefab'):errors.append('Prefab membership differs: '+unit['id']+'/'+node['id'])
 results.append({'unit':unit['id'],'kind':unit['kind'],'data':data,'resources':pixels})
 if data['status']!='PASS' or pixels['status']!='PASS':errors.append('Actual unit data/resources differ: '+unit['id'])
result={'status':'FAIL' if errors else 'PASS','unitsCompared':len(results),'sceneCount':sum(u['kind']=='scene' for u in a['units']),'prefabDefinitions':sum(u['kind']=='prefab' for u in a['units']),'nodesCompared':sum(r['data']['nodesCompared'] for r in results),'scriptIdentitiesCompared':len(script_identities_compared),'errors':errors,'results':results,'projectPortComplete':False,'renderingEquivalent':False}
output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(result['status'],result['unitsCompared'],'units',result['nodesCompared'],'nodes');raise SystemExit(bool(errors))
