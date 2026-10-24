from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html';s=p.read_text(encoding='utf-8')
marker='<h3>물리 프리팹의 런타임 생성과 활성 상태</h3>'
addition='''<h3>런타임 Transform 애니메이션 프리팹</h3><p>Legacy Animation의 지원 Transform 클립을 포함한 프리팹을 생성할 때 재생 Actor도 함께 복제합니다. 각 LevelSequence 바인딩은 원래 프로토타입이 아닌 새 객체 계층으로 연결하고, 인스턴스별 시계로 평가합니다. 프로토타입은 재생하지 않습니다. 실제 테스트는 Loop·Once·ClampForever, 비활성 프리팹의 첫 활성화, 재활성화, 비활성 Animation과 재생 중 객체 복제를 포함합니다.</p><p>Unity 기준 실행에서는 생성 프레임의 애니메이션 평가가 0초 자세부터 시작했습니다. 재생 중 객체를 복제하거나 비활성 객체를 다시 활성화해도 내부 재생 시간은 처음부터 시작하지만, 호출 반환 시점의 Transform과 공개 스크립트 필드는 복사됩니다. Update에서 읽은 자세와 이후 프레임 관찰 자세를 모두 대조합니다. Once 종료 뒤 마지막 자세 유지도 확인했습니다. Unity의 Once/Clamp 열거형 별칭은 내보낼 때 Once로 정규화합니다.</p><p>객체 전체 삭제 시 소유 재생 Actor를 함께 제거합니다. 장면 전환에서는 월드 정리 콜백으로 재생 시계의 델리게이트를 해제합니다. 재생 중 객체를 남긴 채 다음 장면으로 이동하는 실제 실행도 검사합니다. 정의·프로토타입의 실제 채널과 바인딩을 저장·재열기 후 검증하며, 한쪽만 편집한 경우에는 동기화를 위해 재가져오기가 필요합니다.</p><p>검증 대상은 지원되는 Legacy Transform 자동 재생입니다. Animator 상태 기계·스켈레탈 애니메이션·AnimationEvent·블렌딩·임의 재생 제어 API·일반 애니메이션 컴포넌트 조회는 별도 미지원 범위가 남아 있습니다. 물리와 같은 Transform을 동시에 제어하는 조합은 기존 차단을 유지합니다. 런타임 UI 프리팹은 아직 구현 중입니다.</p>'''
assert marker in s
if addition not in s:s=s.replace(marker,addition+marker)
s=s.replace('런타임 애니메이션·UI 프리팹 실행 연결은 별도 구현 항목입니다.','런타임 Transform 애니메이션 프리팹은 위 절의 범위에서 지원합니다. UI 프리팹 실행 연결은 별도 구현 항목입니다.')
s=s.replace('애니메이션·UI·카메라·라이트·Volume을 포함한 런타임 프리팹은 사전 검사를 유지합니다.','지원 Transform 애니메이션은 위 절의 범위를 참고하십시오. UI·카메라·라이트·Volume을 포함한 런타임 프리팹은 사전 검사를 유지합니다.')
s=s.replace('애니메이션·UI 구성, 중첩·Variant 및 임의 API는 별도 미지원 범위입니다.','Transform 애니메이션은 위 절의 범위에서 지원합니다. UI 구성, 중첩·Variant 및 임의 API는 별도 미지원 범위입니다.')
isolation='''<p>패키지 실행에서는 에디터와 달리 숨겨진 프로토타입까지 재생되는 차이를 발견했습니다. 프로토타입 클래스에서는 자동 재생 이벤트 그래프를 제거하고 평가 대상도 보조 Actor로 격리합니다. 복제본은 생성 후 실제 새 계층으로 다시 연결해 자체 시계로 평가합니다. 에디터 검사만으로 통과 처리하지 않고 패키지의 프로토타입 자세도 매 시점 대조합니다.</p>'''
if isolation not in s:s=s.replace('<h3>물리 프리팹의 런타임 생성과 활성 상태</h3>',isolation+'<h3>물리 프리팹의 런타임 생성과 활성 상태</h3>')
p.write_text(s,encoding='utf-8');print('RUNTIME_ANIMATION_PREFAB_GUIDE_UPDATED')
