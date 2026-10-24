"""Render only mappings produced by native save/reload verification."""
import html,json,sys
from pathlib import Path
source,output=map(Path,sys.argv[1:3]);data=json.loads(source.read_text(encoding="utf-8-sig"))
assert data["savedAndReloaded"] and data["referencesVerified"], "Native verification must pass first"
escape=lambda value:html.escape(str(value))
rows="".join("<tr>"+"".join("<td>"+escape(value)+"</td>" for value in (b["sourceId"],b["target"],b["kind"],", ".join(b["references"])))+"</tr>" for b in data["bindings"])
output.write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>실제 포팅 대입·참조 대응표</title><style>body{background:#211e1a;color:#f5efe5;font:15px/1.8 system-ui;max-width:1200px;margin:40px auto;padding:0 24px;overflow-wrap:anywhere}h1,th{color:#f3ab73}.table{overflow:auto}table{width:100%;border-collapse:collapse}td,th{padding:12px;text-align:left;border-bottom:1px solid #514638}pre{white-space:pre-wrap;background:#302b25;padding:20px}</style><h1>실제 포팅 대입·참조 대응표</h1><p>대상 엔진에서 저장 후 다시 연 객체·에셋·참조를 검증한 결과입니다. 버튼 호출 순서와 비활성 리스너도 개별 행으로 기록합니다. 전체 게임·화면 렌더링 동등성의 완료 판정은 아닙니다.</p><div class="table"><table><tr><th>원본 ID</th><th>실제 대상</th><th>유형·상태</th><th>참조 ID</th></tr>'''+rows+"</table></div><h2>실제 검증 데이터</h2><pre>"+escape(json.dumps(data,ensure_ascii=False,indent=2))+"</pre></html>",encoding="utf-8")
print(f"MAPPING_REPORT_PASS: {len(data['bindings'])} verified bindings")
