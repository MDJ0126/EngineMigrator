using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace EngineMigrator.Core;
internal static class ScriptIdentityPorting
{
    internal static void Restore(JsonNode data,string path,string project)
    {
        var matches=(data["scripts"]?.AsArray()??new JsonArray()).Where(s=>Path.GetFileName(s?["name"]?.GetValue<string>())==Path.GetFileName(path)).ToArray();
        if(matches.Length==0)return;if(matches.Length!=1)throw new InvalidOperationException("스크립트 식별자 이름 충돌: "+Path.GetFileName(path));
        var source=matches[0]!;var guid=source["guid"]?.GetValue<string>();if(string.IsNullOrEmpty(guid))return;
        if(!Regex.IsMatch(guid,"^[a-fA-F0-9]{32}$"))throw new InvalidOperationException("잘못된 스크립트 GUID: "+Path.GetFileName(path));
        foreach(var meta in Directory.EnumerateFiles(Path.Combine(project,"Assets"),"*.meta",SearchOption.AllDirectories))if(Regex.IsMatch(File.ReadAllText(meta),"(?im)^guid: *"+guid+" *$"))throw new InvalidOperationException("대상 자산 GUID 충돌: "+guid);
        var order=source["executionOrder"]?.GetValue<int>()??0;
        File.WriteAllText(path+".meta","fileFormatVersion: 2\nguid: "+guid.ToLowerInvariant()+"\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: "+order.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
    }
}
