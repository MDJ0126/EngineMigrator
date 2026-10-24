"""Record the verified single-world fixed-event stage without claiming all physics parity."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import json,re,runpy
from object_equality_evidence import require,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/fixed-update-check'
runpy.run_path(str(root/'tools/verify-fixed-update-probe.py'))
proof=json.loads((home/'verification.json').read_text())
require('319 checks passed.' in(home/'internal-checks.log').read_text(encoding='utf-8-sig'),'Internal checks incomplete')
public=json.loads((root/'artifacts/ui-equality-check/public-pipeline-verification.json').read_text())
require(public.get('headers')==49 and public.get('restored')==49 and public.get('status')=='PUBLIC_PIPELINE_MATCHES_ENGINE_TESTED_HEADERS','Existing project generation changed')
project=json.loads((root/'artifacts/ui-equality-check/project-verification.json').read_text())
headers=root/'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Target/Plugins/EMActorPort/Source/EMActorPort/Public'
for name,digest in project['headerHashes'].items():require(sha(headers/name)==digest,'Previously executed project header changed: '+name)
originals=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
for path,digest in originals.items():require(sha(Path(path)).lower()==digest.lower(),'Original user file changed: '+path)
stamp=datetime.now(timezone(timedelta(hours=9))).isoformat()
section=f'''<section id="fixed-update-actual"><h2>C++ Runtime Actor 고정 이벤트 — 단일 장면 실제 왕복 검증 완료</h2><p>{stamp}. 인수 없는 FixedUpdate와 상속·명시적 부모 호출, 이벤트 내부 Time.deltaTime 및 Time.fixedDeltaTime 읽기를 변환합니다. 고정 이벤트를 Unreal의 일반 Actor Tick보다 먼저 공통 시계에서 배치하며, 활성·비활성·삭제 대기 상태를 확인합니다. FixedUpdate가 단순 Blueprint 사용자 이벤트로 잘못 선택되지 않도록 C++ Runtime Actor 백엔드를 사용합니다. Blueprint 그래프 자체의 고정 이벤트 노드를 구현한 것은 아닙니다.</p><table><thead><tr><th>렌더 프레임률 / 고정 간격 요청</th><th>실제 대조</th></tr></thead><tbody><tr><td>100 FPS / 0.02초</td><td>고정 호출 없는 렌더 프레임 포함</td></tr><tr><td>50 FPS / 0.02초</td><td>시작 위상과 시간 0 호출 포함</td></tr><tr><td>30 FPS / 0.02초</td><td>한 프레임의 여러 고정 호출</td></tr><tr><td>30 FPS / 0.01초</td><td>한 프레임의 3~4회 고정 호출과 정수 경계</td></tr></tbody></table><p>각 조합을 Unity 6000.0.83f1 원본 → Unreal 5.8 → 복원 Unity에서 실행한 실제 캡처 12개로 대조했습니다. 조합마다 24프레임·16개 값을 확인했고, 정수 상태·호출 순서 코드는 정확히 일치했습니다. 시간 누적 등 부동소수점 값은 0.000002 이내 허용 범위를 적용했습니다. 상속된 FixedUpdate, 초기 Start 선행, OnEnable/OnDisable, GameObject 활성 전환, 런타임 AddComponent·삭제를 포함합니다.</p><p>부동소수점 누적으로 일부 프레임의 호출이 달랐던 결과를 보존하고 정수 시계로 수정했습니다. 실제 Unity 기록에서 기본 시간 단위 141,120,000틱/초와 요청 0.01초의 실제 고정 간격 1,411,199틱을 확인했습니다. 시간 단위 API는 <a href="https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.IntegerTime.RationalTime.html">Unity 6.0 공식 RationalTime 문서</a>를 함께 확인했습니다. 정수 시계 수정 후에도 복원 C# 4개 파일의 SHA-256이 실제 Unity 실행본과 같았습니다.</p><p>프로젝트의 고정 간격·틱 값을 스크립트 Actor에 전달하고, Unreal 장면 저장·다시 열기 후 컴포넌트 참조·실행 순서·설정값이 유지되는 것을 실제 어댑터로 확인했습니다. 잘못된 고정 간격 5종은 Actor 생성 전에 차단했습니다. 테스트 중 발견한 다른 컴포넌트 정수 필드 ++/--의 잘못된 네이티브 대입식도 수정했습니다. 내부 검사 319개가 통과했습니다. 기존 49개 스크립트의 생성 헤더·복원 결과는 이전 실제 검증본과 같음을 확인했으며, 이 단계에서 기존 전체 프로젝트 엔진 실행을 다시 수행했다는 뜻은 아닙니다. 사용자 원본 {len(originals)}개 파일을 보존했습니다.</p><p><strong>남은 범위:</strong> 이번 단계는 단일 장면의 고정 스크립트 이벤트입니다. 장면 전환 중 고정 시계 연속성, 임의 가변 프레임률·긴 시간 실행, 비기본 timeScale, 실행 중 fixedDeltaTime 변경, 물리 서브스텝과 스크립트의 완전한 교차 실행·Rigidbody API·궤적 동등성·모든 버전·전체 프로젝트 포팅 완료는 검증하지 않았습니다.</p></section>'''
for path in(root/'docs/guide.html',root/'artifacts/development-report.html'):
 text=path.read_text(encoding='utf-8-sig');text=re.sub(r'<section id="fixed-update-actual">.*?</section>','',text,flags=re.S)
 require('</main>'in text,'HTML anchor missing');path.write_text(text.replace('</main>',section+'</main>'),encoding='utf-8')
readme=root/'README.md';text=readme.read_text(encoding='utf-8-sig').replace('FixedUpdate/LateUpdate·OnEnable/OnDisable','Actor Blueprint의 FixedUpdate/LateUpdate·OnEnable/OnDisable')
note='\nC++ Runtime Actor의 단일 장면 FixedUpdate는 100/50/30 FPS와 0.02/0.01초 간격의 실제 왕복 대조를 통과했습니다. 프로젝트 고정 간격을 전달하며 상속·활성 전환·동적 컴포넌트를 처리합니다. 물리 서브스텝 및 장면 전환 중 시계 연속성은 별도 검증 범위입니다.\n'
if note not in text:text=text.replace('## 남은 범위','## 남은 범위\n'+note)
readme.write_text(text,encoding='utf-8')
record={'status':'FIXED_UPDATE_SINGLE_WORLD_VERIFIED_AWAITING_RELEASE','goalComplete':False,'recordedKst':stamp,'internalChecks':319,'originalFilesUnchanged':len(originals),'roundtrip':proof,'publicPipeline':public,'sourceHashes':{str(p.relative_to(root)).replace('\\','/'):sha(p)for p in(root/'src/EngineMigrator.Core/ActorScripts.cs',root/'src/EngineMigrator.Core/FixedUpdatePorting.cs',root/'src/EngineMigrator.Core/ActorBlueprintGraphs.cs',root/'src/EngineMigrator.Core/ActorValidationSurface.cs',root/'adapters/unreal/script_components.py',root/'tests/EngineMigrator.Checks/Fixtures/fixed-update.cs.txt')},'nextValidation':'Scene-travel fixed-clock continuity and broader physics stepping','xerj':'Unavailable; rg fallback'}
(home/'checkpoint.json').write_text(json.dumps(record,ensure_ascii=False,indent=2));print('FIXED_UPDATE_SINGLE_WORLD_STAGE_RECORDED')
