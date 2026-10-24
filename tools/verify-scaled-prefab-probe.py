"""Require actual prefab asset export, native reference readback and Unity import execution."""
from pathlib import Path
import json,math,re
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;h=root/'artifacts/scaled-prefab-check';unity=root/'artifacts/runtime-ui-api-tests';core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
bindings={};floats={2}|{8+30*i+j for i in range(5)for j in[14,15,27,28]}
def bind(name,kind,folder,capture=True):
 p=h/(name+'.json')if capture else None;matches=[]
 for rp in folder.glob('engine-preflight-*.json'):
  r=load(rp)
  try:validate_receipt(r,kind,h/(name+'.log'),p,root,unity_method='EMScaledPrefabProbe.Run')
  except(RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
  matches.append((r['finishedUtc'],rp))
 require(bool(matches),'Scoped engine receipt missing '+name);rp=sorted(matches)[-1][1];bindings[name]={'receipt':str(rp),'receiptSha256':sha(rp),'captureSha256':sha(p)if p else None}
def rows(name,initial):
 d=load(h/(name+'.json'));r=d.get('rows');require(d.get('status')=='CAPTURED'and type(r)is list and len(r)==24,'Incomplete prefab capture '+name)
 for i,a in enumerate(r,1):
  require(type(a)is list and len(a)==158 and a[0]==i and all(type(x)in(int,float)and math.isfinite(x)for x in a),'Malformed prefab state '+name)
  require(a[4]==1,'Dormant native prototype ran callbacks '+name)
  for k in range(5):
   o=8+30*k
   if a[o]:require(a[o+3]==1,'Live prefab internal/self reference lost '+name)
 require(r[0][1]==initial and r[1][5:7]==[1,0] and r[2][3]==1 and r[6][7]==0,'Prefab creation return values/instance separation missing '+name)
 require(r[6][128]==1 and r[6][129:131]==[0,0] and r[6][132]==0,'Inactive prefab was not dormant '+name)
 require(r[7][129:131]==[1,1] and r[7][132]==1 and r[7][144]==1,'Paused dormant prefab activation/awake references missing '+name)
 require(r[9][8]==0 and r[13][128]==0 and r[17][68]==0 and r[17][98]==0 and r[21][38]==0,'Prefab destruction did not invalidate per-instance references '+name)
 return r
imports={}
for suffix,case,initial in [('', '100-20',.5),('','30-20',.5),('zero','100-20',0)]:
 modes=['original'+suffix,'native'+suffix,'restored'+suffix];values=[rows(m+'-'+case,initial)for m in modes]
 for mode,data in zip(modes,values):
  for i,(a,b)in enumerate(zip(values[0],data)):
   for j,(x,y)in enumerate(zip(a,b)):require(abs(x-y)<=(2e-6 if j in floats else 0),f'{mode}-{case} frame={i+1} column={j}: {x}/{y}')
  bind(mode+'-'+case,'Unreal'if mode.startswith('native')else'Unity',core if mode.startswith('native')else unity)
 name='restored'+suffix+'-'+case;result=load(h/(name+'.json.import.json'));require(result.get('behaviourInstancesVerified')==5 and len(result.get('maps',[]))==3,'Actual Unity prefab/project import verification missing '+name)
 ident=result['maps'][0]['id'].rsplit('_',1)[1];inp=h/('RestoredInput_'+ident);require(inp.is_dir(),'Actual Unity import input missing')
 manifest=load(inp/'project-package.json');require(sum(u['kind']=='prefab'for u in manifest['units'])==2,'Imported prefab definitions missing')
 for unit in manifest['units']:
  package=load(inp/unit['package']/'package.json')
  if unit['kind']=='prefab':
   require(len(package['nodes'])==2,'Prefab child graph missing')
   for node in package['nodes']:
    fields={f['name']:f for f in node['behaviours'][0]['fields']};peer=next(n for n in package['nodes']if n['id']!=node['id'])
    require(fields['Self']['value']==node['id'] and fields['Target']['value']==peer['id'] and fields['Peer']['value']==peer['behaviours'][0]['id'],'Actual native-exported prefab links lost before Unity import')
 imports[name]={'input':str(inp),'manifestSha256':sha(inp/'project-package.json'),'resultSha256':sha(h/(name+'.json.import.json'))}
clock=load(h/'originalzero-100-20.json.clock.json');require(len(clock)==24 and clock[11][1:5]==[2822398,141120000,1,1411199] and clock[11][5]==0,'Actual render-before-scale rational timing evidence missing')
counter=load(h/'scaled-tick-counterexample.json');require(counter.get('status')=='REPRODUCED_QUANTIZATION_MISMATCH'and len(counter.get('differences',[]))==56,'Scaled tick counterexample missing')
for p,d in load(h/'build-hashes.json').items():require(sha(Path(p)).lower()==d.lower(),'Native source/binary build changed '+p)
bind('build','Compiler',core,False);bind('mapszero-100-20','Unreal',core,False)
adapter=load(h/'adapter-verification.json');require(adapter.get('status')=='PASS'and adapter.get('actualUnityPrefabDefinitions')==2 and adapter.get('behaviourBindings')==5 and adapter.get('mapReloadVerified')is True,'Production prefab staging/import failed')
for name in ['ScaledPrefabDriver','ScaledPrefabWorker']:
 text=(h/'restored'/(name+'.cs')).read_text()
 for n in ['ScaledPrefabDriver','ScaledPrefabWorker']:text=re.sub(r'\b'+n+r'\b','Restored'+n,text)
 require((unity/'UnitySource/Assets/EngineMigratorScaledPrefabProbe'/('Restored'+name+'.cs')).read_text()==text,'Executed restored source changed '+name)
require(sha(root/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll')==sha(h/'runner/bin/Release/net9.0/EngineMigrator.Core.dll'),'Engine generator differs from managed-test core')
record={'status':'ACTUAL_SCALED_PREFAB_ASSET_ROUNDTRIP_PASS','goalComplete':False,'scenarios':3,'actualCaptures':9,'framesPerCapture':24,'valuesPerFrame':158,'sourcePrefabAssets':2,'actualProductionUnityImport':True,'nativeFieldReferenceExport':True,'imports':imports,'bindings':bindings,'adapter':adapter,'physicsAnimationUiParity':False,'wholeProjectCliPipelineRerun':False};(h/'verification.json').write_text(json.dumps(record,indent=2));print(record['status'])
