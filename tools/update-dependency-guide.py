from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html';s=p.read_text(encoding='utf-8')
marker='<h3>비활성 프리팹과 OnDisable 내부 복제</h3>'
addition='''<h3>프리팹 정의 간 연쇄·순환 참조</h3><p>사용자 스크립트의 GameObject 필드가 다른 기본 프리팹 자산의 루트를 가리키는 경우를 지원합니다. 장면에서 A만 참조하더라도 A→B→C→A처럼 연결된 정의를 수집하며, 방문한 자산은 한 번만 구성합니다. 대상이 없는 참조는 가져오기 전에 차단합니다. 중첩 프리팹이나 Variant와는 별개의 자산 필드 참조입니다.</p><p>Unreal에서는 같은 장면 안의 비실행 프로토타입을 서로 연결합니다. 생성된 객체에서 다음 프리팹을 조회·생성하고, 순환 경로를 돌아 같은 유형을 다시 생성해도 내부 루트·컴포넌트 참조는 새 복제본으로 연결합니다. Unity가 자기 자신을 가리키는 필드를 저장할 때 내부 루트 참조로 직렬화하는 경우에는 그 내부 참조 의미를 보존합니다. 실제 자산 참조와 내부 참조는 내보낸 종류에 따라 구분합니다.</p><p>Unity 복원은 먼저 프리팹 자산의 경로와 식별자를 등록한 뒤 내용을 채우므로, 매니페스트 순서와 무관하게 앞방향·순환 참조를 복원합니다. 복원 후 실제 실행과 재내보내기로 참조·상태를 대조합니다. 비교식 안의 상태 변경 없는 사용자 컴포넌트 GetComponent 조회를 지원하며 AddComponent 같은 상태 변경 호출의 평가 순서 제한은 유지합니다. 런타임 프리팹의 일반 물리·애니메이션·UI 구성, 중첩·Variant 및 임의 API는 별도 미지원 범위입니다.</p>'''
assert marker in s
if addition not in s:s=s.replace(marker,addition+marker)
s=s.replace('프리팹 간 에셋 의존성, Component 복제, 지연 삭제는 아직 미지원입니다.','기본 프리팹 루트 간 GameObject 자산 필드 참조는 위 절의 범위에서 지원합니다. Component 자체 복제와 지연 시간 인수가 있는 삭제는 아직 미지원입니다.')
p.write_text(s,encoding='utf-8');print('RUNTIME_DEPENDENCY_GUIDE_UPDATED')
