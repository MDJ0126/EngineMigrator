"""Mutation checks for the verifier. All receipts here are synthetic test data."""
from pathlib import Path
from datetime import datetime,timedelta,timezone
import copy,json,tempfile,subprocess,sys
from object_equality_evidence import UNITY,UNREAL,DOTNET,validate_receipt,owned,verify
from object_equality_oracle import expected_rows,require_rows,expected_flows,require_flows

root=Path(__file__).resolve().parent.parent;home=root/'artifacts/object-equality-check';passed=[]
def check(name,callback,reject=False):
 try:callback()
 except (RuntimeError,ValueError,KeyError,TypeError,IndexError):
  if not reject:raise
 else:
  if reject:raise RuntimeError('Invalid evidence accepted: '+name)
 passed.append(name)

now=datetime.now(timezone.utc)
with tempfile.TemporaryDirectory(prefix='verifier-synthetic-',dir=home)as folder:
 folder=Path(folder);capture=folder/'capture.json';capture.write_text('{}');log=folder/'test.log'
 invocation={'executable':UNITY,'arguments':['-batchmode','-nographics','-projectPath',str(folder),'-executeMethod','EMObjectEqualityProbe.Run','-logFile',str(log)],'workingDirectory':str(folder),'startedUtc':(now-timedelta(minutes=1)).isoformat()}
 receipt={'status':'EXECUTED','Kind':'Unity','ExitCode':0,'engineStarted':True,'policyChanged':False,'invocation':invocation,'finishedUtc':(now+timedelta(minutes=1)).isoformat(),'before':{'PolicyChanged':False,'Profiles':['Public']},'after':{'PolicyChanged':False,'Profiles':['Public']},'unityIpcAudits':[{'policyChanged':False,'rows':[{'transport':'Named IPC pipe','tcpListenerCount':0}]}]}
 check('valid scoped Unity receipt mechanism',lambda:validate_receipt(receipt,'Unity',log,capture,root))
 for name,mutate in [
  ('old unbound receipt',lambda r:r.pop('invocation')),
  ('failed execution',lambda r:r.update(status='FAIL')),
  ('boolean exit status',lambda r:r.update(ExitCode=False)),
  ('policy changed',lambda r:r.update(policyChanged=True)),
  ('process did not start',lambda r:r.update(engineStarted=False)),
  ('wrong executable',lambda r:r['invocation'].update(executable=UNREAL)),
  ('wrong log',lambda r:r['invocation']['arguments'].__setitem__(-1,'unrelated.log')),
  ('stale capture',lambda r:r['invocation'].update(startedUtc=(now+timedelta(seconds=30)).isoformat())),
  ('reversed execution interval',lambda r:r.update(finishedUtc=(now-timedelta(minutes=2)).isoformat())),
  ('missing timezone',lambda r:r['invocation'].update(startedUtc='2026-10-23T00:00:00')),
  ('interactive Unity',lambda r:r['invocation']['arguments'].remove('-batchmode')),
  ('wrong probe',lambda r:r['invocation']['arguments'].__setitem__(r['invocation']['arguments'].index('EMObjectEqualityProbe.Run'),'Other.Run')),
  ('no IPC observation',lambda r:r.update(unityIpcAudits=[])),
  ('TCP package manager listener',lambda r:r['unityIpcAudits'][0]['rows'][0].update(tcpListenerCount=1)),
  ('boolean TCP listener count',lambda r:r['unityIpcAudits'][0]['rows'][0].update(tcpListenerCount=False)),
  ('changed active profile',lambda r:r['after'].update(Profiles=['Private'])),
  ('missing active profile',lambda r:r['before'].update(Profiles=[])),
  ('original Unity project',lambda r:r['invocation']['arguments'].__setitem__(r['invocation']['arguments'].index('-projectPath')+1,'C:/Projects/EngineMigratorUnity')),
 ]:
  changed=copy.deepcopy(receipt);mutate(changed)
  check(name,lambda r=changed:validate_receipt(r,'Unity',log,capture,root),True)
 for kind,exe,flags in [('Unreal',UNREAL,['-notraceserver','-NoZenAutoLaunch','-ddc=InstalledNoZenLocalFallback','-UDPMESSAGING_TRANSPORT_ENABLE=0']),('Compiler',DOTNET,['-NoUBA','-UBADisableRemote','-NoXGE','-NoFASTBuild','-NoSNDBS','-MaxParallelActions=1'])]:
  native=copy.deepcopy(receipt);native.update(Kind=kind);native['invocation'].update(executable=exe,arguments=['-Project='+str(folder/'RuntimeWorld.uproject'),'-abslog='+str(log)]+flags)
  check('valid '+kind+' receipt mechanism',lambda r=native,k=kind:validate_receipt(r,k,log,capture,root))
  for flag in flags:
   changed=copy.deepcopy(native);changed['invocation']['arguments'].remove(flag)
   check(kind+' missing '+flag,lambda r=changed,k=kind:validate_receipt(r,k,log,capture,root),True)
 check('outside private evidence',lambda:owned(root,root/'../EngineMigratorUnity'),True)
 check('missing actual project evidence',lambda:verify(root,folder),True)
 (folder/'project-integration-verification.json').write_text(json.dumps({'status':'PASS','sceneTravelVerified':True,'creationDestructionVerified':True}))
 check('bare success flags without actual world captures',lambda:verify(root,folder),True)
 optimized=subprocess.run([sys.executable,'-O','-c','from object_equality_evidence import verify;verify('+repr(str(root))+','+repr(str(folder))+')'],cwd=Path(__file__).resolve().parent,text=True,capture_output=True)
 if optimized.returncode==0 or 'Python optimization disables'not in optimized.stderr:raise RuntimeError('Optimized comparator execution was not rejected')
 passed.append('Python optimization cannot disable project comparator assertions')

check('complete oracle rows',lambda:require_rows(expected_rows(),'valid'))
for name,mutate in [('boolean value',lambda r:r[0].__setitem__(2,True)),('float value',lambda r:r[0].__setitem__(2,1.0)),('missing row',lambda r:r.pop()),('missing column',lambda r:r[0].pop()),('duplicate case',lambda r:r.__setitem__(1,r[0])),('wrong identity after GC',lambda r:r[13].__setitem__(2,1))]:
 rows=expected_rows();mutate(rows);check(name,lambda r=rows:require_rows(r,name),True)
check('complete identity flow rows',lambda:require_flows(expected_flows(),'valid'))
for name,mutate in [('boolean flow',lambda r:r[0].__setitem__(2,True)),('float flow',lambda r:r[0].__setitem__(2,1.0)),('missing flow case',lambda r:r.pop()),('missing flow value',lambda r:r[0].pop()),('wrong conditional identity',lambda r:r[13].__setitem__(4,1)),('wrong explicit-null identity',lambda r:r[13].__setitem__(9,0))]:
 rows=expected_flows();mutate(rows);check(name,lambda r=rows:require_flows(r,name),True)
result={'status':'VERIFIER_MECHANISM_TESTS_PASS','checks':len(passed),'syntheticReceiptsOnly':True,'actualEngineExecuted':False,'passed':passed}
(home/'verifier-selftest.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
