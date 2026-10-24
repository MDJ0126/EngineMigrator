"""Validate fresh, scoped execution evidence. Never starts an engine."""
from datetime import datetime,timezone
from pathlib import Path
import hashlib
import json
import importlib.util
from object_equality_oracle import require_rows,require_flows

UNITY=r'C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe'
UNREAL=r'C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe'
DOTNET=r'C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\ThirdParty\DotNet\10.0\win-x64\dotnet.exe'

def require(value,message):
 if not value:raise RuntimeError(message)

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def owned(root,path):
 root=Path(root).resolve();path=Path(path)
 if not path.is_absolute():path=root/path
 resolved=path.resolve()
 require(resolved.is_relative_to(root/'artifacts'),'Evidence outside private artifacts: '+str(path))
 require(not path.is_symlink(),'Linked evidence is unsupported: '+str(path))
 for parent in path.parents:
  if parent==root:break
  require(not parent.is_symlink(),'Linked evidence parent is unsupported')
 return resolved

def stamp(value):
 require(type(value)is str,'Missing execution timestamp')
 try:result=datetime.fromisoformat(value.replace('Z','+00:00'))
 except ValueError as error:raise RuntimeError('Invalid execution timestamp')from error
 require(result.tzinfo is not None,'Naive execution timestamp is unsupported')
 return result

def validate_receipt(receipt,kind,expected_log,capture=None,root=None,unity_method='EMObjectEqualityProbe.Run'):
 require(receipt.get('status')=='EXECUTED'and receipt.get('Kind')==kind,'Successful matching engine receipt required')
 require(type(receipt.get('ExitCode'))is int and receipt['ExitCode']==0,'Successful integer exit code required')
 require(receipt.get('engineStarted')is True and receipt.get('policyChanged')is False,'Audited process execution required')
 invocation=receipt.get('invocation',{})
 require(invocation.get('executable')=={'Unity':UNITY,'Unreal':UNREAL,'Compiler':DOTNET}[kind],'Unexpected executable')
 args=invocation.get('arguments');require(type(args)is list and all(type(a)is str for a in args),'Execution arguments missing')
 if root is not None:
  work=owned(root,invocation.get('workingDirectory',''))
  if kind=='Unity':
   require(args.count('-projectPath')==1 and args.index('-projectPath')+1<len(args),'Single private Unity project required')
   project=owned(root,args[args.index('-projectPath')+1])
  else:
   projects=[a.removeprefix('-Project=')for a in args if a.startswith('-Project=')or(not a.startswith('-')and a.endswith('.uproject'))]
   require(len(projects)==1,'Single private UE project required');project=owned(root,projects[0])
  require(project==work or project.is_relative_to(work),'Project outside owned execution directory')
 require(str(expected_log)in args or any(a in ('-abslog='+str(expected_log),'-log='+str(expected_log))for a in args),'Receipt belongs to another capture/log')
 start=stamp(invocation.get('startedUtc'));finish=stamp(receipt.get('finishedUtc'));require(finish>=start,'Execution timestamps reversed')
 if capture is not None:
  written=datetime.fromtimestamp(Path(capture).stat().st_mtime,timezone.utc)
  require(start<=written<=finish,'Capture is stale or outside process execution')
 before=receipt.get('before',{});after=receipt.get('after',{})
 require(before.get('PolicyChanged')is False and after.get('PolicyChanged')is False,'Read-only firewall receipts required')
 require(type(before.get('Profiles'))is list and bool(before['Profiles'])and before['Profiles']==after.get('Profiles'),'Active profile evidence missing or changed')
 if kind=='Unity':
  require('-batchmode'in args and '-nographics'in args and '-executeMethod'in args and args.index('-executeMethod')+1<len(args)and args[args.index('-executeMethod')+1]==unity_method,'Wrong Unity probe')
  audits=receipt.get('unityIpcAudits');require(type(audits)is list and bool(audits),'UPM IPC observation missing')
  for audit in audits:
   require(audit.get('policyChanged')is False,'IPC audit changed policy')
   for row in audit.get('rows',[]):require(row.get('transport')=='Named IPC pipe'and type(row.get('tcpListenerCount'))is int and row['tcpListenerCount']==0,'Unexpected UPM listener')
 elif kind=='Unreal':
  for flag in ('-notraceserver','-NoZenAutoLaunch','-ddc=InstalledNoZenLocalFallback','-UDPMESSAGING_TRANSPORT_ENABLE=0'):require(flag in args,'Unreal safety option missing: '+flag)
 else:
  for flag in ('-NoUBA','-UBADisableRemote','-NoXGE','-NoFASTBuild','-NoSNDBS','-MaxParallelActions=1'):require(flag in args,'Compiler isolation option missing: '+flag)

def verify(root,home):
 require(__debug__,'Python optimization disables project comparator assertions; use normal execution')
 root=Path(root).resolve();home=owned(root,home)
 require((home/'project-integration-verification.json').is_file(),'Project runtime scene integration evidence missing')
 record=json.loads((home/'project-integration-verification.json').read_text())
 require(record.get('status')=='PASS'and record.get('sceneTravelVerified')is True and record.get('creationDestructionVerified')is True,'Runtime project integration is incomplete')
 world_inputs=record.get('worldCaptures');require(type(world_inputs)is dict,'Actual project scene captures missing')
 module_path=Path(__file__).with_name('compare-runtime-prefab.py')
 spec=importlib.util.spec_from_file_location('object_equality_prefab',module_path);prefab=importlib.util.module_from_spec(spec);spec.loader.exec_module(prefab)
 summaries=[];world_receipts=set()
 for name,kind in [('original','Unity'),('native','Unreal'),('restored','Unity')]:
  binding=world_inputs.get(name,{});path=owned(root,binding.get('path',''))
  require(path.is_file()and binding.get('sha256')==sha(path),'Missing or changed project capture: '+name)
  summaries.append(prefab.summarize(path))
  receipt_path=owned(root,binding.get('receipt',''));require(receipt_path not in world_receipts,'Project captures reuse an execution receipt');world_receipts.add(receipt_path)
  require(binding.get('receiptSha256')==sha(receipt_path),'Project capture receipt changed')
  validate_receipt(json.loads(receipt_path.read_text()),kind,owned(root,binding.get('log','')),path,root,unity_method='EMRuntimeWorldProbe.Run')
 require(summaries[0]==summaries[1]==summaries[2],'Actual project lifecycle/reference/scene checkpoints differ')
 hashes=record.get('sourceHashes');require(type(hashes)is dict,'Engine-tested source hashes missing')
 required={'src/EngineMigrator.Core/ActorScripts.cs','src/EngineMigrator.Core/ResourcePorting.cs','src/EngineMigrator.Core/WeakGameObjectPorting.cs','tests/EngineMigrator.Checks/Fixtures/object-equality.cs.txt','artifacts/object-equality-check/AuditObjectEquality.h','artifacts/object-equality-check/restored.cs','artifacts/object-equality-check/native-probe.py'}
 require(required.issubset(hashes),'Required engine-tested source hashes missing')
 for relative,wanted in hashes.items():
  source=(root/relative).resolve();require(source.is_relative_to(root),'Source outside workspace')
  require(type(wanted)is str and sha(source)==wanted,'Engine-tested source changed: '+relative)
 bindings=record.get('captureReceipts');require(type(bindings)is dict,'Per-capture engine receipt bindings missing')
 used=set(world_receipts)
 for name,kind,log in [('unity-original','Unity','unity-original.log'),('native-execution','Unreal','native.log'),('unity-restored','Unity','unity-restored.log')]:
  path=home/(name+'.json');require(path.is_file(),'Actual engine capture missing: '+name)
  data=json.loads(path.read_text());require(data.get('status')in ('PASS','CAPTURED'),'Actual capture failed: '+name)
  require_rows(data.get('rows'),name);require_rows(data.get('afterGarbageCollectionRows'),name+' after GC')
  require_flows(data.get('identityFlows'),name)
  if kind=='Unreal':require(type(data.get('synchronousGCCompleted'))is list and len(data['synchronousGCCompleted'])==20 and all(v is True for v in data['synchronousGCCompleted']),'Actual synchronous GC completion evidence missing')
  binding=bindings.get(name,{});require(binding.get('captureSha256')==sha(path),'Capture changed since verification: '+name)
  receipt_path=owned(root,binding.get('receipt',''));require(receipt_path not in used,'One execution receipt reused for multiple captures');used.add(receipt_path)
  require(binding.get('receiptSha256')==sha(receipt_path),'Execution receipt changed')
  validate_receipt(json.loads(receipt_path.read_text()),kind,home/log,path,root)
 compiler=owned(root,record.get('compilerReceipt',''));validate_receipt(json.loads(compiler.read_text()),'Compiler',home/'native-build.log',root=root)
 world_compiler=owned(root,record.get('worldCompilerReceipt',''));validate_receipt(json.loads(world_compiler.read_text()),'Compiler',home/'world-build.log',root=root)
 baseline=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
 require(len(baseline)==60 and all(Path(p).is_file()and sha(p).upper()==h.upper()for p,h in baseline.items()),'User original baseline changed')
 return {'status':'ENGINE_EVIDENCE_PASS','cases':20,'actualCaptures':3,'goalComplete':False,'releaseReady':False,'note':'Release additionally requires original-project preservation and full regression checks.'}
