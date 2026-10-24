"""Record honest partial progress without publishing an unverified build."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib
import json

root=Path(__file__).resolve().parent.parent;home=root/'artifacts/object-equality-check'
model=json.loads((home/'model-verification.json').read_text())
log=(home/'release-checks.log').read_text(encoding='utf-8-sig')
if '279 checks passed.' not in log:raise RuntimeError('Release checks incomplete')
now=datetime.now(timezone.utc);start=datetime.fromisoformat('2026-10-23T06:03:35+00:00')
files=['src/EngineMigrator.Core/ActorScripts.cs','tests/EngineMigrator.Checks/ActorChecks.cs','tests/EngineMigrator.Checks/Fixtures/object-equality.cs.txt']
checkpoint={'status':'IMPLEMENTED_ENGINE_VALIDATION_PENDING','previousGoalTurn':'progress','startedUtc':start.isoformat(),'recordedUtc':now.isoformat(),'elapsedSeconds':(now-start).total_seconds(),'goalComplete':False,'checksPassed':279,'model':model,'engineLaunchesThisStage':0,'automaticApprovalRejections':2,'rejectionReason':'Actual Unity execution rejected despite existing scoped runner, read-only firewall evidence and prior IPC receipts; no bypass attempted.','releaseUpdated':False,'existingRelease':'artifacts/EngineMigrator-object-truth-preview-20261023','pending':['Fresh actual Unity source capture','Actual UE build and equality execution','Restored Unity execution','Post-GC identity retention in reflected native references','Integration into runtime creation/destruction/scene-travel fixture','Release update only after engine gates pass'],'sources':{p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in files}}
(home/'checkpoint.json').write_text(json.dumps(checkpoint,indent=2))
section='''<section id="object-equality-pending"><h2>GameObject 동등성 비교 — 개발 중</h2><p>소스 변환에 GameObject == / != 처리를 추가했습니다. null·같은 객체·다른 객체·삭제된 객체·비활성 객체의 20개 조합을 관리 코드 원본/복원 및 MSVC /Od·/O2 모형과 독립 기준으로 대조했습니다. .NET 검사 279개가 통과했습니다.</p><p><strong>실제 엔진 검증 미완료:</strong> 이번 Unity 실행은 자동 승인 검토에서 두 번 거절됐으며 엔진을 실행하거나 우회하지 않았습니다. 실제 Unity→Unreal→Unity 실행, GC 이후 객체 식별 유지 및 프로젝트 런타임 통합 검증이 남아 있습니다. 기존 배포본은 변경하지 않았습니다.</p><p>최종 엔진 검증 배포본: EngineMigrator-object-truth-preview-20261023. 개발 소스의 검사 수와 배포본 검증 수를 구분해 주십시오.</p><p><a href="https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Runtime/Export/Scripting/UnityEngineObject.bindings.cs">Unity 공식 6000.0 참조 소스</a>를 확인했습니다. 정확한 6000.0.83f1 실행 검증은 대기 중입니다.</p></section>'''
for relative in ['artifacts/development-report.html','docs/guide.html']:
 path=root/relative;text=path.read_text(encoding='utf-8-sig')
 if 'id="object-equality-pending"' not in text:
  anchor='</main>' if '</main>' in text else '</body>'
  if anchor not in text:raise RuntimeError('HTML content closure missing: '+relative)
  path.write_text(text.replace(anchor,section+anchor),encoding='utf-8')
print(json.dumps(checkpoint))
