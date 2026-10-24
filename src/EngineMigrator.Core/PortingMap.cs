using System.Net;
using System.Text;

namespace EngineMigrator.Core;

public sealed record PortingMapEntry(string SourcePath, string Kind, string Status, string? ActualOutputPath,
    string Rule, string BindingStatus, string RequiredWork, IReadOnlyList<CodeMapping> CodeMappings);

public static class PortingMap
{
    public static IReadOnlyList<PortingMapEntry> Build(InspectionReport report, IReadOnlyList<ConvertedFile> converted)
    {
        var codeByPath = converted.ToDictionary(f => f.SourcePath, StringComparer.OrdinalIgnoreCase);
        return report.Assets.Select(asset =>
        {
            if (codeByPath.TryGetValue(asset.Path, out var code))
                return new PortingMapEntry(asset.Path, asset.Kind, code.Generated ? "코드 생성 · 엔진 통합 미검증" : "차단 · 미지원",
                    code.OutputPath, string.Join("; ", code.Diagnostics),
                    "대상 프로젝트에 자동 배치·참조 연결하지 않았습니다.",
                    code.Generated ? "아래 심볼 대응과 호출 형태를 확인하고 대상 엔진에서 통합·컴파일·실행 검증이 필요합니다." : "파일별 차단 사유에 대응하는 변환기를 구현해야 합니다.", code.Mappings);
            return new PortingMapEntry(asset.Path, asset.Kind, "미변환 · 변환기 필요", null,
                "파일 조사만 수행했습니다. 원본 속성값·대상 에셋·참조 대응은 아직 생성되지 않았습니다.",
                "원본 ID/GUID, 연결 대상, 대상 ID와 속성 연결은 미확인입니다.", RequiredWork(asset.Path), []);
        }).ToArray();
    }

    private static string RequiredWork(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".meta" => "GUID와 import 설정을 추출하고 원본 에셋·대상 ID 대응표에 연결해야 합니다.",
        ".unity" or ".prefab" or ".umap" => "오브젝트 계층·컴포넌트·속성·스크립트·에셋 참조를 추출하고 대상 장면에 재구성해야 합니다.",
        ".mat" or ".shader" or ".shadergraph" => "머티리얼·셰이더의 실제 속성값과 텍스처 연결을 추출하고 대상 렌더링 구현으로 대응해야 합니다.",
        ".png" or ".jpg" or ".jpeg" or ".tga" or ".exr" or ".psd" => "이미지 데이터와 import 속성을 읽고 대상 텍스처 에셋을 생성한 뒤 머티리얼 참조를 연결해야 합니다.",
        ".fbx" or ".obj" or ".blend" => "메시·재질 슬롯·스켈레톤·애니메이션 포함 여부를 실제 분석하고 변환·참조 연결해야 합니다.",
        ".uasset" => "엔진 어댑터로 내부 에셋 유형부터 확인해야 합니다. 확장자만으로 Blueprint·머티리얼 등으로 대응하지 않습니다.",
        ".dll" => "바이너리 의존성의 기능·대체 구현·사용 가능 여부를 확인해야 합니다.",
        _ => "파일 내부 형식과 의존성을 확인하고 대응 변환기·프로젝트 설정 반영 및 실행 검증을 구현해야 합니다."
    };

    public static string ToHtml(InspectionReport report, ConversionBundleResult bundle)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "생성되지 않음");
        var html = new StringBuilder("""
<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>원본 → 대상 포팅 대응표</title>
<style>:root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;background:#211e1a;color:#f5efe5;overflow-wrap:anywhere;font:15px/1.75 system-ui,'맑은 고딕',sans-serif}main{max-width:1180px;margin:auto;padding:40px 24px}h1{font-size:36px;line-height:1.3}h2,h3,a{color:#f3ab73}p{color:#c0b3a1}.notice,article{background:#302b25;border:1px solid #514638;padding:20px;border-radius:12px;margin:20px 0}.notice{border-left:4px solid #f3ab73}code,pre{background:#191713;padding:4px 8px;border-radius:5px;white-space:pre-wrap;overflow-wrap:anywhere}pre{padding:16px;overflow:auto}.table{overflow-x:auto}table{border-collapse:collapse;width:100%;min-width:680px}th,td{text-align:left;vertical-align:top;padding:12px;border-bottom:1px solid #514638;overflow-wrap:anywhere}th{color:#f3ab73}td:first-child{max-width:300px}input{width:100%;padding:12px;border:1px solid #77624d;border-radius:8px;background:#302b25;color:#f5efe5;font:inherit}summary{cursor:pointer;color:#f3ab73}article[data-hidden="true"]{display:none}small{color:#c0b3a1}@media(max-width:600px){main{padding:24px 16px}h1{font-size:28px}}@media print{body{background:white;color:black}article,.notice,pre,code{background:#f4eee7;color:black}p,small{color:#4a4036}input{display:none}h2,h3,a,th{color:#8d431e}}
</style></head><body><main><small>ENGINEMIGRATOR / SOURCE → TARGET</small><h1>무엇이 어디로 옮겨졌나요?</h1>
<div class="notice"><strong>전체 프로젝트 포팅 미완료</strong><p>코드 생성과 대상 엔진 배치·참조 연결·실행 성공은 서로 다른 단계입니다. 이 보고서는 이번 실행에서 생성된 코드와 미변환 파일을 구분합니다. 원본 파일을 대상에 복사한 것으로 엔진 변환 성공을 표시하지 않습니다.</p></div>
""");
        html.Append($"<p>{E(report.SourceEngine.ToString())} → {E(report.TargetEngine.ToString())}<br>원본: {E(report.Source)}<br>선택한 대상 프로젝트: {E(report.Target)}<br>이번 실제 출력 폴더: {E(bundle.Directory)}</p>");
        html.Append("<h2>조사 범위</h2><p>이번 검사에서 발견된 파일만 포함합니다. 외부 패키지·엔진 설치 플러그인·바이너리 내부 코드 등의 누락 여부는 전체 의존성 분석 전까지 미확인입니다.</p><ul>");
        foreach (var finding in report.Findings) html.Append($"<li>{E(finding.Severity)}: {E(finding.Message)}</li>");
        html.Append("</ul><h2>원본 파일 → 실제 생성 위치</h2><p>검색하면 파일 대응표와 세부 설명이 함께 필터링됩니다.</p><label for=\"search\">원본 경로·유형·상태 검색</label><input id=\"search\" type=\"search\" placeholder=\"CombatMath, 머티리얼, 미변환…\"><div class=\"table\"><table><thead><tr><th>원본</th><th>유형</th><th>실제 생성 위치</th><th>상태</th></tr></thead><tbody>");
        foreach (var entry in bundle.Mapping)
            html.Append($"<tr data-search=\"{E(entry.SourcePath + " " + entry.Kind + " " + entry.Status)}\"><td>{E(entry.SourcePath)}</td><td>{E(entry.Kind)}</td><td>{OutputLink(entry.ActualOutputPath)}</td><td>{E(entry.Status)}</td></tr>");
        html.Append("</tbody></table></div><h2>파일별 대입·연결 안내</h2>");
        foreach (var entry in bundle.Mapping)
        {
            html.Append($"<article data-search=\"{E(entry.SourcePath + " " + entry.Kind + " " + entry.Status)}\"><h3>{E(entry.SourcePath)}</h3><p><strong>{E(entry.Status)}</strong><br>실제 생성: {OutputLink(entry.ActualOutputPath)}</p><p>변환 규칙: {E(entry.Rule)}<br>참조·배치: {E(entry.BindingStatus)}<br>남은 작업: {E(entry.RequiredWork)}</p>");
            foreach (var map in entry.CodeMappings)
            {
                html.Append($"<details><summary>{E(map.Kind)}: {E(map.SourceSymbol)} → {E(map.TargetSymbol)}</summary><p>원본 L{map.SourceLine} → 생성 파일 L{map.TargetLine}<br>{E(map.Rule)}</p><p>호출·사용 형태: <code>{E(map.Usage)}</code></p><h4>원본 코드</h4><pre>{E(map.SourceSnippet)}</pre><h4>생성 코드</h4><pre>{E(map.TargetSnippet)}</pre></details>");
            }
            html.Append("</article>");
        }
        html.Append("<h2>생성 코드의 대상 프로젝트 통합</h2><p>아래는 배치 위치를 결정할 때의 안내이며 이번 실행에서 자동 적용하지 않았습니다. 버전·모듈·이름 충돌·호출 위치를 확인한 뒤 컴파일과 실행 검증이 필요합니다.</p>");
        html.Append(report.TargetEngine == Engine.Unity
            ? "<p>생성된 .cs는 정적 유틸리티 클래스입니다. 예시 위치는 <code>Assets/EngineMigrator/Generated/클래스.cs</code>이며 실제 생성 위치는 위 표를 따릅니다. 기존 클래스와 이름이 충돌하는지 확인하고 게임 코드에서 <code>클래스.메서드(인수)</code> 형태로 호출해야 합니다. GameObject에 붙이는 MonoBehaviour가 아닙니다.</p>"
            : "<p>생성된 .h는 엔진 객체 상속이 없는 정적 C++ 함수 컨테이너입니다. 대상 C++ 모듈을 선택해 헤더 위치를 결정하고 호출 코드에 include 및 <code>클래스::메서드(인수)</code> 연결이 필요합니다. Blueprint 노출·Actor 생성·컴포넌트 부착은 구현하지 않았습니다. 모듈·빌드 설정과 include 경로는 아직 자동 결정하지 않습니다.</p>");
        html.Append("""
<h2>최종 완료 기준</h2><p>모든 코드·에셋·장면·설정의 대응, ID/참조 연결, 대상 엔진 컴파일, 시작 장면 실행과 원본 플레이 동작 대조가 끝나야 프로젝트 포팅 완료입니다. 이번 결과의 ProjectPortComplete는 false입니다.</p>
<script>document.getElementById('search').addEventListener('input',function(){const q=this.value.toLocaleLowerCase();document.querySelectorAll('[data-search]').forEach(el=>{el.hidden=!el.dataset.search.toLocaleLowerCase().includes(q);});});</script></main></body></html>
""");
        return html.ToString();
        static string OutputLink(string? path) => path is null ? "생성되지 않음" : "<a href=\"" + string.Join("/", path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString)) + "\"><code>" + E(path) + "</code></a>";
    }
}
