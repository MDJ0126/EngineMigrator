"""Preserve verified UI progress without declaring the unexecuted clone fix complete."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import hashlib,json,re
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/ui-equality-check'
def load(path):return json.loads(path.read_text(encoding='utf-8-sig'))
identity=load(home/'engine-verification.json');api=load(home/'ui-api-verification.json');project=load(home/'project-verification.json')
if identity.get('status')!='UI_ENGINE_ROUNDTRIP_PASSED'or api.get('status')!='ACTUAL_UI_API_ROUNDTRIP_PASSED'or project.get('status')!='ACTUAL_UI_REFERENCE_PROJECT_ROUNDTRIP_PASSED':raise RuntimeError('Prior actual UI candidate evidence missing')
if '302 checks passed.'not in(home/'release-checks.log').read_text(encoding='utf-8-sig'):raise RuntimeError('Current internal checks missing')
original=load(home/'destruction-clone-before-fix-original.json');native=load(home/'destruction-clone-before-fix-native.json')
differences=[{'frame':a[0],'columns':[i for i,(v,w)in enumerate(zip(a,b))if v!=w]}for a,b in zip(original['rows'],native['rows'])if a!=b]
if differences!=[{'frame':6,'columns':[11,12]},{'frame':7,'columns':[11,12]}]:raise RuntimeError('Actual deletion/clone difference changed')
baseline=load(root/'artifacts/user-project-baseline/original-hashes.json')
for name,digest in baseline.items():
 if hashlib.sha256(Path(name).read_bytes()).hexdigest().upper()!=digest.upper():raise RuntimeError('User original changed: '+name)
now=datetime.now(timezone(timedelta(hours=9)));stamp=now.strftime('%Y-%m-%d %H:%M:%S KST')
sources=[root/'src/EngineMigrator.Core/ActorScripts.cs',root/'src/EngineMigrator.Core/WeakUiPorting.cs',root/'src/EngineMigrator.Core/ActorValidationSurface.cs',root/'tests/EngineMigrator.Checks/Fixtures/ui-destruction.cs.txt',home/'AuditUIDestruction.h']
record={'status':'UI_IMPLEMENTATION_PENDING_CLONE_ENGINE_REGRESSION','goalComplete':False,'phaseStartedKst':'2026-10-23T20:55:30+09:00','recordedKst':now.isoformat(),'internalChecks':302,'beforeCloneFixActualIdentityCases':30,'beforeCloneFixActualUIApiCheckpoints':20,'beforeCloneFixProjectHeaders':49,'baseRemovalFramesPassed':7,'actualCloneDifferenceBeforeFix':differences,'cloneFixNativeBuildExecuted':False,'cloneFixActualRoundtripPassed':False,'releaseUpdated':False,'priorTransformReleasePreserved':True,'originalFilesUnchanged':len(baseline),'blocker':'The existing safety runner cannot acquire the exclusive engine lease. Some separate probes succeeded, but actual build attempts remain blocked. Owner/cause unconfirmed; lock not deleted or bypassed.','sourceHashes':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest()for p in sources},'nextActions':['Build current private Core fixture through destruction-runner; successful build must write destruction-build-hashes.json','Capture original/native/restored destruction and cloning with 17-column raw null diagnostics','Verify and fix any actual remaining difference','Regenerate project headers and complete final engine regressions before publishing'],'xerj':'Unavailable; rg fallback'}
(home/'checkpoint.json').write_text(json.dumps(record,indent=2))
(home/'clone-reference-difference.json').write_text(json.dumps({'status':'ACTUAL_DIFFERENCE_REPRODUCED','differences':differences,'originalSha256':hashlib.sha256((home/'destruction-clone-before-fix-original.json').read_bytes()).hexdigest(),'nativeSha256':hashlib.sha256((home/'destruction-clone-before-fix-native.json').read_bytes()).hexdigest(),'fixEngineVerified':False},indent=2))
section=f'''<section id="ui-reference-pending"><h2>UI 참조·삭제 확장 — 복제 조합 최종 검증 중</h2><p>{stamp} 기준. Canvas와 Button의 참조를 소유 Actor의 약한 식별 정보와 UI 종류로 구분하도록 구현했습니다. 수정 전 30개 조합 중 28개에서 실제 Unity와 Unreal 결과가 달랐고, 종류·존재 여부 검사를 적용한 뒤 30개 조합의 GC 전후 왕복 대조가 통과했습니다. Canvas·Button 기본 지연 삭제 7프레임, UI 프리팹·상태·외부 콜백 20개 체크포인트, 49개 스크립트의 프리팹·GC·세 장면 전환 대조도 복제 추가 수정 전 후보에서 통과했습니다.</p><p><strong>추가로 재현한 차이:</strong> UI 삭제 후 객체를 복제하면 6·7프레임의 삭제 참조 비교 2개 값이 Unity와 Unreal에서 달랐습니다. 복제본의 삭제 UI 참조 정리와 삭제 대기 표식 초기화 코드를 추가했고 현재 내부 검사 302개는 통과했습니다. 그러나 이 추가 수정의 Unreal 빌드와 실제 왕복 대조는 아직 완료하지 못했습니다. 원본 Unity의 관리 참조 null 여부와 Unreal의 명시적 약한 null 상태도 함께 대조하도록 검증을 보강했습니다.</p><p>공용 실행 잠금을 확보하지 못해 추가 빌드가 엔진 시작 전에 차단되고 있습니다. 일부 별도 잠금 확인은 성공했으나 실제 빌드에서는 차단이 반복돼 점유자와 원인은 미확인입니다. 잠금 삭제·우회·보안 정책 변경은 하지 않았습니다. 디스크 안전 중단은 자체 생성한 오래된 PCH 캐시 두 개만 정리해 여유를 확보한 후 기존 검증을 재실행했습니다. 새 UI 배포본은 발행하지 않았고 마지막 검증 완료 Transform 배포본을 유지합니다.</p><p>사용자 원본 {len(baseline)}개 파일은 해시가 일치합니다. 전체 UI API·동적 UI 추가·모든 엔진 버전·렌더링 동등성·전체 프로젝트 포팅 완료를 뜻하지 않습니다.</p></section>'''
for path in(root/'docs/guide.html',root/'artifacts/development-report.html'):
 text=path.read_text(encoding='utf-8-sig');text=re.sub(r'<section id="ui-reference-pending">.*?</section>','',text,flags=re.S)
 if '</main>'not in text:raise RuntimeError('HTML anchor missing')
 path.write_text(text.replace('</main>',section+'</main>'),encoding='utf-8')
print(json.dumps({'status':record['status'],'internalChecks':302,'recordedKst':now.isoformat(),'releaseUpdated':False}))
