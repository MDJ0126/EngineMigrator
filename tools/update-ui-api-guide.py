from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
assert json.loads((root/'artifacts/runtime-ui-api-tests/unreal-verification.json').read_text(encoding='utf-8-sig'))['status']=='PASS'
p=root/'docs/guide.html';s=p.read_text(encoding='utf-8')
section='''<section id="ui-component-api"><h2>UI 컴포넌트 API와 씬 UI 복제</h2><p>GameObject 또는 사용자 MonoBehaviour의 GetComponent&lt;Canvas&gt;(), GetComponent&lt;Button&gt;()와 Canvas.enabled, Button.enabled, Button.interactable의 읽기·단순 대입을 처리합니다. Canvas/Button 직렬화 필드는 소유 객체를 통해 실제 컴포넌트로 복원하고, 런타임 복제 시 내부 참조를 새 계층에 연결합니다.</p><p>Canvas를 꺼도 GameObject와 스크립트의 활성 상태는 유지하고 UI 표시만 바꿉니다. Button.enabled와 interactable은 별도 상태로 보존하며 실제 UMG 버튼에는 두 조건을 함께 적용합니다. 처음부터 꺼진 Canvas를 스크립트로 켜는 동작도 포함합니다.</p><p>씬에 배치된 UI도 표시 관리 Actor를 사용합니다. 복제 범위 안의 콜백 대상은 새 인스턴스로 연결하고, 범위 밖의 기존 컴포넌트는 원래 대상을 유지합니다. 원본 UI를 삭제한 뒤 복제본에서 외부 콜백이 계속 호출되는 것을 실행 대조합니다.</p><p>검증은 기본 Overlay UI와 지원 상태 API 범위입니다. UI 컴포넌트 AddComponent/Destroy, 런타임 AddListener, 일반 문자열·색·텍스트 API와 RectTransform 전체 API는 미완료입니다. UI Transform 좌표·배율의 프록시 차이는 여전히 미지원이며, 이번 상태 API 검증을 Transform 또는 렌더링 동등성으로 해석하지 않습니다.</p></section>'''
if 'id="ui-component-api"' not in s:s=s.replace('</main>',section+'</main>')
s=s.replace('런타임 AddListener·고정 인수·일반 UI API는 지원 범위가 아닙니다.','런타임 AddListener·고정 인수와 위 절에 없는 UI API는 지원 범위가 아닙니다. Canvas/Button의 지원 상태 API는 UI 컴포넌트 절을 참고하세요.')
p.write_text(s,encoding='utf-8');print('UI_API_GUIDE_UPDATED')
