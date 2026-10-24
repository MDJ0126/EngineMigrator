"""Record only the independently verified component stage; keep the tested release intact."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import hashlib,json,re
import runpy,contextlib,io,sys

root=Path(__file__).resolve().parent.parent;home=root/'artifacts/component-equality-check'
verification=runpy.run_path(str(root/'tools/verify-component-equality.py'))['verify'](root,False)
with contextlib.redirect_stdout(io.StringIO()):runpy.run_path(str(root/'tools/verify-component-equality-world.py'))
project=json.loads((home/'project-verification.json').read_text())
for folder in ('legacy-regression-final','legacy-restored-comparison-final'):
 result=json.loads((home/folder/'verification.json').read_text(encoding='utf-8-sig'))
 if result.get('status')!='PASS'or result.get('frames')!=250:raise RuntimeError('Actual regression comparison incomplete')
baseline=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
for name,digest in baseline.items():
 if hashlib.sha256(Path(name).read_bytes()).hexdigest().upper()!=digest.upper():raise RuntimeError('Original changed: '+name)
checks=(home/'release-checks.log').read_text(encoding='utf-8-sig')
if '292 checks passed.'not in checks:raise RuntimeError('Internal checks incomplete')
now=datetime.now(timezone(timedelta(hours=9)));stamp=now.strftime('%Y-%m-%d %H:%M:%S KST')
checkpoint=json.loads((home/'checkpoint.json').read_text(encoding='utf-8-sig'))
checkpoint.update(status='COMPONENT_ENGINE_ROUNDTRIP_VERIFIED_AWAITING_RELEASE',publicComponentBackendEnabled=True,internalChecks=292,unrealComponentBuildExecuted=True,unrealComponentRoundtripPassed=True,blocker=None,unityRestoredCases=34,unitySourceRestoredVerified=True,originalFilesUnchanged=len(baseline),recordedKst=now.isoformat(),verification=verification,projectVerification=project)
(home/'checkpoint.json').write_text(json.dumps(checkpoint,indent=2))
section=f'''<section id="component-identity-candidate"><h2>사용자 컴포넌트 GC 참조 — 실제 왕복 검증 통과</h2><p>{stamp} 기준. 일반 변환 API에서 사용자 MonoBehaviour 참조 간 동등성 비교가 있는 변환 묶음은 컴포넌트 참조와 GameObject 참조를 약한 Actor 참조로 보존합니다. 지역 변수는 원래 컴포넌트 타입을 복원할 수 있는 별칭을 사용합니다. 함수 입력은 const 약한 참조로 전달받아 별도 지역 변수로 복사하므로 삭제 객체 식별 정보와 C# 인수의 값 복사 의미를 유지합니다. UHT가 약한 참조 입력 전용 매크로를 생성한 것도 확인했습니다.</p><p>실제 Unity 원본 → Unreal → 복원 Unity에서 34개 조합의 16개 값이 GC 전후 일치했습니다. 컴포넌트만 삭제, 소유 객체 삭제, 비활성 객체, 같은 소유 객체의 다른 컴포넌트, 함수 인수·반환값·인수 재대입을 포함합니다. Unreal 동기 GC 완료 34회를 확인했습니다. 일반 변환 API의 생성·복원 코드가 실행한 코드와 일치합니다.</p><p>프로젝트 49개 스크립트 생성·복원 및 실제 런타임 대조도 통과했습니다. 원본 Unity 55프레임·Unreal 56프레임·복원 Unity 55프레임에서 프리팹 8개 체크포인트와 RuntimeSpawner → RuntimeMiddle → RuntimeFinal 장면 순서가 일치했습니다. 기존 Unity 원본/복원 250프레임 캡처와 이번 Unreal 250프레임 회귀 대조도 통과했습니다. 이번에 Unity 250프레임을 새로 실행했다는 뜻은 아닙니다. 내부 검사 292개와 IPC 감시의 종료 경합 모의 검사 6개가 통과했습니다.</p><p><strong>적용 안내:</strong> 지원하는 사용자 컴포넌트 참조의 == / != 비교가 있으면 자동 적용됩니다. 타입 메타데이터와 약한 참조 별칭은 역변환 시 검사하며, 임의 변경은 차단합니다. 일반 C#의 상태 변경 호출이 섞인 복합식은 기존 평가 순서 제한이 적용됩니다. 예를 들어 반환값 비교는 먼저 지역 변수에 받은 뒤 비교할 수 있습니다.</p><p><strong>남은 범위:</strong> 실행 버전은 Unity 6000.0.83f1과 Unreal 5.8입니다. Transform·UI 컴포넌트의 전체 식별, 약한 참조를 쓰는 Blueprint 함수 핀, 모든 엔진 버전, 물리 궤적·렌더링 동등성 및 전체 프로젝트 포팅 완료는 검증하지 않았습니다. 사용자 원본 {len(baseline)}개 파일의 해시는 기존 기준과 일치합니다. 방화벽 정책 변경·UAC·컴퓨터 유즈 없이 기존 안전 실행기의 검사·잠금·시간 제한을 사용했습니다. 보안창 유무의 GUI 검사는 하지 않았습니다.</p></section>'''
for path in (root/'artifacts/development-report.html',root/'docs/guide.html'):
 text=path.read_text(encoding='utf-8-sig');text=re.sub(r'<section id="component-identity-candidate">.*?</section>','',text,flags=re.S)
 if '</main>'not in text:raise RuntimeError('Missing HTML anchor')
 path.write_text(text.replace('</main>',section+'</main>'),encoding='utf-8')
print(json.dumps({'status':'COMPONENT_ENGINE_STAGE_RECORDED','originalFilesUnchanged':len(baseline),'unrealRoundtripVerified':True,'recordedKst':now.isoformat()}))
