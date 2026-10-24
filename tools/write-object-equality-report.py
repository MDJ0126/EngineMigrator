"""Update the HTML guide/report from verified results, without launching a GUI."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import hashlib,json,re,sys
from html.parser import HTMLParser

root=Path(__file__).resolve().parent.parent;home=root/'artifacts/object-equality-check';release=root/'artifacts/EngineMigrator-gc-reference-preview-20261023'
result=json.loads((home/'actual-roundtrip-verification.json').read_text());checks=(home/'release-checks.log').read_text(encoding='utf-8-sig');public=json.loads((home/'public-pipeline-verification.json').read_text())
if result['status']!='ACTUAL_GC_REFERENCE_ROUNDTRIP_PASS'or '286 checks passed.'not in checks or public['headers']!=49:raise RuntimeError('Current validation gates are incomplete')
for folder in ('legacy-regression-final','legacy-restored-comparison-final'):
 data=json.loads((home/folder/'verification.json').read_text(encoding='utf-8-sig'))
 if data['status']!='PASS'or data['frames']!=250:raise RuntimeError('250-frame legacy comparison incomplete')
stamp_file=home/'report-timestamp.json'
if not stamp_file.exists():stamp_file.write_text(json.dumps({'utc':datetime.now(timezone.utc).isoformat()}))
stamp=datetime.fromisoformat(json.loads(stamp_file.read_text())['utc']).astimezone(timezone(timedelta(hours=9))).strftime('%Y-%m-%d %H:%M:%S KST')
section='''<section id="gc-reference-actual"><h2>GameObject GC 참조 수정 · 실제 왕복 검증 통과</h2><p>STAMP 기준. 서로 다른 삭제 객체의 참조가 GC 후 모두 null이 되어 같다고 판단하던 문제를 실제 Unreal 동기 GC에서 재현하고 수정했습니다. 객체 인덱스와 일련번호를 유지하는 약한 참조로 객체의 생존 여부와 식별 정보를 구분합니다.</p><table><thead><tr><th>Unity 코드</th><th>Unreal 대응</th><th>복원 결과</th></tr></thead><tbody><tr><td>GameObject a, b / a == b</td><td>동등성 비교가 있는 변환 묶음은 GameObject 필드·지역 변수·인수·반환값에 TWeakObjectPtr&lt;AActor&gt;를 사용합니다.</td><td>GameObject / == 로 복원합니다.</td></tr><tr><td>삭제 객체와 null 비교</td><td>명시적 null과 객체의 생존 상태를 구분합니다.</td><td>삭제 객체는 null과 같게 처리합니다.</td></tr><tr><td>서로 다른 삭제 객체 비교</td><td>GC 이후에도 인덱스·일련번호를 비교합니다. Get() 결과끼리 비교하지 않습니다.</td><td>서로 다르면 같지 않습니다.</td></tr><tr><td>Instantiate / Destroy / SetActive 등</td><td>엔진 API를 호출하는 경계에서만 살아 있는 Actor 포인터를 꺼냅니다. 약한 참조 복사 시 식별 정보는 유지합니다.</td><td>지원 API의 원래 C# 호출로 복원합니다.</td></tr></tbody></table><p>실제 Unity 원본·Unreal·복원 Unity에서 20개 조합의 GC 전후 비교 및 인수·반환·조건식·null 재대입 흐름을 대조했습니다. Unreal 동기 GC 완료 콜백 20회를 확인했습니다. 프로젝트 실행은 Unity 원본 55프레임·Unreal 56프레임·복원 Unity 55프레임이며, 8개 프리팹 수명주기·참조 체크포인트와 RuntimeSpawner → RuntimeMiddle → RuntimeFinal 장면 전환이 일치했습니다. 프로젝트 안에서도 GC 완료 후 서로 다른 삭제 객체와 복제 별칭을 확인했습니다. 프레임 타임라인 전체가 동일하다는 뜻은 아닙니다.</p><p>현재 .NET 검사 285개가 통과했습니다. 일반 공개 변환 API가 엔진에서 실행한 49개 헤더와 정확히 일치하는 코드를 생성했으며, 49개 스크립트를 복원했습니다. 변경 대상 RuntimeSpawner를 제외한 48개는 이전 복원본과 Roslyn 토큰 종류·텍스트·누락 여부가 일치했습니다. 기존 실제 Unity 원본/복원 250프레임 캡처와 새 Unreal 250프레임 실행의 회귀 대조도 통과했습니다. 이는 이번에 Unity 250프레임을 다시 실행했다는 뜻은 아닙니다.</p><p><strong>지원 범위:</strong> 이번 실행 버전은 Unity 6000.0.83f1과 Unreal 5.8입니다. GameObject 참조 비교가 대상이며 모든 컴포넌트 식별·Blueprint 그래프·엔진 버전을 검증한 것은 아닙니다. 약한 GameObject 참조를 인수/반환값으로 쓰는 함수는 UFUNCTION()로 리플렉션되지만 해당 인수/반환값을 Blueprint 호출 핀으로 직접 노출하는 기능은 아직 지원하지 않습니다. 렌더링·입력·물리 궤적 동등성 및 전체 프로젝트 포팅 완료는 미확인입니다.</p><p>사용자 원본 60개 파일은 보존됐습니다. 전용 테스트 복사본과 기존 고정 에디터 경로를 사용하고 실행 전후 방화벽 검사, 배타 실행, IPC 감시를 유지했습니다. 전체 방화벽 설정이나 다른 프로젝트 프로세스는 변경하지 않았습니다. GUI 창 검사와 컴퓨터 유즈는 수행하지 않았습니다.</p></section>'''.replace('STAMP',stamp)
section=section.replace('285개','286개').replace('인수·반환·조건식·null 재대입','인수·반환·조건식·리플렉션 호출·인수의 값 복사·null 재대입')
def update(path,content):
 text=path.read_text(encoding='utf-8-sig')
 for old in ('object-equality-pending','object-equality-gc-audit','gc-reference-actual'):
  text=re.sub(r'<section id="'+old+r'">.*?</section>','',text,flags=re.S)
 if '</main>'not in text:raise RuntimeError('HTML content anchor missing')
 text=text.replace('</main>',content+'</main>');parser=HTMLParser();parser.feed(text);parser.close();path.write_text(text,encoding='utf-8')
update(root/'docs/guide.html',section)
if '--guide-only'in sys.argv:print('VERIFIED_GC_GUIDE_UPDATED_RELEASE_NOT_YET_PUBLISHED');sys.exit(0)
if not (release/'EngineMigrator.App.exe').is_file():raise RuntimeError('Publish the app before reporting a new release')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
if sha(release/'EngineMigrator.Core.dll')!=sha(root/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'):raise RuntimeError('Published Core differs from checked Release assembly')
link='<p><a href="'+release.name+'/EngineMigrator.App.exe">최신 실행 파일</a> · <a href="'+release.name+'.zip">배포 ZIP</a> · <a href="object-equality-check/actual-roundtrip-verification.json">실제 엔진 왕복 근거</a></p>'
update(root/'artifacts/development-report.html',section.replace('</section>',link+'</section>'))
print('VERIFIED_GC_REPORT_AND_GUIDE_UPDATED')
