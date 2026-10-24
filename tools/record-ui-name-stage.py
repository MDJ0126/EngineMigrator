"""Publish only the actually verified naming scope and preserved existing evidence."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import json,re,runpy
from object_equality_evidence import require,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/ui-name-check'
runpy.run_path(str(root/'tools/verify-ui-name-probe.py'))
proof=json.loads((home/'verification.json').read_text())
require('312 checks passed.' in (home/'internal-checks.log').read_text(encoding='utf-8-sig'),'Internal regression incomplete')
public=json.loads((root/'artifacts/ui-equality-check/public-pipeline-verification.json').read_text())
require(public.get('headers')==49 and public.get('restored')==49 and public.get('status')=='PUBLIC_PIPELINE_MATCHES_ENGINE_TESTED_HEADERS','Project generation regression incomplete')
project=json.loads((root/'artifacts/ui-equality-check/project-verification.json').read_text())
headers=root/'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Target/Plugins/EMActorPort/Source/EMActorPort/Public'
require(len(project['headerHashes'])==49,'Native project evidence missing')
for name,digest in project['headerHashes'].items():require(sha(headers/name)==digest,'Previously executed project header changed: '+name)
originals=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
for path,digest in originals.items():require(sha(Path(path)).lower()==digest.lower(),'User original changed: '+path)
stamp=datetime.now(timezone(timedelta(hours=9))).isoformat()
section=f'''<section id="ui-owner-name-actual"><h2>Canvas·Button 이름 변경 — 소유 객체 대응 수정·실제 검증 완료</h2><p>{stamp}. 동적 UI 컴포넌트의 name을 변경할 때 내부 참조 객체만 바뀌고 소유 GameObject 이름은 유지되던 차이를 실제 Unreal 게임 모드에서 재현했습니다. 수정 전 8개 관찰 행 중 3개가 Unity 원본·복원본과 달랐으며, 수정 후 기존 UI·동적 UI 모두 일치했습니다.</p><table><thead><tr><th>Unity 스크립트</th><th>확인한 결과</th></tr></thead><tbody><tr><td>Canvas.name = 문자열 상수</td><td>소유 GameObject 이름 변경</td></tr><tr><td>Button.name = 문자열 상수</td><td>같은 소유 GameObject 이름 변경</td></tr><tr><td>복제한 객체의 Canvas.name 변경</td><td>복제본 이름만 변경하고 원본 이름 유지</td></tr></tbody></table><p>UI 종류별 이름 변경과 복제 단계를 기존·동적 UI 두 시나리오에서 실행했으며, 한글과 공백을 포함한 이름을 확인했습니다. Unity 원본·복원 C#은 한 번의 실제 Unity 실행에서 각각 실행했습니다. 수정 후 복원 C# 바이트가 해당 실행본과 같음을 확인하고 캡처를 재사용했으며, 새 Unreal 게임 모드 실행 결과를 대조했습니다. 초기 편집기 Python 호출은 종료 오류로 성공 처리하지 않았고, 실제 검증은 정상 종료한 게임 모드 실행을 사용했습니다. 초기 종료 오류의 원인은 미확인으로 남아 있습니다.</p><p>동적 UI 생성·삭제·재추가·복제의 두 게임 모드 회귀와 내부 검사 312개가 통과했습니다. 기존 49개 스크립트의 생성 헤더·복원 C#은 직전 실제 검증본과 같음을 대조했습니다. 전체 49개 프로젝트의 엔진 실행을 이번에 다시 수행했다는 뜻은 아닙니다. 이전 UI 이름 변환 헤더는 백엔드 메타데이터를 보존해 역변환할 수 있으며, 새 헤더의 소유 객체 대응 코드를 제거하거나 이전 방식으로 바꾸면 역변환을 차단합니다. 사용자 원본 {len(originals)}개 파일은 해시가 일치합니다.</p><p><strong>범위:</strong> 살아 있는 지원 Canvas·Button 참조에 대한 name 문자열 상수 대입을 검증했습니다. 이름 읽기·동적 문자열 연산·삭제된 참조의 예외 동등성·다른 UI 타입·렌더링·클릭 동등성·모든 엔진 버전·전체 프로젝트 포팅 완료는 검증하지 않았습니다.</p></section>'''
for path in (root/'docs/guide.html',root/'artifacts/development-report.html'):
 text=path.read_text(encoding='utf-8-sig');text=re.sub(r'<section id="ui-owner-name-actual">.*?</section>','',text,flags=re.S)
 require('</main>' in text,'HTML anchor missing');path.write_text(text.replace('</main>',section+'</main>'),encoding='utf-8')
record={'status':'UI_OWNER_NAME_VERIFIED_AWAITING_RELEASE','goalComplete':False,'recordedKst':stamp,'internalChecks':312,'originalFilesUnchanged':len(originals),'roundtrip':proof,'publicPipeline':public,'reusedProjectHeaderHashes':project['headerHashes'],'editorShutdownFailure':'미확인; failed receipt excluded; game-mode execution used','sourceHashes':{str(p.relative_to(root)).replace('\\','/'):sha(p) for p in (root/'src/EngineMigrator.Core/ActorScripts.cs',root/'src/EngineMigrator.Core/UiNamePorting.cs',root/'tests/EngineMigrator.Checks/Fixtures/ui-name.cs.txt')},'xerj':'Unavailable; rg fallback'}
(home/'checkpoint.json').write_text(json.dumps(record,ensure_ascii=False,indent=2));print('UI_OWNER_NAME_STAGE_RECORDED')
