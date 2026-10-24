from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html';s=p.read_text(encoding='utf-8')
s=s.replace('GameObject 자체는 활성 상태여야 하며, <code>SetActive</code>와 비활성 GameObject 계층은 아직 지원하지 않습니다.','GameObject의 활성 상태와 계층 전파는 아래 SetActive 지원 범위를 참고해 주세요.')
s=s.replace('임의 생성 시점·재진입·전체 실행 순서와 비활성 GameObject 동등성은 별도 검증 범위입니다.','임의 생성 시점·재진입·전체 실행 순서는 별도 검증 범위입니다.')
marker='<h3>OnEnable/OnDisable과 컴포넌트 enabled</h3>'
addition='''<h3>GameObject 활성 상태와 비활성 계층</h3><p><code>GameObject.SetActive(bool)</code>, <code>activeSelf</code>와 <code>activeInHierarchy</code> 조회를 네이티브 Actor 코드로 변환하고 다시 C#으로 복원합니다. 작성 시 비활성인 장면 객체와 부착 스크립트·참조도 함께 전달합니다. 원본 프로젝트는 수정하지 않습니다.</p><p>GameObject 자체의 activeSelf는 해당 객체의 태그로 보존하고, activeInHierarchy는 부모 계층을 따라 계산합니다. 부모가 비활성이어도 자식의 activeSelf는 유지됩니다. 계층이 실제 활성 상태로 전환될 때 스크립트의 Awake·OnEnable을 호출하고 Start를 지연 실행합니다. 비활성 객체에 AddComponent로 추가한 스크립트도 첫 활성화까지 Awake를 보류합니다. 비활성화는 OnDisable을 동기 호출하고 Update를 중지하며, 재활성화한 프레임의 Update는 보류합니다.</p><p>검증 시나리오는 부모 토글, 부모가 비활성일 때 자식 자체 토글, 반복 동일 값 설정, 같은 프레임 false→true, 작성 시 비활성 객체의 최초 활성화, disabled 컴포넌트와 계층 활성 상태 조합, 비활성 런타임 객체에 컴포넌트 추가 및 삭제입니다. 객체의 일반 활성/비활성 상태는 메시 표시와 Actor 충돌 상태에도 연결됩니다. 모든 종류의 물리·UI·애니메이션 컴포넌트 토글, 활성 콜백의 재진입·상호 호출, 비활성 프리팹 생성과 부모 변경으로 인한 활성 이벤트 순서는 아직 실행 검증 범위 밖입니다.</p>'''
if addition not in s:s=s.replace(marker,addition+marker)
p.write_text(s,encoding='utf-8');print('RUNTIME_ACTIVE_GUIDE_UPDATED')
