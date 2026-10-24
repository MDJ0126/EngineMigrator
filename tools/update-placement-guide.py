from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html'
s=p.read_text(encoding='utf-8')
s=s.replace('프리팹 간 에셋 의존성, Component 복제, 부모/위치/회전을 받는 오버로드, 지연 삭제는 아직 미지원입니다.','프리팹 간 에셋 의존성, Component 복제, 지연 삭제는 아직 미지원입니다.')
marker='<h3>런타임 객체 복제·삭제와 장면 전환</h3>'
addition='''<h3>생성 위치·회전·부모와 참조</h3><p><code>Instantiate(source, position, rotation)</code>, <code>Instantiate(source, parent)</code>, <code>Instantiate(source, parent, worldPositionStays)</code>, <code>Instantiate(source, position, rotation, parent)</code>를 GameObject 원본에 지원합니다. 대상 엔진에서는 계층을 지연 생성하고 객체 참조와 최종 좌표·부모를 연결한 뒤 생명주기를 시작합니다. 위치·회전 인수는 Unity 좌표계를 변환하며, 부모 지정 시 로컬/월드 유지 규칙을 적용합니다.</p><p>직렬화된 <code>Transform</code> 장면 참조와 <code>Quaternion</code> 필드를 양방향으로 복원합니다. 지원 스크립트의 <code>transform.parent</code>, <code>transform.rotation</code>, <code>transform.localRotation</code> 읽기와 Quaternion 생성·identity를 추가했습니다. Transform 필드는 Unreal Actor 포인터로 연결하고 Unity 복원 시 해당 GameObject의 Transform으로 되돌립니다. 프리팹 에셋의 Transform 참조, 임의 Component 복제와 전체 Transform API는 아직 미지원입니다.</p><p>장면 내부 런타임 프로토타입과 참조된 프리팹 정의의 계층·좌표·직렬화 상태·메시/재질 연결이 독립적으로 수정되면 역변환을 차단합니다. 동기화 자동 전파는 아직 제공하지 않으며 원본에서 다시 임포트해야 합니다. 이전 결과에 검사 기준이 없으면 재임포트가 필요합니다. 공유 자산 자체의 모든 편집 속성을 감시하는 검사는 아닙니다.</p>'''
if addition not in s:s=s.replace(marker,addition+marker)
p.write_text(s,encoding='utf-8')
print('PLACEMENT_GUIDE_UPDATED')
