"""Build the report from completed engine execution evidence, never from task estimates."""
import html,json,re
from pathlib import Path
from datetime import datetime,timezone
root=Path(__file__).resolve().parent.parent;art=root/'artifacts'
forward='ui-sprite-tests/RP-6c891568180e';reverse='ui-sprite-reverse/RP-6b22c67c08e3'
def read(file):return json.loads((art/file).read_text(encoding='utf-8-sig'))
evidence=[('22개 객체 왕복 데이터',reverse+'/roundtrip-data-verification.json'),('PNG 픽셀·공유 참조',reverse+'/resource-pixel-verification.json'),('원본 Unity ↔ Unreal 250프레임',forward+'/ExtendedRuntime-250/verification.json'),('복원 Unity 250프레임',forward+'/ExtendedRuntime-250-Restored/verification.json'),('실제 Windows 게임 실행','packaging-tests/UE58Sprite/PackagedRuntime/verification.json'),('원본 60개 파일 보존','continued-original-preservation.json'),('지원 밖 overrideSprite 상태 실제 차단',forward+'/Source/override-sprite-rejection.json'),('누락 리소스·잘못된 UV·추가 Pawn 차단','runtime-comparison-guards/verification.json')]
checks=int(re.search(r'(\d+) checks passed',(art/'continued-checks.log').read_text(encoding='utf-8-sig')).group(1))
summary={'updatedUtc':datetime.now(timezone.utc).isoformat(),'checks':checks,'projectPortComplete':False,'renderingEquivalent':False,'evidence':{}}
for label,file in evidence:
 data=read(file);assert data['status']=='PASS',(label,data)
 summary['evidence'][label]=data
assert summary['evidence']['실제 Windows 게임 실행']['packagedUITexturesVerified']==4
for file,label in [('ui-sprite-tests-55-final/RP-0534bc086297/ExtendedRuntime-250/verification.json','Unreal 5.5.4 새 22개 객체 실행'),('ui-sprite-tests-55-final/RP-0534bc086297/ExtendedRuntime-250-Restored/verification.json','Unreal 5.5.4 복원 Unity 실행'),('runtime-deployment-verification.json','최신 앱 실제 실행·배포 모듈 검사'),('runtime-archive-verification.json','최신 ZIP 압축 해제 검사'),('runtime-report-browser-verification.json','최종 보고서·가이드·대응표 브라우저 검사')]:
 if (art/file).exists():
  data=read(file);assert data['status']=='PASS';evidence.append((label,file));summary['evidence'][label]=data
if (art/'ui-sprite-tests-55-final/RP-0534bc086297/ExtendedRuntime-250-Restored/verification.json').exists():
 reverse55=Path(read('ui-sprite-tests/reverse55-result.json')['RunDirectory']).relative_to(art).as_posix()
 for label,suffix in [('Unreal 5.5.4 왕복 22개 객체','roundtrip-data-verification.json'),('Unreal 5.5.4 왕복 PNG 픽셀','resource-pixel-verification.json')]:
  file=reverse55+'/'+suffix;data=read(file);assert data['status']=='PASS';evidence.append((label,file));summary['evidence'][label]=data
(art/'runtime-development-verification.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
def link(file,label):return '<a href="'+html.escape(file,quote=True)+'">'+html.escape(label)+'</a>'
rows=''.join('<tr><td>'+html.escape(label)+'</td><td>'+link(file,'검증 결과 열기')+'</td></tr>' for label,file in evidence)
body='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>EngineMigrator 런타임·이미지 포팅 결과</title><style>:root{color-scheme:dark}*{box-sizing:border-box}body{margin:40px auto;padding:0 24px;max-width:1100px;background:#211e1a;color:#f5efe5;font:15px/1.8 system-ui;overflow-wrap:anywhere}h1,h2,a,th{color:#f3ab73}.card,.notice{padding:20px;border:1px solid #514638;border-radius:12px;background:#302b25;margin:18px 0}.notice{border-left:4px solid #e46b35}table{width:100%;border-collapse:collapse}td,th{padding:12px;border-bottom:1px solid #514638;text-align:left}.table{overflow:auto}li{margin:8px 0}p{color:#c9bdad}</style><span>ENGINEMIGRATOR · 실제 실행 검증</span><h1>Windows 게임 실행과 UI 이미지 왕복 포팅</h1><div class="notice"><strong>검증한 범위는 실제 포팅·실행했습니다. 전체 게임 자동 포팅은 아직 미완료입니다.</strong><p>사용자 Unity 6000.0.83f1 기본 URP 프로젝트를 별도로 복사하고, 물리·애니메이션·UI·상속 스크립트 검증용 객체를 추가했습니다. 원본 60개 파일은 해시 대조로 보존을 확인했습니다.</p></div>'''
body+='<h2>실행·결과 열기</h2><ul>'+''.join('<li>'+link(file,label)+'</li>' for file,label in [('EngineMigrator-runtime-preview-20261019/EngineMigrator.App.exe','최신 프로그램 실행 파일'),('EngineMigrator-runtime-preview-20261019.zip','런타임 포함 ZIP — 폴더 전체를 압축 해제'),('EngineMigrator-runtime-preview-20261019/guide.html','HTML 사용·대입 가이드'),(reverse+'/runtime-porting-map.html','실제 자산·참조·콜백 대응표'),(forward+'/Target/PortTarget.uproject','Unreal 5.8.1 변환 프로젝트'),(reverse+'/Target/','Unity 왕복 복원 프로젝트')])+'</ul>'
body+=f'<div class="card"><strong>22개 객체 · 250프레임 실행 대조 · {checks}개 자체 검사</strong><ul><li>실제 Actor Blueprint의 상태·상속·부모 함수 호출·종료 이벤트와 버튼 콜백을 실행했습니다.</li><li>PNG 1개를 공유하는 Simple Sprite 3곳과 RawImage 1곳을 변환했습니다. 사각 영역·피벗·PPU·UV·공유 참조와 복원 이미지 픽셀을 대조했습니다.</li><li>게임 모듈·Game/Editor 타깃을 생성해 Runtime UI 모듈이 패키지에 포함되도록 수정했습니다. 테스트 전용 프로브로 Windows 실행 파일을 구동했습니다.</li><li>원본에 없는 DefaultPawn의 자동 생성을 막는 맵 전용 GameMode를 저장하고 다시 읽어 검증했습니다.</li></ul></div>'
body+='<h2>검증 조건과 실제 차이</h2><p>실행 비교는 Unity에서 기록한 deltaTime 입력을 Unreal에 동일하게 공급한 250프레임 검사입니다. 게임 시작 전 Unreal 부트스트랩 1프레임은 제외했습니다. 버튼은 delegate를 두 번 직접 호출했습니다. 실제 마우스 입력, 자연 FPS, 화면 렌더링 외형의 일치는 검증하지 않았습니다.</p><p>기본 물리의 자유낙하 차이는 약 0.000002m 이내였습니다. 접촉 회복 중 전체 궤적은 최대 약 0.20m 차이가 있어 동일하지 않습니다. 위치·크기 Legacy Animation과 종료 상태 대조는 통과했습니다.</p>'
body+='<h2>미완료 범위</h2><p>임의 Actor Blueprint 노드·엔진 API·잠재/비동기 동작, 모든 보조 스크립트·외부 패키지, FixedUpdate·충돌 이벤트·조인트, Animator·스켈레탈·회전 애니메이션, 복잡한 UI·TMP·자동 레이아웃·입력, Sprite 패킹·트리밍·슬라이스, 다중 장면·프리팹, 모든 엔진 버전 조합은 미완료입니다. 로컬 LLM은 아직 연결되지 않았습니다.</p>'
body+='<h2>검증 근거</h2><div class="table"><table><tr><th>대조·실행 항목</th><th>실제 결과</th></tr>'+rows+'</table></div><p>XERJ: 미사용 — 연결 도구가 없어 rg·설치된 엔진 소스·실행 로그로 검증했습니다.</p></html>'
(art/'development-report.html').write_text(body,encoding='utf-8')
print('RUNTIME_REPORT_WRITTEN')
