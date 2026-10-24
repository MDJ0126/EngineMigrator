from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html';s=p.read_text(encoding='utf-8')
s=s.replace('과 부모 변경으로 인한 활성 이벤트 순서는 아직 실행 검증 범위 밖입니다.','은 아직 실행 검증 범위 밖입니다.')
s=s.replace('·OnEnable/OnDisable·비활성/활성 토글','')
s=s.replace('Awake/Start 중 생성과 장시간 GC 이후의 모든 참조 동등성은 미검증이며','Awake/Start의 임의 재진입과 장시간 GC 이후의 모든 참조 동등성은 미검증이며')
s=s.replace('<td>Awake</td><td>PostInitializeComponents에서 한 번</td>','<td>Awake</td><td>활성 객체는 PostInitializeComponents, 비활성 객체는 최초 활성화 때 한 번</td>')
for previous in ('<td>Start</td><td>BeginPlay</td>','<td>Start</td><td>작성 객체는 BeginPlay. 동적 생성은 생성 시점에 따라 첫 Tick 전 또는 현재 Tick 뒤에 한 번</td>'):
 s=s.replace(previous,'<td>Start</td><td>활성화 후 한 번. 초기 활성 컴포넌트는 BeginPlay, 비활성·동적 컴포넌트는 활성화·생성 시점에 따라 첫 Tick 전 또는 현재 Tick 뒤</td>')
marker='<h3>스크립트 식별자와 실행 순서</h3>'
addition='''<h3>부모 변경과 초기 Awake/Start 내부 생성·삭제</h3><p><code>Transform.SetParent(parent)</code>, <code>SetParent(parent, worldPositionStays)</code>와 <code>transform.parent = null</code>의 부모 해제를 지원합니다. 활성·비활성 부모 사이 이동 후 activeSelf를 보존하고 activeInHierarchy·OnEnable·OnDisable·Update 상태를 갱신합니다. 월드 좌표 유지와 로컬 좌표 유지 옵션, 월드·로컬 위치와 회전을 함께 대조했습니다.</p><p>초기 Awake 또는 Start 안에서 비활성 객체를 만들고 사용자 컴포넌트를 추가·설정한 뒤 활성화하는 흐름을 검증했습니다. 생성 호출이 반환될 때 Awake·OnEnable은 실행됐지만 Start는 아직 실행되지 않습니다. 첫 Tick 전에 생성한 컴포넌트의 Start를 첫 Update 전에 처리하여 같은 프레임부터 Update가 실행되도록 합니다. Update 도중 생성한 경우에는 기존처럼 현재 Tick 뒤에 Start를 처리하고 다음 프레임부터 Update를 실행합니다.</p><p><code>Destroy(this)</code>로 컴포넌트를 자기 삭제할 수 있습니다. 검증한 활성 컴포넌트의 Awake 내부 삭제는 Awake→OnDisable→OnDestroy 순서이며 OnEnable·Start·Update는 실행하지 않습니다. Start 내부 삭제는 Awake→OnEnable→Start→OnDisable→OnDestroy 순서이고 Update는 실행하지 않습니다. OnDisable은 삭제 요청 안에서 호출하고 실제 제거·OnDestroy는 해당 프레임의 Tick 이후 처리합니다.</p><p>이 범위는 초기 생성과 검사한 부모 변경 흐름에 대한 실행 검증입니다. 모든 초기 Awake/Start의 객체 사이 호출 순서, 상호·재진입 콜백, 잘못된 순환 부모 지정, 임의 생성 단계와 모든 물리·UI·애니메이션 조합의 동등성은 아직 미검증입니다.</p>'''
if addition not in s:s=s.replace(marker,addition+marker)
p.write_text(s,encoding='utf-8');print('RUNTIME_PHASE_GUIDE_UPDATED')
