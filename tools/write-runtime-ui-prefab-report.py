"""Publish the milestone only when independent engine and shipping gates pass."""
from pathlib import Path
import hashlib,html,json,re
root=Path(__file__).resolve().parents[1];art=root/'artifacts';tests=art/'runtime-ui-prefab-tests'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
config=read(tests/'paths.json');forward=root/config['forward'];reverse=root/config['reverse'];packaging=root/config['packaging'];release=root/config['release'];cli=root/config['cli']
baseline=read(art/'user-project-baseline/original-hashes.json');assert all(sha(Path(p))==h.upper() for p,h in baseline.items())
for run,engine in ((forward,'unreal'),(reverse,'unity')):
 assert (run/'resource-port-result.json').is_file()
 verification=read(run/'Transfer'/f'{engine}-import-result.json');assert verification['savedAndReloaded'] and verification['referencesVerified']
paths={'unityBaseline':tests/'unity-baseline-verification.json','forwardRuntime':tests/'unreal-verification.json','restoredRuntime':tests/'restored-verification.json','projectRoundtrip':reverse/'project-roundtrip-verification.json','generatedHeaders':tests/'latest-generated-v6/verification.json','uiSourceCompatibility':tests/'ui-source-compatibility.json','prototypeGuard':tests/'prototype-guard-verification.json','packagedRuntime':packaging/'verification.json','archive':art/'runtime-ui-prefab-archive-verification.json','appSmoke':tests/'app-smoke-verification.json'}
paths.update({name+'Regression':tests/(name+'-regression-verification.json') for name in ('animation-prefab','physics-prefab','dependency','dormant','destroy','order','phase','active','enable','component','placement','prefab')})
paths.update({name:forward/config[name] for name in ('legacyRuntime','restoredLegacyRuntime')})
checks={name:read(path) for name,path in paths.items()};assert all(v['status']=='PASS' for v in checks.values())
assert Path(checks['appSmoke']['evidence']).resolve().is_relative_to(Path(checks['archive']['unicodeExtraction']).resolve())
assert checks['packagedRuntime']['packagedExecutableExecuted']
assert len(checks['prototypeGuard']['cases'])==2 and all(c['actualWidgetCallbackChanged'] and c['unsynchronizedUIEditRejected'] and c['restoredInMemory'] for c in checks['prototypeGuard']['cases'])
for name in ('unityBaseline','forwardRuntime','restoredRuntime','packagedRuntime'):
 assert len(checks[name]['checkpoints'])==20 and checks[name]['checkpoints']['16']['Passive']['visible']
 assert checks[name]['sceneTransitionWithVisibleWidgetVerified'] and checks[name]['checkpoints']['20']['Sleeping']['visible']
for name in ('forwardRuntime','packagedRuntime'):assert checks[name]['nativeViewportCleanupVerified']
count=checks['generatedHeaders']['headersCompared'];assert count==checks['projectRoundtrip']['scriptIdentitiesCompared']
assert '181 checks passed' in (tests/'checks-final.log').read_text(encoding='utf-8-sig')
assert sha(cli/'EngineMigrator.Core.dll')==sha(release/'EngineMigrator.Core.dll')
for name,h in checks['generatedHeaders']['hashes'].items():assert sha(packaging/'Project/Plugins/EMActorPort/Source/EMActorPort/Public'/name)==h.upper()
checks.update(status='PASS',cleanCliForwardAndReverse=True,automatedChecks=181,originalFilesUnchanged=len(baseline),computerUse=False,projectPortComplete=False,renderingEquivalent=False)
limit=read(tests/'ui-transform-limit.json');assert limit['status']=='KNOWN_UNSUPPORTED' and not limit['uiTransformAPISupported'];checks['uiTransformLimit']=limit
(tests/'milestone-verification.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
links=[('최신 실행 파일',release.name+'/EngineMigrator.App.exe'),('배포 ZIP',release.name+'.zip'),('사용 가이드','../docs/guide.html'),('실행 검증 근거','runtime-ui-prefab-tests/milestone-verification.json'),('프로젝트 왕복 대조',str(reverse.relative_to(art)).replace('\\','/')+'/project-roundtrip-verification.json'),('실제 변환 대응표',str(reverse.relative_to(art)).replace('\\','/')+'/resource-port.html')]
cards=''.join('<a href="'+html.escape(p)+'">'+html.escape(label)+' ↗</a>' for label,p in links)
current=art/'development-report.html';backup=art/'development-report-runtime-animation-prefab-20261023.html'
if not backup.exists():backup.write_bytes(current.read_bytes())
style=re.search(r'<style>.*?</style>',backup.read_text(encoding='utf-8'),re.S).group()
roundtrip=checks['projectRoundtrip']
body='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>EngineMigrator 런타임 UI 프리팹 왕복 검증</title>'''+style+'''<main><div class="badge">ENGINE MIGRATOR · UI PREFAB PREVIEW · 2026-10-23</div><h1>UI 프리팹 복제와 콜백 연결<br>실제 왕복 실행 검증</h1><p>Unity 6000.0.83f1 → Unreal 5.8.1 → Unity · 컴퓨터 유즈 미사용</p><div class="cards">'''+cards+'''</div><h2>이번에 추가한 동작</h2><section><ul><li>UI 프리팹의 표시 관리 Actor를 계층과 함께 복제하고, 실제 UMG 위젯과 버튼 콜백을 새 객체·컴포넌트 집합으로 연결합니다. 원본 두 개와 실행 중 재복제본의 상태를 독립적으로 대조했습니다.</li><li>비활성 프리팹 첫 활성화·루트 비활성화/복구·버튼 객체 비활성화/복구·처음부터 disabled인 Canvas·콜백 없는 UI를 포함했습니다. 루트·자식 콜백 순서와 Off 상태, 생명주기 카운터를 확인했습니다.</li><li>원본 삭제 뒤 복제본 버튼의 독립 실행, 전체 삭제 시 뷰포트 제거, 남은 UI를 둔 채 장면 전환을 검사했습니다. 매 시점 살아 있는 표시 관리 Actor와 뷰포트 위젯 수를 함께 대조했습니다. 활성 UI를 남긴 장면 전환 뒤에는 월드 구분 없이 뷰포트 등록 위젯이 0개인지 확인해 이전 월드의 잔존 UI도 검사합니다.</li><li>실제 Widget Blueprint의 콜백을 메모리에서 변경했을 때 보호 검사가 거부하고 복원 후 통과하는 것을 확인했습니다. 맵·자산은 저장하지 않았습니다. 기존 배포본의 UI 런타임 소스도 계속 읽을 수 있습니다.</li><li>비활성 Canvas 검색 누락과 Overlay 프리팹 미리보기의 0 스케일 사전 검사, 복제 Actor 생성 시 Owner 초기화로 연결이 누락된 문제를 실행 근거로 수정했습니다.</li></ul></section><h2>검증 근거</h2><section><p>최신 프로그램으로 빈 출력 폴더에서 전체 CLI 정방향 변환과 역변환을 수행했습니다. <strong>20개 시점</strong>을 Unity 원본·Unreal·복원 Unity·패키징한 Windows 게임에서 대조했습니다.</p><p>버튼 검증은 Unity Button.onClick.Invoke와 Unreal UButton.OnClicked Broadcast를 실제 런타임에서 호출하는 방식입니다. 마우스 클릭 좌표·입력 라우팅·픽셀 렌더링 동일성은 검증하지 않았습니다.</p><p>''' + f"장면 {roundtrip['sceneCount']}개 · 프리팹 정의 {roundtrip['prefabDefinitions']}개 · 객체 {roundtrip['nodesCompared']}개 · 스크립트 GUID/실행 순서 {count}개"+'''의 재내보낸 데이터·자산·참조를 대조했습니다. 기존 런타임 애니메이션·물리·순환 프리팹·비활성·삭제·순서·생성 단계·활성 계층·enabled·컴포넌트·좌표 복제·기본 프리팹의 <strong>12개 실제 회귀 시나리오</strong>, 기존 물리 접촉·장면 애니메이션·UI·상속의 원본/복원 <strong>250프레임</strong> 검사를 통과했습니다.</p><p>자동 검사 181개, 생성·실행·패키징 헤더와 배포 Core 해시, ZIP CRC·추출 해시·한글 경로 앱 자체 실행을 확인했습니다. 사용자 원본 60개 파일은 변경하지 않았습니다.</p></section><h2>전체 포팅은 계속 개발 중입니다</h2><section><ul><li>지원 UI는 기존 기본 Overlay Canvas와 지원 위젯, 인수 없는 public void 영구 콜백입니다. 중첩 Canvas·일반 회전/배율·버튼 시각 전환·외부 폰트·실행 중 AddListener·일반 UI 컴포넌트 API는 남아 있습니다. 씬에 배치된 UI의 동적 복제와 외부 콜백 대상 조합도 추가 구현·검증 대상입니다.</li><li>UI Transform API의 화면 좌표·배율 동등성은 미지원이며 실제 Canvas 루트 배율 차이를 확인했습니다. 마우스 입력·시각적 동일성과 모든 초기 생명주기의 UI 생성 조합은 미검증입니다.</li><li>Animator·스켈레탈·애니메이션 이벤트·블렌딩·재생 제어, 일반 엔진 컴포넌트 조회/생성·복잡한 물리, 비동기/Additive·DontDestroyOnLoad, 중첩/Variant·깊은 재진입·장시간 GC가 남아 있습니다.</li><li>임의 Blueprint·외부 의존성·전체 API·모든 엔진 버전 대응은 미완료입니다.</li></ul></section><small>projectPortComplete=false / renderingEquivalent=false · 실제 실행 근거가 있는 범위만 통과로 기록합니다.</small></main></html>'''
current.write_text(body,encoding='utf-8');print('RUNTIME_UI_PREFAB_MILESTONE_REPORT_PASS')
