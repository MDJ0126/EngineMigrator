"""Record the bounded, actually executed Owner-field roundtrip and adapter checks."""
from pathlib import Path
from datetime import datetime,timezone,timedelta
import hashlib,json,runpy,re
from object_equality_evidence import require,validate_receipt,sha
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/owner-field-check'
runpy.run_path(str(root/'tools/verify-owner-field-probe.py'))
require('305 checks passed.' in (home/'internal-checks.log').read_text(encoding='utf-8-sig'),'Internal regressions missing')
adapter=json.loads((home/'adapter-verification.json').read_text(encoding='utf-8-sig'))
require(adapter['status']=='PASS' and all(adapter[k] is True for k in ('sourceFieldPreserved','nativeOwnerUnchanged','sceneReferenceReloadChecked','exportSourceReferencePreserved','prototypeSnapshotMapped')),'Adapter assertions missing')
core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
receipts=[]
for path in core.glob('engine-preflight-*.json'):
 data=json.loads(path.read_text(encoding='utf-8-sig'))
 try:validate_receipt(data,'Unreal',home/'adapter-unreal.log',root=root)
 except (RuntimeError,KeyError,ValueError,TypeError,IndexError):continue
 receipts.append((data['finishedUtc'],path))
require(bool(receipts),'Successful audited adapter receipt missing')
public=json.loads((root/'artifacts/ui-equality-check/public-pipeline-verification.json').read_text())
require(public.get('status')=='PUBLIC_PIPELINE_MATCHES_ENGINE_TESTED_HEADERS' and public.get('headers')==49 and public.get('restored')==49,'Existing public pipeline regression missing')
originals=json.loads((root/'artifacts/user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
for name,digest in originals.items():require(sha(Path(name)).lower()==digest.lower(),'User original changed: '+name)
stamp=datetime.now(timezone(timedelta(hours=9))).isoformat()
section=f'''<section id="owner-field-actual"><h2>Owner 필드 충돌 자동 대응 — 구현·실제 왕복 검증 완료</h2><p>{stamp}. 기존 Unreal 빌드 로그에서 사용자 C# Owner 필드가 AActor.Owner와 충돌해 UHT가 거부한 것을 확인했습니다. 이제 C# 필드명은 Owner로 보존하고, 생성 C++의 필드와 참조 접근만 EM_Field_Owner로 대응합니다. 인수·지역 변수의 이름은 구분하며, 상속된 필드와 다른 컴포넌트의 필드 읽기·쓰기도 대응합니다.</p><p>Unity 6000.0.83f1 원본 → Unreal 5.8 실제 게임 실행 → 복원 Unity를 새로 실행해 삭제·복제 7프레임의 17개 값을 대조했습니다. Unreal 실제 어댑터 실행에서는 장면 참조 가져오기·참조 검증·내보내기·프리팹 상태 스냅샷의 Owner 키가 유지되고, 기본 AActor.Owner가 변경되지 않는 것을 확인했습니다. 내부 검사 305개가 통과했습니다.</p><p>기존 49개 스크립트의 생성 헤더와 복원 C#은 이전 실제 검증본과 동일한지 대조했습니다. 해당 전체 프로젝트의 엔진 실행을 이번 단계에서 다시 수행했다는 뜻은 아닙니다. 사용자 원본 {len(originals)}개 파일은 해시가 일치합니다. 이 단계는 Owner 필드 충돌에 대한 지원이며, 모든 Unreal 예약 필드·모든 엔진 버전·전체 프로젝트 포팅 완료를 의미하지 않습니다.</p></section>'''
for path in (root/'docs/guide.html',root/'artifacts/development-report.html'):
 text=path.read_text(encoding='utf-8-sig');text=re.sub(r'<section id="owner-field-actual">.*?</section>','',text,flags=re.S)
 require('</main>' in text,'HTML anchor missing');path.write_text(text.replace('</main>',section+'</main>'),encoding='utf-8')
record={'status':'OWNER_FIELD_ACTUAL_ROUNDTRIP_VERIFIED_AWAITING_RELEASE','goalComplete':False,'recordedKst':stamp,'internalChecks':305,'originalFilesUnchanged':len(originals),'adapter':adapter,'adapterReceipt':str(sorted(receipts)[-1][1]),'roundtrip':json.loads((home/'destruction-verification.json').read_text()),'publicPipeline':public,'sourceHashes':{str(p.relative_to(root)):sha(p) for p in (root/'src/EngineMigrator.Core/ActorScripts.cs',root/'adapters/unreal/script_components.py',root/'adapters/unreal/runtime_prototype_guard.py')},'xerj':'Unavailable; rg fallback'}
(home/'checkpoint.json').write_text(json.dumps(record,indent=2));print('OWNER_FIELD_STAGE_RECORDED')
