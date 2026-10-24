"""Save the unfinished-stage result, preserving the last engine-tested release."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re
from html.parser import HTMLParser

root=Path(__file__).resolve().parent.parent;home=root/'artifacts/object-equality-check'
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
checks=(home/'release-checks.log').read_text(encoding='utf-8-sig')
if not re.search(r'^281 checks passed\.',checks,re.M):raise RuntimeError('Current .NET regression run incomplete')
model=json.loads((home/'model-verification.json').read_text());gc=json.loads((home/'gc-model-verification.json').read_text());verifier=json.loads((home/'verifier-selftest.json').read_text());reverse=json.loads((home/'full-reverse-verification.json').read_text())
if gc['status']!='IDENTITY_LOSS_REPRODUCED_IN_SOURCE_BACKED_MODEL'or verifier['checks']!=42 or reverse['scripts']!=49:raise RuntimeError('Required source-backed evidence incomplete')
release=root/'artifacts/EngineMigrator-object-truth-preview-20261023';archive=json.loads((root/'artifacts/object-truth-archive-verification.json').read_text())
original_release=Path(archive['unicodeExtraction'])
for name in ('EngineMigrator.App.exe','EngineMigrator.Core.dll'):
 if sha(release/name)!=sha(original_release/name):raise RuntimeError('Previously verified release changed: '+name)
baseline=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text())
if not all(sha(p).upper()==h.upper()for p,h in baseline.items()):raise RuntimeError('User original changed')
started=datetime.fromisoformat('2026-10-23T06:12:27+00:00');now=datetime.now(timezone.utc)
sources=['src/EngineMigrator.Core/ActorScripts.cs','src/EngineMigrator.Core/ResourcePorting.cs','tests/EngineMigrator.Checks/ActorChecks.cs','tools/object_equality_evidence.py','tools/object_equality_oracle.py']
record={'status':'CANDIDATE_GC_IDENTITY_GAP_PRODUCTION_BLOCKED','startedUtc':started.isoformat(),'recordedUtc':now.isoformat(),'elapsedSeconds':(now-started).total_seconds(),'previousGoalTurn':'progress','goalComplete':False,'actualEngineLaunchesThisTurn':0,'engineRetryAfterPreviousDenials':False,'dotnetChecksPassed':281,'verifierMechanismChecks':42,'verifierReceiptsSyntheticOnly':True,'fullHeaderReverseRegression':49,'fullReversePriorSourceHashesEqual':True,'gcModelStatus':gc['status'],'gcModelProfiles':['Od','O2'],'actualEngineGCFailureVerified':False,'productionForwardAndReverseCandidateBlocked':True,'internalCandidateForValidationOnly':True,'originalFilesUnchanged':len(baseline),'releaseUpdated':False,'existingReleasePreserved':str(release),'sourceHashes':{p:sha(root/p)for p in sources},'nextRequired':['Replace pointer-only identity representation with identity surviving reference elimination','Actual Unity/UE source/restored execution with post-GC comparison','Current project runtime creation/destruction and scene transition integration','Release update only after all engine and regression gates pass'],'remainingExecutionRestriction':'Prior automatic approval review rejected actual Unity execution twice. No bypass or new engine launch attempted.'}
(home/'gc-audit-checkpoint.json').write_text(json.dumps(record,indent=2))
section='''<section id="object-equality-gc-audit"><h2>객체 비교 후속 점검 — GC 식별 보존 미완료</h2><p>UE 5.8 설치 소스의 참조 제거 동작과 현재 비교 도우미를 결합한 C++ 모형에서 차이를 재현했습니다. 서로 다른 삭제 객체의 참조가 모두 null로 제거되면 같은 객체로 오인합니다. /Od·/O2에서 같은 결과가 나왔습니다. <strong>실제 UE GC 실행 결과를 확인한 것은 아닙니다.</strong></p><p>잘못된 변환을 방지하기 위해 GameObject 간 동등성 비교 후보는 일반 정방향·역방향 변환에서 차단했습니다. 내부 검증용 후보와 재현 코드는 유지했습니다. GC 이후에도 유지되는 객체 식별 표현과 실제 엔진 검증이 필요합니다.</p><p>현재 .NET 검사 281개, 검증기 변조·누락 검사 40개, 기존 49개 헤더의 역변환 및 이전 복원 소스와 해시 대조가 통과했습니다. 검증기 검사는 합성 영수증을 사용한 엔진 없는 테스트입니다. 실행 영수증에는 실행 경로·인수·시작/종료 시간을 기록하도록 변경했습니다. 실제 엔진에서 이 변경을 재검증한 것은 아닙니다.</p><p>사용자 원본 60개는 변경되지 않았습니다. 기존 엔진 검증 배포본의 실행 파일과 Core DLL은 보존됐으며 새 배포는 하지 않았습니다. 전체 포팅 목표는 미완료입니다.</p></section>'''
section=section.replace('검사 40개','검사 '+str(verifier['checks'])+'개')
for relative in ['artifacts/development-report.html','docs/guide.html']:
 path=root/relative;text=path.read_text(encoding='utf-8-sig')
 text=text.replace('소스 변환에 GameObject == / != 처리를 추가했습니다.','내부 검증용 후보로 GameObject == / != 처리를 추가했습니다. 후속 GC 식별 손실 모형 재현에 따라 일반 변환 경로에서는 차단했습니다.')
 if 'id="object-equality-gc-audit"'in text:
  text=re.sub(r'<section id="object-equality-gc-audit">.*?</section>',lambda _:section,text,flags=re.S)
 else:
  if '</main>'not in text:raise RuntimeError('HTML content anchor missing')
  text=text.replace('</main>',section+'</main>')
 parser=HTMLParser();parser.feed(text);parser.close()
 if text.count('id="object-equality-gc-audit"')!=1:raise RuntimeError('Duplicate audit section')
 path.write_text(text,encoding='utf-8')
print(json.dumps(record))
