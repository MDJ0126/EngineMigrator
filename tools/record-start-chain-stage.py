"""Publish documentation only after real startup, regression and project gates pass."""
from pathlib import Path
import json,subprocess,sys,datetime,re,hashlib
r=Path(__file__).resolve().parent.parent;a=r/'artifacts';h=a/'start-chain-check'
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def require(v,s):
 if not v:raise RuntimeError(s)
for name in ['verify-start-chain-probe.py','verify-start-chain-regressions.py','verify-start-chain-world49.py']:
 subprocess.run([sys.executable,str(r/'tools'/name)],cwd=r,check=True,stdout=subprocess.DEVNULL)
require('368 checks passed.'in(h/'managed-checks-final.log').read_text(encoding='utf-8-sig'),'Current managed checks missing')
require(load(h/'world49/legacy-regression/verification.json')['status']=='PASS','Actual legacy 250-frame gate missing')
compat=load(h/'legacy-reverse-compatibility.json')
require(compat['status']=='PUBLISHED_NATIVE_49_REVERSE_COMPATIBILITY_PASS'and compat['classes']==49,'Published native reverse compatibility missing')
for p,d in compat['oldHeaders'].items():require(hashlib.sha256(Path(p).read_bytes()).hexdigest()==d.lower(),'Published legacy header changed '+p)
original=load(a/'user-project-baseline/original-hashes.json')
for p,d in original.items():require(hashlib.sha256(Path(p).read_bytes()).hexdigest()==d.lower(),'Original project modified '+p)
stamp=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=9))).isoformat();release='EngineMigrator-start-chain-preview-20261024'
section=f'''<section id="start-chain-actual"><h2>LateUpdate 없는 스크립트의 중첩 생성·시작 대기열</h2><p>{stamp}. Start·OnEnable에서 자식과 손자를 생성하는 다섯 클래스 묶음을 Unity → 네이티브 Actor → 복원 Unity로 실행했습니다. 100/30 FPS 각각 24프레임·65개 상태 값을 정확히 대조했습니다. 첫 고정 업데이트 횟수에 따른 시작 단계, Update 뒤 시작된 부모가 생성한 자식의 프레임 끝 시작, 재활성화와 부모 삭제 후 상태를 확인했습니다.</p><p>이벤트 순서 검사는 클래스 간 Update 순서와 개별 Start·활성 변경을 구분합니다. 동일 클래스 인스턴스들의 Update 순서에는 의존하지 않으며, 객체별 호출 횟수와 참조·상태는 별도로 정확히 대조합니다. 이는 <a href="https://docs.unity3d.com/6000.0/Documentation/Manual/script-execution-order.html">Unity 6.0이 같은 실행 순서 값 사이의 순서를 보장하지 않는 계약</a>에 따른 것입니다. 같은 클래스 객체 사이의 미정의 호출 순서에 의존하는 게임 로직까지 동일한 실행 순서를 보장하지 않습니다. 과거 전체 인스턴스 순서 체크섬의 차이와 진단 로그는 테스트 결과에 보존했습니다.</p><p>고정 업데이트·시간 배율·프리팹·후기 이벤트·장면 전환의 기존 사례 18개를 새 모듈로 실행해 회귀 검사했습니다. 추가로 LateUpdate 없는 49개 스크립트와 20장면·25프리팹이 포함된 별도 프로젝트 복사본을 빌드하고 원본/Unreal/복원 Unity의 생성·삭제·참조·GC·세 장면 전환 및 기존 250프레임 상태·UI·애니메이션·물리 허용 범위를 대조했습니다. 내부 검사 368개가 통과했으며, 이전 시작 방식의 헤더 역변환도 유지됩니다.</p><p><strong>제한:</strong> 이 검증은 위 사례의 지원 동작에 해당합니다. 임의 API·스켈레탈 애니메이션·복잡한 UI·모든 엔진 버전·전체 렌더링·물리 궤적 동등성 및 모든 게임의 완전 자동 포팅은 미완료입니다. 관측 전용 LateUpdate는 테스트 도구이며 변환된 다섯 클래스에는 LateUpdate가 없습니다. 원본 Unity의 LateUpdate와 Unreal의 actor 업데이트 직후를 같은 관측 경계로 사용하며, 그 뒤의 시작 처리는 다음 관측 상태와 호출 순서에 반영됩니다.</p></section>'''
for p in [r/'docs/guide.html',a/'development-report.html']:
 s=p.read_text(encoding='utf-8-sig');s=re.sub(r'<section id="start-chain-actual">.*?</section>','',s,flags=re.S);require('</main>'in s,'Main element missing');p.write_text(s.replace('</main>',section+'</main>'),encoding='utf-8')
p=a/'development-report.html';s=p.read_text(encoding='utf-8-sig');s=re.sub(r'<section id="current-preview">.*?</section>','',s,flags=re.S);banner=f'<section id="current-preview"><h2>최신 테스트용 배포본 — 시작 대기열 보강</h2><p>중첩 생성·프레임 끝 Start·고정 시간에 따른 시작 순서와 49개 스크립트 프로젝트 회귀 검증을 반영했습니다. 전체 포팅 개발은 계속 진행 중입니다.</p><p><a href="{release}/EngineMigrator.App.exe">프로그램 실행</a> · <a href="{release}.zip">배포 ZIP</a> · <a href="{release}/guide.html">가이드</a></p></section>';p.write_text(s.replace('<main>','<main>'+banner,1),encoding='utf-8')
p=r/'README.md';s=p.read_text(encoding='utf-8-sig');note='\nLateUpdate 없는 묶음의 Start/OnEnable 중첩 생성은 100/30 FPS 실제 왕복과 별도 49개 스크립트 프로젝트로 검증했습니다. 프레임 끝 시작과 고정 시간별 시작 단계를 보강했습니다. 동일 클래스 인스턴스의 미정의 Update 순서는 보장하지 않습니다.\n';p.write_text(s+(note if note not in s else''),encoding='utf-8')
proof={'status':'START_CHAIN_VERIFIED_AWAITING_RELEASE','goalComplete':False,'phaseStartedKst':'2026-10-24T08:21:34+09:00','initialPhaseStartedKst':'2026-10-24T08:09:11+09:00','proposedMinutes':[45,90],'recordedKst':stamp,'internalChecks':368,'startup':load(h/'verification.json'),'regressions':load(h/'regression-verification.json'),'project49':load(h/'world49/project-verification.json'),'legacy250':load(h/'world49/legacy-regression/verification.json'),'originalFilesVerified':len(original),'release':str(a/release),'nextUnverified':['FixedUpdate 콜백 내부의 중첩 생성과 고정 단계 교차','추가적인 고정 시간·초기 시간 배율에서의 깊은 시작 연쇄','높은 순서에서 낮은 순서 대상으로의 교차 활성 변경','임의 사용자 게임·추가 엔진 버전·전체 렌더링/물리 궤적 동등성']}
proof['sourceHashes']={p:hashlib.sha256((r/p).read_bytes()).hexdigest()for p in ['src/EngineMigrator.Core/ActorScripts.cs','src/EngineMigrator.Core/StartQueuePorting.cs','tests/EngineMigrator.Checks/ActorChecks.cs','tests/EngineMigrator.Checks/Fixtures/start-chain.cs.txt']}
(h/'checkpoint.json').write_text(json.dumps(proof,indent=2));(r/'tools/verify-start-chain-release.py').write_text((r/'tools/verify-integrated-project-release.py').read_text(encoding='utf-8-sig').replace('integrated-project','start-chain'),encoding='utf-8');print('START_CHAIN_REPORT_GATES_PASS')


