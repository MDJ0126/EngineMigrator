from pathlib import Path
from datetime import datetime,timezone,timedelta
import json,re,sys
sys.path.insert(0,str(Path('tools').resolve()))
from object_equality_evidence import require,sha
root=Path.cwd();h=root/'artifacts/fixed-travel-check'
proof=json.loads((h/'verification.json').read_text());require(proof['status']=='ACTUAL_FIXED_TRAVEL_ROUNDTRIP_PASS','Actual travel missing')
require('322 checks passed.'in(h/'internal-checks.log').read_text(encoding='utf-8-sig'),'Checks missing')
public=json.loads((root/'artifacts/ui-equality-check/public-pipeline-verification.json').read_text());require(public['headers']==49 and public['restored']==49,'Public regression missing')
originals=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
for p,d in originals.items():require(sha(Path(p)).lower()==d.lower(),'Original modified '+p)
stamp=datetime.now(timezone(timedelta(hours=9))).isoformat()
section=f'''<section id="fixed-travel-actual"><h2>고정 이벤트 시계 — 3개 장면 실제 왕복 검증 완료</h2><p>{stamp}. C++ Runtime Actor의 고정 업데이트 시계를 GameInstance 수명으로 유지하도록 수정했습니다. 장면 정리 때 Actor 등록은 해제하되 누적 시간은 이어집니다. 설치된 Unreal 5.8 TickTaskManager.cpp에서 초기 프레임 0의 Actor Tick 방문 값과 실행 제외 조건을 확인했고, 실제 실행에서 고정 콜백만 먼저 진행하던 초기 준비 프레임을 제외했습니다.</p><p>Unity 6000.0.83f1 원본 → Unreal 5.8 게임 실행 → 복원 Unity를 실제 실행해 100 FPS / 요청 고정 간격 0.02초, 30 FPS / 0.01초의 각 3장면·26프레임·7개 값을 대조했습니다. 별도로 고정 스크립트가 없는 중간 장면을 5프레임 거치는 왕복도 대조했습니다. 빈 장면에서 마지막 장면으로 이동하는 호출은 두 엔진의 테스트 관찰기가 수행하며, 이 호출 자체가 사용자 스크립트 변환 검증이라는 뜻은 아닙니다. 장면별 Start 선행·고정 호출 횟수·초기 위상·누적 시간이 일치했습니다. 시간 값 허용 오차는 0.000002이며 나머지 상태는 정확히 일치했습니다.</p><p>이전 단일 장면 4조건을 새 네이티브 빌드로 각각 재실행해 기존 Unity 캡처와 대조했습니다. 해당 Unity 원본·복원 캡처는 이전 단계의 실제 실행 증거를 재사용했습니다. 내부 검사 322개와 기존 49개 스크립트 생성·복원 대조가 통과했으며 사용자 원본 {len(originals)}개 파일의 해시를 확인했습니다. 구형 시계 헤더의 역변환을 유지하되 서로 다른 시계 버전이 섞인 번들은 명시적으로 거부합니다.</p><p><strong>남은 검증:</strong> 임의 가변 프레임률·장시간 실행·비기본 timeScale·실행 중 fixedDeltaTime 변경·물리 서브스텝·Rigidbody 궤적 동등성·모든 엔진 버전 및 전체 프로젝트 포팅 완료는 검증하지 않았습니다.</p></section>'''
for p in [root/'docs/guide.html',root/'artifacts/development-report.html']:
 s=p.read_text(encoding='utf-8-sig');s=re.sub(r'<section id="fixed-travel-actual">.*?</section>','',s,flags=re.S);require('</main>'in s,'HTML missing main');p.write_text(s.replace('</main>',section+'</main>'),encoding='utf-8')
p=root/'README.md';s=p.read_text(encoding='utf-8-sig');s=s.replace('물리 서브스텝 및 장면 전환 중 시계 연속성은 별도 검증 범위입니다.','고정 시계의 3장면 전환과 빈 중간 장면도 실제 왕복 대조를 통과했습니다. 물리 서브스텝과 임의 가변 시간 배율은 별도 검증 범위입니다.');p.write_text(s,encoding='utf-8')
record={'status':'FIXED_TRAVEL_VERIFIED_AWAITING_RELEASE','goalComplete':False,'recordedKst':stamp,'internalChecks':322,'originalFilesUnchanged':len(originals),'roundtrip':proof,'publicPipeline':public}
(h/'checkpoint.json').write_text(json.dumps(record,indent=2))
p=root/'tools/verify-fixed-update-release.py';s=p.read_text().replace('fixed-update','fixed-travel');(root/'tools/verify-fixed-travel-release.py').write_text(s)
print('REPORT_AND_RELEASE_GATE_READY')
