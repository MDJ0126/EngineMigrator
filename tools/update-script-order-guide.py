from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html';s=p.read_text(encoding='utf-8')
marker='<h3>GameObject 활성 상태와 비활성 계층</h3>'
addition='''<h3>스크립트 식별자와 실행 순서</h3><p>원본 MonoScript의 GUID와 Editor의 Script Execution Order 값을 내보내어 Unreal 프로젝트의 스크립트 정의에 보존합니다. C# 복원 시 같은 GUID와 executionOrder를 가진 .meta를 생성하며, 대상 자산이 이미 그 GUID를 사용하면 충돌로 중단합니다. 메타데이터가 없는 이전 패키지도 계속 복원할 수 있습니다.</p><p>Unreal의 사용자 스크립트 Update는 명시적 실행 순서가 낮은 Actor를 Tick 선행 조건으로 사용합니다. 런타임 AddComponent에도 해당 클래스의 순서 설정을 전달합니다. 서로 다른 객체의 Awake/Start 전체 호출 순서와 재진입 조합은 아직 실행 동등성이 검증되지 않았으며, 명시적 순서가 있는 원본을 내보낼 때 이 제한을 경고합니다.</p><p><a href="https://docs.unity3d.com/cn/6000.0/Manual/script-execution-order.html">Unity 6000.0 공식 문서</a>에 따르면 같은 값이나 기본값을 사용하는 스크립트 사이의 실행 순서는 비결정적입니다. 활성 계층 테스트는 제어 스크립트에 -100, 나머지 스크립트에 0을 명시합니다. 순서를 명시하기 전후의 원본 Unity 결과가 동일함을 대조한 뒤 포팅·역변환·패키징 실행을 비교합니다. 임의 기본 순서의 프레임별 결과를 동일하게 보존한다고 보장하지 않습니다.</p>'''
if addition not in s:s=s.replace(marker,addition+marker)
p.write_text(s,encoding='utf-8');print('SCRIPT_IDENTITY_ORDER_GUIDE_UPDATED')
