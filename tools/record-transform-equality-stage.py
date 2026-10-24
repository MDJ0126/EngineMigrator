"""Record verified Transform scope and update existing guide/report without engine launches."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import contextlib,hashlib,io,json,re,runpy

root=Path(__file__).resolve().parent.parent;home=root/'artifacts/transform-equality-check'
standalone=runpy.run_path(str(root/'tools/verify-transform-equality.py'))['verify'](root,False)
with contextlib.redirect_stdout(io.StringIO()):runpy.run_path(str(root/'tools/verify-transform-equality-world.py'))
project=json.loads((home/'project-verification.json').read_text())
for folder in ('legacy-regression-final','legacy-restored-comparison-final'):
 result=json.loads((home/folder/'verification.json').read_text(encoding='utf-8-sig'))
 if result.get('status')!='PASS'or result.get('frames')!=250:raise RuntimeError('Regression comparison incomplete')
if '296 checks passed.'not in(home/'release-checks.log').read_text(encoding='utf-8-sig'):raise RuntimeError('Internal checks incomplete')
baseline=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
for name,digest in baseline.items():
 if hashlib.sha256(Path(name).read_bytes()).hexdigest().upper()!=digest.upper():raise RuntimeError('User original changed: '+name)
now=datetime.now(timezone(timedelta(hours=9)));stamp=now.strftime('%Y-%m-%d %H:%M:%S KST')
section=f'''<section id="transform-identity-actual"><h2>Transform GC 참조 — 실제 재현·수정·왕복 검증</h2><p>{stamp} 기준. 기존 Transform 변환은 GC 전에 삭제 객체와 null 비교 2개 조합에서, GC 후 서로 다른 삭제 객체 비교 1개 조합에서 Unity와 달랐습니다. 실제 수정 전 Unreal 캡처와 원본 Unity 캡처를 보존했습니다. 수정본은 Transform 참조 간 == / != 비교가 있는 변환 묶음에서 소유 Actor의 약한 참조 식별 정보를 사용합니다. Transform은 이번 검증에서 소유 GameObject와 함께 삭제됩니다. GameObject와 Transform의 서로 다른 객체 종류를 같은 것으로 취급하지 않으며, 두 종류 사이 직접 비교는 기존처럼 지원하지 않습니다.</p><table><thead><tr><th>Unity 코드</th><th>Unreal 대응</th><th>역변환</th></tr></thead><tbody><tr><td>Transform 필드·지역 변수</td><td>약한 Actor 참조, 지역 변수는 EM_TransformRef 별칭</td><td>Transform 타입 유지</td></tr><tr><td>Transform 함수 입력·반환</td><td>식별 정보를 유지하는 약한 참조와 입력의 지역 복사</td><td>원래 입력·반환 타입 유지</td></tr><tr><td>localPosition / SetParent / parent</td><td>실제 엔진 API 경계에서만 살아 있는 Actor 포인터를 사용</td><td>Unity 속성·SetParent 호출로 복원</td></tr></tbody></table><p>실제 Unity 원본 → Unreal → 복원 Unity에서 20개 조합의 18개 값이 GC 전후 일치했습니다. 동기 Unreal GC 완료 20회를 확인했습니다. 함수 인수·반환·재대입, 위치 변경과 부모 설정·해제를 포함합니다. parent = null은 SetParent(null, true)로 복원될 수 있으며 두 실행 결과를 대조했습니다. 일반 변환 API가 실행한 헤더·복원 코드를 그대로 생성하는 것도 확인했습니다.</p><p>49개 스크립트의 프로젝트 생성·복원 및 런타임 왕복도 통과했습니다. Unity 원본 55프레임·Unreal 56프레임·복원 Unity 55프레임에서 프리팹 8개 체크포인트, GC 후 Transform 식별, 세 장면 전환을 대조했습니다. 기존 실제 Unity 원본/복원 250프레임 캡처와 새 Unreal 250프레임 회귀 비교도 통과했습니다. 이번 단계의 내부 검사는 296개입니다. 사용자 원본 {len(baseline)}개 파일의 해시가 기존 기준과 일치합니다.</p><p><strong>남은 범위:</strong> 실행 버전은 Unity 6000.0.83f1과 Unreal 5.8입니다. UI 컴포넌트 식별, 모든 Transform API, 약한 참조 Blueprint 호출 핀, 모든 엔진 버전 및 전체 프로젝트 포팅 완료는 검증하지 않았습니다. 물리 궤적과 렌더링의 동등성도 보장하지 않습니다. 기존 안전 실행기와 방화벽 검사·잠금을 사용했으며 방화벽 정책 변경·UAC·컴퓨터 유즈는 수행하지 않았습니다. GUI 보안창 유무 자체는 검사하지 않았습니다.</p></section>'''
for path in(root/'docs/guide.html',root/'artifacts/development-report.html'):
 text=path.read_text(encoding='utf-8-sig');text=re.sub(r'<section id="transform-identity-actual">.*?</section>','',text,flags=re.S)
 if '</main>'not in text:raise RuntimeError('HTML insertion anchor missing')
 path.write_text(text.replace('</main>',section+'</main>'),encoding='utf-8')
record={'status':'TRANSFORM_GC_ENGINE_VERIFIED_AWAITING_RELEASE','goalComplete':False,'phaseStartedKst':'2026-10-23T20:40:27+09:00','recordedKst':now.isoformat(),'internalChecks':296,'originalFilesUnchanged':len(baseline),'standalone':standalone,'project':project,'xerj':'Unavailable; rg fallback'}
(home/'checkpoint.json').write_text(json.dumps(record,indent=2));print('TRANSFORM_GC_ACTUAL_ENGINE_STAGE_RECORDED')
