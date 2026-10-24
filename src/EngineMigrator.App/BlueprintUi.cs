using EngineMigrator.Core;
using System.Text.Json;

namespace EngineMigrator.App;

public sealed partial class MainForm
{
    private async Task PortBlueprintAsync()
    {
        if(busy||report is null)return;
        try
        {
            var current=report;var import=current.SourceEngine==Engine.Unity;
            using var input=new OpenFileDialog { Title=import?"변환할 순수 C# 함수 파일 선택":"추출할 Blueprint Function Library 에셋 선택",InitialDirectory=Path.Combine(current.Source,import?"Assets":"Content"),Filter=import?"C#|*.cs":"Blueprint asset|*.uasset" };
            if(input.ShowDialog(this)!=DialogResult.OK)return;
            if(!Path.GetFullPath(input.FileName).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(current.Source))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("원본 프로젝트 내부의 파일을 선택해 주세요.");
            var unrealProject=import?current.Target:current.Source;
            using var descriptor=JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(unrealProject,"*.uproject").Single()));
            var version=descriptor.RootElement.GetProperty("EngineAssociation").GetString();
            var editor=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Epic Games","UE_"+version,"Engine","Binaries","Win64","UnrealEditor-Cmd.exe");
            if(!File.Exists(editor)){using var pick=new OpenFileDialog{Title="동일 버전의 UnrealEditor-Cmd.exe 선택",Filter="Unreal editor|UnrealEditor-Cmd.exe"};if(pick.ShowDialog(this)!=DialogResult.OK)return;editor=pick.FileName;}
            using var destination=new FolderBrowserDialog{Description="Blueprint 포팅 작업 복사본의 출력 위치를 선택하세요."};if(destination.ShowDialog(this)!=DialogResult.OK)return;
            ResourcePorting.GuardPath(destination.SelectedPath);
            foreach(var original in new[]{current.Source,current.Target})if(Path.GetFullPath(destination.SelectedPath).Equals(Path.GetFullPath(original),StringComparison.OrdinalIgnoreCase)||Path.GetFullPath(destination.SelectedPath).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(original))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("원본·대상 프로젝트 외부의 출력 폴더를 선택해 주세요.");
            string value;
            if(import){var ir=BlueprintPorting.FromCSharp(File.ReadAllText(input.FileName));var stage=Path.Combine(destination.SelectedPath,"BlueprintInput-"+Guid.NewGuid().ToString("N")+".json");Directory.CreateDirectory(destination.SelectedPath);File.WriteAllText(stage,ir);value=stage;}
            else {var content=Path.Combine(current.Source,"Content");var relative=Path.GetRelativePath(content,input.FileName);if(relative.StartsWith(".."))throw new InvalidOperationException("/Game/ Content 에셋만 지원합니다.");value="/Game/"+Path.ChangeExtension(relative,null).Replace('\\','/')+"."+Path.GetFileNameWithoutExtension(relative);}
            busy=true;inspect.Enabled=convert.Enabled=portResources.Enabled=save.Enabled=mappingGuide.Enabled=false;source.Enabled=target.Enabled=direction.Enabled=false;cancel.Enabled=true;
            using var cts=new CancellationTokenSource();cancellation=cts;
            try
            {
                status.Text="실제 Blueprint 그래프 변환·컴파일 중 — 함수 라이브러리 제한 지원";
                var adapterRoot=Path.Combine(AppContext.BaseDirectory,"adapters","unreal");
                var result=await BlueprintNativePorting.RunAsync(import?"import":"export",unrealProject,value,destination.SelectedPath,editor,Path.Combine(adapterRoot,"EMBlueprintBridge"),Path.Combine(adapterRoot,"verify_blueprint.py"),cts.Token);
                lastMappingHtml=Path.Combine(result,"blueprint-port.html");mappingGuide.Enabled=true;
                status.Text="Blueprint 함수 변환 통과 / 전체 프로젝트 포팅 미완료";log.AppendText(Environment.NewLine+"Blueprint 결과: "+result);
            }
            finally{busy=false;cancellation=null;inspect.Enabled=convert.Enabled=portResources.Enabled=save.Enabled=true;source.Enabled=target.Enabled=direction.Enabled=true;cancel.Enabled=false;}
        }
        catch(OperationCanceledException){status.Text="Blueprint 작업을 취소했습니다.";}
        catch(Exception ex){status.Text="Blueprint 변환 미완료";log.AppendText(Environment.NewLine+ex.Message);}
    }
}
