from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
check=json.loads((root/'artifacts/runtime-ui-prefab-tests/unreal-verification.json').read_text(encoding='utf-8-sig'))
assert check['status']=='PASS' and len(check['checkpoints'])==20
path=root/'docs/guide.html';text=path.read_text(encoding='utf-8')
section='''<section id="runtime-ui-prefab"><h2>런타임 UI 프리팹과 콜백 연결</h2><p>지원 Overlay uGUI 프리팹을 Instantiate하면 Canvas의 UMG 위젯과 표시 관리 Actor를 함께 생성합니다. 버튼의 대상은 장면 전체 태그 검색 대신 이번에 복제한 객체·컴포넌트 집합으로 연결합니다. 여러 인스턴스와 실행 중 재복제본의 콜백이 서로 섞이지 않도록 처리합니다.</p><ul><li>루트·자식 컴포넌트의 인수 없는 public void 영구 콜백, 등록 순서와 Off 상태를 보존합니다. 런타임 AddListener·고정 인수·일반 UI API는 지원 범위가 아닙니다.</li><li>비활성 프리팹 첫 활성화, 루트와 버튼 GameObject의 SetActive, Canvas가 처음부터 disabled인 프리팹, 콜백 없는 UI를 검증합니다. 비활성화 동안에는 기존 위젯을 숨기고 상태를 보존합니다.</li><li>객체 전체 삭제 시 해당 UI 위젯도 뷰포트에서 제거하고, 장면 전환 시 남은 표시 관리 객체와 참조를 정리합니다. 숨겨진 프리팹 프로토타입은 위젯을 만들지 않습니다.</li><li>정의 맵 또는 런타임 프로토타입의 UMG 콜백이 별도로 변경되면 역변환 전 보호 검사에서 차단합니다. 정의와 인스턴스의 변경 내용을 조정한 뒤 다시 가져와야 합니다.</li></ul><p>검증 방식: Unity의 실제 Button.onClick.Invoke와 Unreal의 실제 UButton.OnClicked Broadcast를 같은 시점에 실행하고, 자기 인스턴스의 상태·순서·생명주기와 뷰포트 위젯 수를 대조합니다. 마우스 입력·클릭 좌표·렌더링 동일성 검증을 대신하지 않습니다.</p><p>범위: 기본 Overlay Canvas, 단위 배율, 기존 지원 Image/RawImage/Text/Button 레이아웃입니다. 중첩 Canvas·임의 UI 회전/배율·버튼 시각 전환·외부 폰트·UI Transform API의 화면 좌표·배율 동등성은 미지원입니다. 실제 실행에서 Canvas 루트 배율이 Unity 1과 현재 Unreal 프록시 0으로 다른 것을 확인했습니다. Unity가 프리팹 미리보기의 Overlay 루트에 기록하는 0 스케일은 실행 관찰에 근거해 사전 검사에서 구분합니다. 일반 0 스케일 UI 지원을 뜻하지 않습니다.</p></section>'''
if 'id="runtime-ui-prefab"' not in text:
 text=text.replace('</main>',section+'</main>') if '</main>' in text else text.replace('</body>',section+'</body>')
text=text.replace('런타임 UI 프리팹 실행 연결과 일반 엔진 컴포넌트','지원 범위 밖 런타임 UI 동작과 일반 엔진 컴포넌트')
text=text.replace('UI Transform API의 화면 좌표 동등성은 미지원 또는 미검증입니다.','UI Transform API의 화면 좌표·배율 동등성은 미지원입니다. 실제 실행에서 Canvas 루트 배율이 Unity 1과 현재 Unreal 프록시 0으로 다른 것을 확인했습니다.')
path.write_text(text,encoding='utf-8');print('RUNTIME_UI_PREFAB_GUIDE_UPDATED')
