using System.Text.RegularExpressions;
namespace EngineMigrator.Core;
internal static class UnityProjectAudit
{
    internal static void Add(string root,IReadOnlyList<AssetEntry> assets,List<Finding> findings,CancellationToken token)
    {
        var guids=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        void Issue(string severity,string code,string path,int? line,string message,string fix)=>findings.Add(new(severity,path+(line is null?"":":"+line)+" — "+message+" / "+fix){Code=code,SourcePath=path,Line=line,Suggestion=fix});
        foreach(var asset in assets.Where(a=>a.Path.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)))
        {
            token.ThrowIfCancellationRequested();var file=Path.Combine(root,asset.Path);try{var match=Regex.Match(File.ReadAllText(file),@"(?m)^guid:\s*([a-fA-F0-9]{32})\s*$");if(!match.Success)continue;var guid=match.Groups[1].Value;
                if(guids.TryGetValue(guid,out var other))Issue("차단","DUPLICATE_GUID",asset.Path,null,"동일 GUID가 "+other+"에도 있습니다.","원본 .meta를 확인해 서로 다른 자산에 고유 GUID를 복원해 주세요.");else guids.Add(guid,asset.Path);
            }catch(IOException ex){Issue("미확인","GUID_READ_FAILED",asset.Path,null,ex.Message,"파일 접근 상태를 확인해 주세요.");}
        }
        foreach(var asset in assets.Where(a=>new[]{".unity",".prefab",".mat",".asset",".controller"}.Contains(Path.GetExtension(a.Path).ToLowerInvariant())))
        {
            token.ThrowIfCancellationRequested();try{
                var file=Path.Combine(root,asset.Path);using(var reader=new StreamReader(file)){if(!(reader.ReadLine()?.StartsWith("%YAML")??false))continue;}
                var lines=File.ReadAllLines(file);var ids=new HashSet<string>(Regex.Matches(string.Join("\n",lines),@"(?m)^--- !u!\d+ &(-?\d+)").Cast<Match>().Select(m=>m.Groups[1].Value));var reported=new HashSet<string>();
                for(int i=0;i<lines.Length;i++){
                    var local=Regex.Match(lines[i],@"(?:[:]|-)\s*\{fileID:\s*(-?\d+)\s*\}");if(local.Success&&local.Groups[1].Value!="0"&&!ids.Contains(local.Groups[1].Value))Issue("차단","MISSING_LOCAL_REFERENCE",asset.Path,i+1,"대상 fileID "+local.Groups[1].Value+"가 같은 파일에 없습니다.","Inspector에서 끊어진 객체/컴포넌트 참조를 다시 지정해 주세요.");
                    var external=Regex.Match(lines[i],@"(?:[:]|-)\s*\{fileID:\s*-?\d+,\s*guid:\s*([a-fA-F0-9]{32}),\s*type:\s*\d+\}");if(!external.Success)continue;var guid=external.Groups[1].Value;
                    if(!guid.StartsWith("0000000000000000",StringComparison.Ordinal)&&!guids.ContainsKey(guid)&&reported.Add(guid))Issue("미확인","EXTERNAL_GUID_UNRESOLVED",asset.Path,i+1,"GUID "+guid+"가 프로젝트 내 .meta에서 확인되지 않았습니다.","패키지 자산일 수 있습니다. 엔진 사전 검사로 확인하고, 누락이면 원본 자산과 .meta를 복원해 주세요.");
                }
            }catch(IOException ex){Issue("미확인","REFERENCE_READ_FAILED",asset.Path,null,ex.Message,"파일 접근 상태를 확인해 주세요.");}
        }
        var scenes=assets.Count(a=>a.Path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase));var prefabs=assets.Count(a=>a.Path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase));
        findings.Add(new("안내",$"프로젝트 범위: 장면 {scenes}개 / 프리팹 {prefabs}개. 다중 장면·프리팹은 프로젝트 단위로 검사하고, 동일 자산 ID의 내용 충돌을 차단합니다."){Code="PROJECT_SCOPE"});
    }
}
