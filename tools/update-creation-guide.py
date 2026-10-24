from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html'
s=p.read_text(encoding='utf-8')
marker='<h3>생성 위치·회전·부모와 참조</h3>'
addition='''<h3>빈 객체 생성과 런타임 초기화</h3><p><code>new GameObject()</code>, <code>new GameObject("이름")</code>을 실제 Unreal Actor와 루트 SceneComponent로 생성합니다. 이름 생성자와 <code>object.name = "이름"</code>에는 문자열 상수만 지원하며 한글·따옴표·대소문자를 보존합니다. Unreal 내부 객체명은 엔진이 고유하게 부여하고, 원본 이름은 별도 런타임 이름으로 보존합니다. 이름 읽기·문자열 변수/계산·NUL 이름은 아직 지원하지 않습니다.</p><p>객체의 <code>transform.position</code>, <code>localPosition</code>, <code>rotation</code>, <code>localRotation</code>, <code>localScale</code> 읽기·단순 대입, <code>parent</code> 읽기·대입과 <code>SetParent(parent)</code>·<code>SetParent(parent, worldPositionStays)</code>를 추가했습니다. 다른 객체의 Transform에도 적용합니다. 생성된 빈 객체는 메시나 사용자 스크립트를 자동으로 붙이지 않습니다. <code>AddComponent</code>·<code>GetComponent</code>의 일반 변환과 Component 복제는 미지원입니다.</p><p>원본 Unity, Unreal 게임 월드, 복원 Unity와 패키징한 Windows 실행 파일에서 12개 시점을 대조해 빈 객체 생성·한글 이름 변경·600자 한글 이름·대소문자 구분·부모 연결/해제·좌표/회전/크기·복제·계층 삭제·재생성과 두 번의 장면 전환이 통과했습니다. 이번 부모 검증은 회전된 부모와 균일 크기를 사용하며 비균일 크기·회전의 전단 변형은 미검증입니다.</p>'''
if addition not in s:s=s.replace(marker,addition+marker)
p.write_text(s,encoding='utf-8');print('RUNTIME_CREATION_GUIDE_UPDATED')
