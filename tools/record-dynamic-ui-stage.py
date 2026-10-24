"""Gate the dynamic UI release on actual scoped captures and preserved originals."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import json,re,runpy
from object_equality_evidence import require,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/dynamic-ui-check'
runpy.run_path(str(root/'tools/verify-dynamic-ui-probe.py'))
proof=json.loads((home/'verification.json').read_text())
for name in ('verify-ui-equality-world.py','verify-ui-api-evidence.py'):runpy.run_path(str(root/'tools'/name))
prior=json.loads((root/'artifacts/ui-equality-check/pre-normalization-restored-hashes.json').read_text());current={p.name:sha(p) for p in (root/'artifacts/ui-equality-check/world-restored-scripts').glob('*.cs')};require(prior==current and len(current)==49,'Reused Unity sources changed')
require('308 checks passed.' in (home/'internal-checks.log').read_text(encoding='utf-8-sig'),'Internal regressions incomplete')
for folder in ('legacy-regression-final','legacy-restored-comparison-final'):
 result=json.loads((root/'artifacts/ui-equality-check'/folder/'verification.json').read_text(encoding='utf-8-sig'));require(result.get('status')=='PASS' and result.get('frames')==250,'Actual 250-frame regression missing')
public=json.loads((root/'artifacts/ui-equality-check/public-pipeline-verification.json').read_text())
require(public.get('status')=='PUBLIC_PIPELINE_MATCHES_ENGINE_TESTED_HEADERS' and public.get('headers')==49 and public.get('restored')==49,'Existing public pipeline changed')
originals=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
for path,digest in originals.items():require(sha(Path(path)).lower()==digest.lower(),'User original changed: '+path)
stamp=datetime.now(timezone(timedelta(hours=9))).isoformat()
section=f'''<section id="dynamic-ui-actual"><h2>Canvas·Button 동적 추가·재추가 — 구현·실제 왕복 검증 완료</h2><p>{stamp}. 지원 스크립트에서 GameObject.AddComponent&lt;Canvas&gt;()와 AddComponent&lt;Button&gt;()를 변환합니다. 빈 객체에서 생성하는 경우와 장면에 기존 UI 컴포넌트가 있는 경우를 각각 Unity 6000.0.83f1 → Unreal 5.8 → 복원 Unity에서 실행했습니다. 기존 UI 재추가 중 삭제 콜백의 자체 제거 후 캡처 값 접근 문제를 실제 중단 로그로 확인해 수정했습니다. 콜백에 필요한 값을 먼저 복사하며 기존 UI 삭제 헤더의 역변환 호환도 유지합니다. 두 시나리오의 실제 캡처 6개에서 10프레임·18개 값이 일치했습니다.</p><table><thead><tr><th>사용 동작</th><th>확인한 결과</th></tr></thead><tbody><tr><td>동일 종류 중복 추가</td><td>이 버전의 실제 Unity와 같이 null 반환</td></tr><tr><td>Destroy 후 다음 프레임 재추가</td><td>새 인스턴스 생성, 이전 참조는 계속 null 판정, 새 참조와 구분</td></tr><tr><td>enabled / interactable / gameObject</td><td>기본값·설정값과 소유 객체 대응 유지</td></tr><tr><td>생성·재추가 후 GameObject 복제</td><td>복제본의 UI 참조 분리와 속성 상태 유지</td></tr><tr><td>삭제된 이전 참조를 다시 삭제</td><td>새 컴포넌트에 영향을 주지 않음</td></tr></tbody></table><p><strong>적용:</strong> 지원되는 Canvas·Button 동적 추가를 사용하는 스크립트 변환 시 자동 적용하며, 역변환 시 원래 AddComponent 호출로 복원합니다. 내부 검사 308개와 기존 49개 스크립트를 수정 런타임으로 다시 생성·빌드하고, 새 Unreal 실행으로 프리팹 8개 체크포인트·GC·세 장면 전환 및 UI API 20개 체크포인트 및 기존 250프레임을 회귀 대조했습니다. 복원 Unity 소스 49개의 SHA-256이 이전 실제 실행본과 같아 기존 Unity 원본·복원 캡처를 재사용했습니다. Unity 실행을 전부 이번에 다시 수행했다는 뜻은 아닙니다. 사용자 원본 {len(originals)}개 파일의 해시를 확인했습니다.</p><p><strong>남은 범위:</strong> 이번 검증은 기본 Canvas·Button의 참조·상태·삭제·복제입니다. Graphic·텍스트·이미지 등 다른 UI 컴포넌트의 동적 생성, RectTransform 전용 속성, 새 위젯의 렌더링·사용자 클릭 동등성, 모든 엔진 버전 및 전체 프로젝트 포팅 완료를 검증한 것은 아닙니다.</p></section>'''
for path in (root/'docs/guide.html',root/'artifacts/development-report.html'):
 text=path.read_text(encoding='utf-8-sig');text=re.sub(r'<section id="dynamic-ui-actual">.*?</section>','',text,flags=re.S)
 text=text.replace('동적 UI 추가, 다른 UI 타입,','Graphic·RectTransform 전용 속성 및 다른 UI 타입,')
 require('</main>' in text,'HTML anchor missing');path.write_text(text.replace('</main>',section+'</main>'),encoding='utf-8')
record={'status':'DYNAMIC_UI_ACTUAL_ROUNDTRIP_VERIFIED_AWAITING_RELEASE','goalComplete':False,'recordedKst':stamp,'internalChecks':308,'originalFilesUnchanged':len(originals),'roundtrip':proof,'publicPipeline':public,'sourceHashes':{str(p.relative_to(root)):sha(p) for p in (root/'src/EngineMigrator.Core/WeakUiPorting.cs',root/'src/EngineMigrator.Core/DynamicUiPorting.cs',root/'src/EngineMigrator.Core/ActorScripts.cs',root/'src/EngineMigrator.Core/ActorValidationSurface.cs',root/'tests/EngineMigrator.Checks/Fixtures/dynamic-ui.cs.txt')},'xerj':'Unavailable; rg fallback'}
(home/'checkpoint.json').write_text(json.dumps(record,indent=2));print('DYNAMIC_UI_STAGE_RECORDED')
