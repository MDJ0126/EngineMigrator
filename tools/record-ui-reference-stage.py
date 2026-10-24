"""Record only fully gated UI reference/removal/clone evidence; never starts engines."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import contextlib,hashlib,io,json,re,runpy
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/ui-equality-check'
def load(path):return json.loads(path.read_text(encoding='utf-8-sig'))
with contextlib.redirect_stdout(io.StringIO()):
 identity=runpy.run_path(str(root/'tools/verify-ui-equality.py'))['verify'](root,False)
 for tool in('verify-ui-destruction.py','verify-ui-api-evidence.py','verify-ui-equality-world.py'):runpy.run_path(str(root/'tools'/tool))
removal=load(home/'destruction-verification.json');api=load(home/'ui-api-verification.json');project=load(home/'project-verification.json')
for folder in('legacy-regression-final','legacy-restored-comparison-final'):
 result=load(home/folder/'verification.json')
 if result.get('status')!='PASS'or result.get('frames')!=250:raise RuntimeError('Actual 250-frame comparison missing')
if '302 checks passed.'not in(home/'release-checks.log').read_text(encoding='utf-8-sig'):raise RuntimeError('Internal regression incomplete')
prior=load(home/'pre-normalization-restored-hashes.json');current={p.name:hashlib.sha256(p.read_bytes()).hexdigest()for p in(home/'world-restored-scripts').glob('*.cs')}
if prior!=current or len(current)!=49:raise RuntimeError('Reused actual Unity sources changed')
originals=load(root/'artifacts/user-project-baseline/original-hashes.json')
for path,digest in originals.items():
 if hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()!=digest.upper():raise RuntimeError('User original changed: '+path)
rows=load(home/'destruction-original.json')['rows']
if rows[-1][-2:]!=[0,1]:raise RuntimeError('Observed Canvas/Button managed-null distinction changed')
cardinality=load(home/'ui-cardinality.json')
if cardinality!={'status':'CAPTURED','canvases':1,'buttons':1}:raise RuntimeError('UI cardinality model requires review')
now=datetime.now(timezone(timedelta(hours=9)));stamp=now.strftime('%Y-%m-%d %H:%M:%S KST')
section=f'''<section id="ui-reference-actual"><h2>Canvas·Button 참조·삭제·복제 — 실제 왕복 검증 통과</h2><p>{stamp} 기준. 같은 소유 객체의 Canvas와 Button을 같은 참조로 판단하거나 Canvas만 삭제했는데 살아 있다고 판단하던 차이를 실제 엔진에서 재현했습니다. 수정 전 30개 조합 중 28개가 달랐으며, 수정 후 30개 조합의 GC 전후 값이 Unity 원본 → Unreal → 복원 Unity에서 일치했습니다.</p><table><thead><tr><th>Unity 동작</th><th>Unreal 대응</th><th>검증 범위</th></tr></thead><tbody><tr><td>Canvas / Button 참조</td><td>소유 Actor의 약한 식별 정보와 UI 종류·존재 여부를 함께 사용</td><td>같은 소유 객체의 다른 UI 종류, null·삭제·비활성·GC 이후 비교</td></tr><tr><td>Destroy(Canvas) / Destroy(Button)</td><td>현재 프레임은 유지하고 프레임 말에 UI 종류 표식을 제거하며 표시/입력 상태를 갱신</td><td>중복 삭제, 다음 프레임 조회, 소유 GameObject 유지</td></tr><tr><td>삭제 후 복제</td><td>실행한 Unity 버전에서 관찰한 대로 Canvas는 원본 삭제 참조 식별을 보존하고 Button은 실제 null로 복제</td><td>관리 참조 null 상태와 명시적 약한 null 상태까지 대조</td></tr><tr><td>삭제 대기 중 복제</td><td>복제본에 원본의 삭제 대기 요청을 전달하지 않음</td><td>새 복제본의 Canvas를 별도로 삭제하는 동작 포함</td></tr></tbody></table><p>삭제·복제 시나리오는 실제 게임/플레이 모드 7프레임의 17개 값을 대조했습니다. 수정 과정에서 두 UI 종류를 모두 null로 처리하는 안도 실제 관리 참조 비교에서 실패해 제외했습니다. 성공한 최종 빌드의 소스와 DLL 해시를 기록하고 일치하는 빌드만 실행했습니다.</p><p>내부 검사 302개가 통과했습니다. 프로젝트 49개 스크립트의 일반 생성·복원 코드 일치를 확인했고, 프리팹 8개 체크포인트·GC·세 장면 전환과 UI API 20개 체크포인트의 상태 분리·외부 클릭 콜백을 새 Unreal 실행으로 회귀 대조했습니다. Unity 복원 소스 49개는 이전 실제 실행본과 SHA-256이 같아 해당 원본/복원 Unity 캡처를 재사용했습니다. 기존 실제 Unity 250프레임 원본·복원 캡처와 새 Unreal 250프레임 대조도 통과했습니다. 이 회귀의 Unity 실행을 모두 이번에 새로 수행했다는 뜻은 아닙니다.</p><p><strong>적용 안내:</strong> 지원 Canvas·Button 타입을 사용하는 스크립트 변환에 자동 적용합니다. 함수 입력·반환과 역변환 타입을 유지하며, 참조 별칭·복제 보일러플레이트가 바뀌면 역변환을 차단합니다. 이 Unity 버전의 중복 추가 시험에서는 한 객체의 Canvas와 Button이 각각 1개로 유지됐습니다.</p><p><strong>남은 범위:</strong> 이번 실제 실행 버전은 Unity 6000.0.83f1과 Unreal 5.8입니다. 동적 UI 추가, 다른 UI 타입, UI와 GameObject/Transform의 혼합 비교, 모든 엔진 버전, 물리 궤적·렌더링 동등성 및 전체 프로젝트 포팅 완료는 지원·검증 완료로 표시하지 않습니다. 사용자 원본 {len(originals)}개 파일은 해시가 일치합니다. 기존 안전 실행기의 잠금·방화벽 검사·시간 제한을 유지했고 다른 프로젝트 프로세스나 전체 방화벽 정책은 변경하지 않았습니다.</p></section>'''
for path in(root/'docs/guide.html',root/'artifacts/development-report.html'):
 text=path.read_text(encoding='utf-8-sig')
 for name in('ui-reference-pending','ui-reference-actual'):text=re.sub(r'<section id="'+name+r'">.*?</section>','',text,flags=re.S)
 if '</main>'not in text:raise RuntimeError('HTML anchor missing')
 path.write_text(text.replace('</main>',section+'</main>'),encoding='utf-8')
record={'status':'UI_REFERENCE_DESTRUCTION_CLONE_VERIFIED_AWAITING_RELEASE','goalComplete':False,'phaseStartedKst':'2026-10-23T22:15:52+09:00','recordedKst':now.isoformat(),'internalChecks':302,'identity':identity,'removal':removal,'uiApi':api,'project':project,'reusedUnityRestoredScripts':49,'unitySourceHashes':current,'originalFilesUnchanged':len(originals),'physicsTrajectoryEquivalent':False,'renderingEquivalent':False,'xerj':'Unavailable; rg fallback'}
(home/'checkpoint.json').write_text(json.dumps(record,indent=2));print('UI_REFERENCE_ACTUAL_ENGINE_STAGE_RECORDED')
