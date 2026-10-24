using EngineMigrator.Core;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;

namespace EngineMigrator.App;
public sealed partial class MainForm
{
    private async Task PortActorAsync()
    {
        if(busy||report is null)return;
        try
        {
            var current=report;
            using var destination=new FolderBrowserDialog{Description="클래스 변환 결과·컴파일 작업 복사본을 저장할 폴더를 선택하세요."};if(destination.ShowDialog(this)!=DialogResult.OK)return;
            var output=Path.GetFullPath(destination.SelectedPath);ResourcePorting.GuardPath(output);
            foreach(var original in new[]{current.Source,current.Target})if(output.Equals(Path.GetFullPath(original),StringComparison.OrdinalIgnoreCase)||output.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(original))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("프로젝트 외부의 출력 폴더를 선택해 주세요.");
            busy=true;inspect.Enabled=convert.Enabled=portResources.Enabled=save.Enabled=mappingGuide.Enabled=false;source.Enabled=target.Enabled=direction.Enabled=false;cancel.Enabled=true;
            using var cts=new CancellationTokenSource();cancellation=cts;
            try
            {
                if(current.SourceEngine==Engine.Unity)
                {
                    var roots=current.Scripts.Where(s=>s.Path.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)&&!s.Path.Replace('\\','/').Split('/').Contains("Editor")).Select(s=>CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(current.Source,s.Path))).GetCompilationUnitRoot()).ToArray();
                    var combined=string.Join("\n",roots.SelectMany(r=>r.Usings).Select(u=>u.ToString()).Distinct())+"\n"+string.Join("\n",roots.SelectMany(r=>r.Members).Select(m=>m.ToFullString()));
                    using var descriptor=JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(current.Target,"*.uproject").Single()));var version=descriptor.RootElement.GetProperty("EngineAssociation").GetString();
                    var editor=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Epic Games","UE_"+version,"Engine","Binaries","Win64","UnrealEditor-Cmd.exe");
                    if(!File.Exists(editor)){using var pick=new OpenFileDialog{Title="일치하는 UnrealEditor-Cmd.exe",Filter="Unreal editor|UnrealEditor-Cmd.exe"};if(pick.ShowDialog(this)!=DialogResult.OK)return;editor=pick.FileName;}
                    status.Text="MonoBehaviour → Actor 상속 클래스 생성·Unreal 실제 컴파일 중…";
                    var run=await ActorProjectPorting.RunAsync(combined,current.Target,output,editor,cts.Token);lastMappingHtml=Path.Combine(run,"actor-port.html");mappingGuide.Enabled=true;log.AppendText(Environment.NewLine+"Actor 클래스 프로젝트: "+run);
                }
                else
                {
                    var inputs=new List<ActorFile>();foreach(var folder in new[]{"Source","Plugins"}.Select(p=>Path.Combine(current.Source,p)).Where(Directory.Exists))foreach(var file in Directory.EnumerateFiles(folder,"*.h",SearchOption.AllDirectories)){if(file.Replace('\\','/').Split('/').Contains("Intermediate"))continue;var code=File.ReadAllText(file);if(code.StartsWith("// EM_ACTOR "))inputs.Add(new(Path.GetFileName(file),code));}
                    if(inputs.Count==0)throw new NotSupportedException("현재 역변환은 EngineMigrator가 생성한 Actor 클래스만 지원합니다.");
                    var files=await Task.Run(()=>ActorScripts.ToUnity(inputs),cts.Token);var run=Path.Combine(output,"ActorToUnity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(run);foreach(var file in files)File.WriteAllText(Path.Combine(run,file.Name),file.Code);log.AppendText(Environment.NewLine+"복원한 MonoBehaviour 소스: "+run);
                }
                status.Text="클래스 변환 통과 / 장면 부착·전체 포팅 미완료";
            }
            finally{busy=false;cancellation=null;inspect.Enabled=convert.Enabled=portResources.Enabled=save.Enabled=true;source.Enabled=target.Enabled=direction.Enabled=true;cancel.Enabled=false;}
        }
        catch(OperationCanceledException){status.Text="Actor 클래스 변환을 취소했습니다.";}
        catch(Exception ex){status.Text="Actor 클래스 변환 미완료";log.AppendText(Environment.NewLine+ex.Message);}
    }
}
