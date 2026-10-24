"""Record the verified stage, leaving the full project-porting objective incomplete."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import hashlib,json,re,collections

root=Path(__file__).resolve().parent.parent;home=root/'artifacts/object-equality-check';started=datetime.fromisoformat('2026-10-23T06:32:17+00:00')
def load(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
actual=load(home/'actual-roundtrip-verification.json');archive=load(root/'artifacts/gc-reference-archive-verification.json');public=load(home/'public-pipeline-verification.json');mechanism=load(home/'verifier-selftest.json')
if actual['status']!='ACTUAL_GC_REFERENCE_ROUNDTRIP_PASS'or archive['status']!='PASS'or public['headers']!=49 or mechanism['checks']!=49:raise RuntimeError('Stage evidence incomplete')
if '286 checks passed.'not in(home/'release-checks.log').read_text(encoding='utf-8-sig'):raise RuntimeError('Final .NET checks incomplete')
for folder in ('legacy-regression-final','legacy-restored-comparison-final'):
 d=load(home/folder/'verification.json')
 if d['status']!='PASS'or d['frames']!=250:raise RuntimeError('Final 250-frame regression incomplete')
baseline=load(root/'artifacts/user-project-baseline/original-hashes.json')
if len(baseline)!=60 or any(sha(p).upper()!=h.upper()for p,h in baseline.items()):raise RuntimeError('Original project changed')
release=root/'artifacts/EngineMigrator-gc-reference-preview-20261023'
if not(release/'EngineMigrator.App.exe').is_file()or list(release.glob('*.pending*')):raise RuntimeError('Release is still pending')
if sha(release/'EngineMigrator.Core.dll')!=sha(root/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'):raise RuntimeError('Release differs from tested assembly')
core=Path(load(root/'artifacts/core-engine-launch-tests/paths.json')['run']);receipts=[]
for folder in(core,root/'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da',root/'artifacts/runtime-ui-api-tests'):
 for path in folder.glob('engine-preflight-*.json'):
  d=load(path);stamp=d.get('invocation',{}).get('startedUtc')
  if not stamp or datetime.fromisoformat(stamp)<started:continue
  if d.get('policyChanged')is not False:raise RuntimeError('Unexpected firewall mutation')
  receipts.append({'path':str(path),'kind':d['Kind'],'status':d['status'],'exitCode':d.get('ExitCode'),'policyChanged':False,'ipcSamples':len(d.get('unityIpcAudits',[]))})
(home/'resumed-engine-receipts.json').write_text(json.dumps(receipts,indent=2))
now=datetime.now(timezone.utc);seconds=(now-started).total_seconds()
record={'status':'GC_REFERENCE_STAGE_PASSED','startedUtc':started.isoformat(),'finishedUtc':now.isoformat(),'finishedKst':now.astimezone(timezone(timedelta(hours=9))).isoformat(),'elapsedSeconds':seconds,'initialEstimateMinutes':[30,60],'revisedAdditionalEstimateMinutes':[10,20],'initialEstimateExceeded':seconds>3600,'delayCause':'Additional actual reflected-argument identity loss found and fixed; final engine/regression/release gates rerun.','dotnetChecksPassed':286,'verifierMechanismChecks':49,'actualIdentityCombinations':20,'actualNativeSynchronousGCCycles':20,'reflectedArgumentsAndReturnsVerified':True,'csharpParameterValueCopyVerified':True,'projectHeadersGeneratedAndRestored':49,'unchangedPriorScriptTokenSequences':48,'worldFrames':actual['worldFrameCounts'],'worldCheckpointCount':8,'sceneSequence':['RuntimeSpawner','RuntimeMiddle','RuntimeFinal'],'freshUnrealLegacyFrames':250,'comparedAgainstSavedActualUnityOriginalAndRestored250Frames':True,'originalFilesUnchanged':60,'archiveFilesVerified':archive['filesCompared'],'releasePath':str(release),'releaseCoreSha256':sha(release/'EngineMigrator.Core.dll'),'releaseArchiveSha256':sha(release.with_suffix('.zip')),'guiLaunched':False,'computerUse':False,'globalFirewallPolicyChanged':False,'additionalHumanApprovalQuestions':0,'priorExecutionBlockerResolvedByExplicitUserApproval':True,'releaseUpdated':True,'goalComplete':False,'remaining':['Collections/arrays and broader scripting/Blueprint graph coverage','Blueprint call pins with weak GameObject parameters/results','Component-specific identity beyond GameObject references','Rendered/input/physics trajectory equivalence','Scene error/async/additive/persistence semantics','Broader real projects, dependencies and engine-version coverage']}
(home/'checkpoint.json').write_text(json.dumps(record,indent=2));(home/'approval-resolution.json').write_text(json.dumps({'priorAudit':'blocked-audit.json','resolved':True,'basis':'Explicit user approval to continue audited private Unity/Unreal execution','freshActualEngineReceipts':len(receipts),'newRepeatedApprovalQuestions':0,'goalComplete':False},indent=2))
print(json.dumps(record))
