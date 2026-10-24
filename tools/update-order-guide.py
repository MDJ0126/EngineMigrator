from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html';s=p.read_text(encoding='utf-8')
s=s.replace('Unreal의 사용자 스크립트 Update는 명시적 실행 순서가 낮은 Actor를 Tick 선행 조건으로 사용합니다.','Unreal의 작성된 사용자 C++ 스크립트는 명시적 실행 순서에 따라 초기 Awake·OnEnable을 처리한 뒤 Start를 정렬 실행합니다. Update도 낮은 순위의 Actor를 Tick 선행 조건으로 사용합니다.')
s=s.replace('서로 다른 객체의 Awake/Start 전체 호출 순서와 재진입 조합은 아직 실행 동등성이 검증되지 않았으며, 명시적 순서가 있는 원본을 내보낼 때 이 제한을 경고합니다.','동일 순위·임의 동적 생성·모든 재진입 조합의 실행 동등성은 별도 검증 범위이며, 명시적 순서가 있는 원본을 내보낼 때 이 범위를 안내합니다.')
s=s.replace('모든 초기 Awake/Start의 객체 사이 호출 순서, 상호·재진입 콜백,','초기 실행 순서와 재진입은 다음 항목의 확인 범위를 참고해 주세요. 그 밖의 상호·재진입 콜백,')
s=s.replace('activeInHierarchy는 부모 계층을 따라 계산합니다.','activeInHierarchy는 부모 계층과 활성화 진행 상태를 반영합니다.')
s=s.replace('<td>Awake</td><td>활성 객체는 PostInitializeComponents, 비활성 객체는 최초 활성화 때 한 번</td>','<td>Awake</td><td>초기 작성 객체는 명시적 순서의 시작 배치, 동적 객체는 PostInitializeComponents, 비활성 객체는 최초 활성화 때 한 번</td>')
s=s.replace('초기 활성 컴포넌트는 BeginPlay,','초기 활성 컴포넌트는 시작 배치,')
s=s.replace('활성 콜백의 재진입·상호 호출','검사한 경우 외의 활성 콜백 재진입·상호 호출')
s=s.replace('초기 실행 순서와 재진입은 다음 항목의 확인 범위를 참고해 주세요.','초기 실행 순서와 재진입은 해당 항목의 확인 범위를 참고해 주세요.')
s=s.replace('Unity는 경고를 기록하고 activeSelf=true','Unity는 메시지를 기록하고 activeSelf=true')
marker='<h3>부모 변경과 초기 Awake/Start 내부 생성·삭제</h3>'
addition='''<h3>초기 실행 순서와 활성 콜백 재진입</h3><p>작성된 사용자 C++ 스크립트의 초기 Awake·OnEnable과 Start를 MonoImporter의 명시적 Script Execution Order 순서로 처리합니다. 초기 Awake·OnEnable을 모두 처리한 뒤 Start를 실행하며, 상속된 콜백과 명시적 부모 호출도 유지합니다. 비활성 객체는 이 배치에서 초기화하지 않고 실제 활성화까지 보류하며, 런타임 추가·복제 객체는 즉시 초기화하는 경로를 사용합니다. 동일 순위 사이의 순서는 보장하지 않습니다.</p><p>검증 프로젝트는 두 객체를 실행 순서와 반대로 배치하고 -100·100을 지정했습니다. 먼저 실행되는 객체는 다른 객체의 Awake·OnEnable·Start 횟수를 0으로, 나중 객체는 1로 읽는지와 전체 콜백 순서를 대조합니다. Update 제어 객체에는 -1000을 지정합니다.</p><p>OnEnable에서 자기 enabled를 false로 바꾸는 경우와 OnDisable에서 자기 enabled를 true로 되돌리는 경우를 검증했습니다. GameObject의 OnEnable 중 비활성화도 처리합니다. GameObject 비활성화 콜백 안에서 재활성화를 요청하면 Unity는 경고를 기록하고 activeSelf=true·activeInHierarchy=false 상태로 남을 수 있습니다. 이때 불필요한 OnEnable을 호출하지 않고, 이후 SetActive(true) 요청에서 계층을 다시 활성화하도록 맞췄습니다.</p><p>임시 시작 배치·재진입 상태는 복제 객체에서 정리합니다. 이 조합 자체의 모든 복제·부모 변경·삭제 동등성을 검증했다는 뜻은 아닙니다. 동일 순위의 객체, 임의 동적 초기화 순서, 깊은 재진입과 다른 객체를 변경하는 모든 조합, 물리·UI·애니메이션의 모든 조합은 아직 확인 범위 밖입니다.</p>'''
if addition not in s:s=s.replace(marker,addition+marker)
p.write_text(s,encoding='utf-8');print('RUNTIME_ORDER_GUIDE_UPDATED')
