from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html';s=p.read_text(encoding='utf-8')
s=s.replace('Component 자체 복제·OnEnable/OnDisable·비활성/활성 토글·벡터나 사용자 컴포넌트 타입의 함수 인수/반환은 미지원입니다.','Component 자체 복제·GameObject 활성 토글·벡터나 사용자 컴포넌트 타입의 함수 인수/반환은 미지원입니다.')
marker='<h3>동적 MonoBehaviour와 컴포넌트 참조</h3>'
addition='''<h3>OnEnable/OnDisable과 컴포넌트 enabled</h3><p>사용자 MonoBehaviour의 <code>OnEnable</code>·<code>OnDisable</code>, 자기 자신과 다른 컴포넌트의 <code>enabled</code> 읽기·단순 대입을 추가했습니다. 작성 시 disabled인 컴포넌트도 플래그와 참조를 저장·재열기·역변환합니다. GameObject 자체는 활성 상태여야 하며, <code>SetActive</code>와 비활성 GameObject 계층은 아직 지원하지 않습니다.</p><p>활성 GameObject의 disabled 컴포넌트는 Awake를 실행하고 첫 활성화까지 OnEnable·Start·Update를 보류합니다. enabled의 실제 값이 바뀔 때 OnEnable/OnDisable을 동기 호출하고, 재활성화한 프레임의 Update를 보류합니다. Start는 한 번만 실행합니다. 컴포넌트 Destroy 호출은 즉시 enabled를 false로 바꾸고 OnDisable을 호출하며, OnDestroy와 실제 제거는 업데이트 이후에 실행합니다.</p><p>반복 false/true 대입, Start 전 비활성화, 비활성 작성 상태의 첫 활성화, 같은 프레임 false→true와 삭제를 원본 Unity의 이벤트 기록·프레임별 Update 횟수와 대조합니다. 이 기능은 네이티브 Actor 경로를 사용합니다. 임의 생성 시점·재진입·전체 실행 순서와 비활성 GameObject 동등성은 별도 검증 범위입니다.</p>'''
if addition not in s:s=s.replace(marker,addition+marker)
p.write_text(s,encoding='utf-8');print('RUNTIME_ENABLE_GUIDE_UPDATED')
