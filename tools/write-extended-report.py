import html,json,re
from datetime import datetime,timezone
from pathlib import Path
root=Path(__file__).resolve().parent.parent;art=root/'artifacts'
forward='extended-project-tests/RP-1b895bf1891b';reverse='extended-project-tests/RP-3961f64137b9'
def read(path):return json.loads((art/path).read_text(encoding='utf-8-sig'))
runtime=read(forward+'/ExtendedRuntime-250/verification.json');restored=read(forward+'/ExtendedRuntime-250-Restored/verification.json');data=read(reverse+'/roundtrip-data-verification.json')
assert runtime['status']==restored['status']==data['status']=='PASS'
checks=int(re.search(r'(\d+) checks passed', (art/'extensions-checks-final.log').read_text(encoding='utf-8-sig')).group(1))
native55='extended-project-tests/RP-bd7c364e6704';runtime55=art/native55/'ExtendedRuntime-250/verification.json'
status55='실제 실행 대조 진행 중'
if runtime55.exists():
    verified55=read(native55+'/ExtendedRuntime-250/verification.json')
    if verified55['status']=='PASS':status55='새 프로젝트 자동 변환·저장·250프레임 실행 대조 PASS'
reverse55='extended-project-tests/RP-bf11cb0a03f4'
restored55=art/native55/'ExtendedRuntime-250-Restored/verification.json'
if restored55.exists() and read(native55+'/ExtendedRuntime-250-Restored/verification.json')['status']=='PASS' and read(reverse55+'/roundtrip-data-verification.json')['status']=='PASS':status55='18개 객체 양방향 데이터·원본/Unreal/복원 Unity 250프레임 실행 PASS'
summary={'updatedUtc':datetime.now(timezone.utc).isoformat(),'sourceProjectPreserved':read('extended-project-tests/original-preservation-check.json'),'roundtrip':data,'sourceAndUnrealRuntime':runtime,'restoredRuntime':restored,'checks':checks,'unreal55':status55,'projectPortComplete':False}
if (art/'extensions-deployment-verification.json').exists():summary['deployment']=read('extensions-deployment-verification.json')
(art/'extended-development-verification.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
link=lambda target,label:'<a href="'+html.escape(target,quote=True)+'">'+html.escape(label)+'</a>'
evidence=[('18개 객체 왕복 데이터',reverse+'/roundtrip-data-verification.json'),('Unity ↔ Unreal 250프레임',forward+'/ExtendedRuntime-250/verification.json'),('복원 Unity ↔ Unreal 250프레임',forward+'/ExtendedRuntime-250-Restored/verification.json'),('비소유 Actor Blueprint 역변환·실행','authored-blueprint-test/RP-78c2fec248e3/ExtendedRuntime-250-Restored/verification.json'),('원본 60개 파일 해시 보존','extended-project-tests/original-preservation-check.json'),(str(checks)+'개 자체 코드 검사','extensions-checks-final.log'),('배포 GUI 스모크 검사','studio-checks.txt')]
if runtime55.exists() and status55.endswith('PASS'):evidence.append(('Unreal 5.5.4 추가 기능 실행',native55+'/ExtendedRuntime-250/verification.json'))
if restored55.exists() and status55.endswith('PASS'):evidence.extend([('Unreal 5.5.4 왕복 데이터',reverse55+'/roundtrip-data-verification.json'),('Unreal 5.5.4 복원 Unity 실행',native55+'/ExtendedRuntime-250-Restored/verification.json')])
for label,file in [('배포 ZIP CRC·압축 해제','extensions-archive-verification.json'),('한글 경로 실제 실행·모듈 해시','extensions-deployment-verification.json'),('HTML 데스크톱·모바일 검사','extensions-report-browser-verification.json')]:
    if (art/file).exists():evidence.append((label,file))
rows=''.join('<tr><td>'+html.escape(name)+'</td><td>'+link(path,'실제 근거 열기')+'</td></tr>' for name,path in evidence)
body='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>EngineMigrator 실제 포팅 개발·검증</title><style>:root{color-scheme:dark}*{box-sizing:border-box}body{background:#211e1a;color:#f5efe5;font:15px/1.8 system-ui;max-width:1100px;margin:40px auto;padding:0 24px;overflow-wrap:anywhere}h1,h2,a,th{color:#f3ab73}p{color:#c0b3a1}.notice,.card{background:#302b25;border:1px solid #514638;border-radius:12px;padding:20px}.notice{border-left:4px solid #e46b35}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:16px;margin:24px 0}.metric{font-size:32px;color:#f3ab73;font-weight:700}table{width:100%;border-collapse:collapse}td,th{padding:12px;text-align:left;border-bottom:1px solid #514638}.table{overflow:auto}li{margin:8px 0}code{overflow-wrap:anywhere}</style><span>ENGINEMIGRATOR · 2026-10-19</span><h1>실제 프로젝트 포팅 확장 결과</h1><div class="notice"><strong>실제 에셋·코드·참조와 런타임 연결을 구현했습니다. 전체 게임 자동 포팅은 아직 미완료입니다.</strong><p>사용자 Unity 프로젝트는 기본 URP 장면입니다. 원본을 보존한 별도 복사본에 검증용 물리·애니메이션·UI·스크립트를 추가했습니다. 지원 범위의 통과를 모든 엔진 기능·버전의 완료로 표시하지 않습니다.</p></div>'''
body+=f'<div class="cards"><div class="card"><div class="metric">18</div>실제 장면 객체 왕복 대조</div><div class="card"><div class="metric">250</div>원본·Unreal·복원 Unity 실행 프레임</div><div class="card"><div class="metric">{checks}</div>자체 코드 검사 통과</div></div>'
body+='<h2>바로 사용하기</h2><ul>'+''.join('<li>'+link(path,label)+'</li>' for path,label in [('EngineMigrator-extended-preview-20261019/EngineMigrator.App.exe','최신 실행 파일'),('EngineMigrator-extended-preview-20261019.zip','런타임 포함 ZIP — 폴더 전체를 압축 해제하세요'),('EngineMigrator-extended-preview-20261019/guide.html','HTML 사용·대입 가이드'),(reverse+'/extended-porting-map.html','실제 39개 참조·콜백 대응표'),(reverse+'/Target/','왕복 복원 Unity 프로젝트 폴더'),(forward+'/Target/PortTarget.uproject','실제 Unreal 5.8.1 결과 프로젝트')])+'</ul>'
body+='''<h2>구현한 변환</h2><ul><li>실제 Actor Blueprint의 상태·대입·수식·분기·상속·부모 호출·BeginPlay/Tick/EndPlay와 공개 Custom Event. 지원 문법의 기존 비소유 Blueprint도 역추출합니다.</li><li>Box/Sphere/Capsule, Rigidbody → 실제 ShapeComponent·Chaos 강체. 질량·중력·감쇠·축 잠금·트리거·공유 PhysicalMaterial과 대상 참조를 연결합니다.</li><li>Legacy Animation 위치·크기 곡선 → 실제 LevelSequence. 키·접선·반복·대상 바인딩을 복원하고 실행 위치·크기를 대조했습니다.</li><li>Overlay uGUI → 실제 Widget Blueprint·위젯 트리. 인수 없는 Button.onClick을 런타임 Actor 이벤트에 연결합니다. 활성 호출 순서와 비활성 리스너를 보존해 두 번 클릭 후 원본·Unreal·복원 프로젝트의 실행 결과가 1212로 일치했습니다.</li></ul><h2>확인된 차이와 남은 구현</h2><p>자유낙하 차이는 약 0.000002m 이내였지만 접촉 회복 중 최대 약 0.20m 차이가 있었습니다. 물리 전체 궤적 동등성은 false입니다. 최종 정지·침투 경계는 별도 조건으로 검증했습니다.</p><p>Animator·스켈레탈·애니메이션 Blueprint, 임의 그래프/API·지연/비동기·충돌 콜백/관절, Sprite·TMP·레이아웃·입력 시스템, 렌더링·마우스 입력·패키징 검증은 남아 있습니다. 기본 폰트는 Unreal 폰트로 대체됩니다. 다른 엔진 버전은 미확인입니다.</p>'''
body+='<p>Unreal 5.5.4: '+html.escape(status55)+'</p><h2>실행 근거</h2><div class="table"><table><tr><th>검증</th><th>결과 파일</th></tr>'+rows+'</table></div><p>원본을 직접 대상으로 지정한 역변환 실행은 자동 승인 검토가 원본 변경 위험으로 차단했습니다. 이후 검증한 별도 템플릿 복사본만 대상으로 사용했습니다.</p><p>XERJ: 미사용 — 연결 도구가 없어 rg·엔진 소스·실행 로그로 검증했습니다.</p>'
(art/'development-report.html').write_text(body,encoding='utf-8');print('EXTENDED_REPORT_WRITTEN')
