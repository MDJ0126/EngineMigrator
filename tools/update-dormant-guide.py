from pathlib import Path
p=Path(__file__).resolve().parents[1]/'docs/guide.html'
s=p.read_text(encoding='utf-8')
marker='<h3>GameObject 전체 삭제와 초기 프리팹 복제</h3>'
addition='''<h3>비활성 프리팹과 OnDisable 내부 복제</h3><p>메시·사용자 스크립트로 구성된 비활성 루트·자식 프리팹의 런타임 생성을 지원합니다. 물리·애니메이션·UI·카메라·라이트·Volume을 포함한 런타임 프리팹은 이 지원 범위에 포함되지 않으며 사전 검사를 유지합니다.</p><p>실제 Unity 기준으로 비활성 프리팹 생성 직후에는 Awake·OnEnable·Start가 실행되지 않습니다. 루트를 활성화하면 해당 루트의 Awake·OnEnable이 실행되고, 비활성 자식은 자신의 활성화까지 대기합니다. 이미 실행한 객체를 비활성 상태에서 복제하면 공개 필드 값은 복사하지만, 엔진 내부 초기화 상태는 새로 시작합니다. 따라서 공개 Awake·Start 카운터도 이후 콜백에서 다시 증가합니다.</p><p>GameObject 삭제 요청의 OnDisable 안에서 복제한 객체와, 계층 비활성화의 OnDisable 안에서 복제한 객체를 실제 실행으로 대조했습니다. 삭제 대기·계층 처리의 임시 태그를 복제본으로 전파하지 않고, 공개 필드와 내부 GameObject·컴포넌트 참조를 복제본으로 연결합니다. 검사한 재활성화·재삭제 흐름의 동등성이며 임의 깊이의 재귀 복제·모든 다른 콜백 조합은 미검증입니다.</p>'''
assert marker in s
if addition not in s:s=s.replace(marker,addition+marker)
s=s.replace('비활성 프리팹과 삭제·재진입 도중 복제의 모든 조합, 자산 자체 삭제·재부모화·부활 동작은 아직 미검증입니다.','비활성 프리팹·OnDisable 내부 복제의 검증 범위는 위 절을 참고하십시오. 그 외 모든 콜백 조합과 자산 자체 삭제·재부모화·부활 동작은 아직 미검증입니다.')
p.write_text(s,encoding='utf-8')
print('RUNTIME_DORMANT_GUIDE_UPDATED')
