using EngineMigrator.Core;

namespace EngineMigrator.App;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--smoke-test"))
        {
            using var form = new MainForm();
            form.Shown += async (_, _) =>
            {
                try { await form.VerifyStudioAsync(); }
                catch (Exception ex) { Directory.CreateDirectory("artifacts"); File.WriteAllText("artifacts/studio-error.txt", ex.ToString()); Environment.ExitCode = 1; }
                finally { form.Close(); }
            };
            Application.Run(form);
            return;
        }
        Application.Run(new MainForm());
    }
}

public sealed partial class MainForm : Form
{
    private readonly TextBox source = new() { Dock = DockStyle.Fill };
    private readonly TextBox target = new() { Dock = DockStyle.Fill };
    private readonly ComboBox direction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly Button inspect = new() { Text = "사전 검사", AutoSize = true };
    private readonly Button save = new() { Text = "보고서 저장", AutoSize = true, Enabled = false };
    private readonly Button cancel = new() { Text = "검사 취소", AutoSize = true, Enabled = false };
    private readonly Button convert = new() { Text = "스크립트 코드 생성", AutoSize = true, Enabled = false };
    private readonly Button mappingGuide = new() { Text = "변환 대응표  ↗", AutoSize = true, Enabled = false };
    private readonly Button portResources = new() { Text = "장면·리소스 포팅", AutoSize = true, Enabled = false };
    private readonly ContextMenuStrip codeMenu = new();
    private string? lastMappingHtml;
    private readonly Label status = new() { Text = "프로젝트를 선택한 후 사전 검사를 실행해 주세요.", AutoSize = true };
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly TextBox log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private InspectionReport? report;
    private CancellationTokenSource? cancellation;
    private bool busy;

    public MainForm()
    {
        InitializeStudio();
        inspect.Click += async (_, _) => await InspectAsync();
        cancel.Click += (_, _) => cancellation?.Cancel();
        save.Click += (_, _) => SaveReport();
        codeMenu.Items.Add("C++ / C# 코드 생성",null,async (_,_)=>await ConvertAsync());
        codeMenu.Items.Add("Blueprint 함수 변환",null,async (_,_)=>await PortBlueprintAsync());
        codeMenu.Items.Add("MonoBehaviour / Actor 클래스 변환",null,async (_,_)=>await PortActorAsync());
        convert.Click += (_,_)=>codeMenu.Show(convert,new Point(0,convert.Height));
        Disposed += (_,_)=>codeMenu.Dispose();
        portResources.Click += async (_, _) => await PortResourcesAsync();
        mappingGuide.Click += (_, _) =>
        {
            if (lastMappingHtml is null) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(lastMappingHtml) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "대응표 열기 실패"); }
        };
        source.TextChanged += (_, _) => InvalidateReport(); target.TextChanged += (_, _) => InvalidateReport();
        direction.SelectedIndexChanged += (_, _) => InvalidateReport();
        FormClosing += (_, e) => { if (busy) { cancellation?.Cancel(); e.Cancel = true; status.Text = "검사를 취소하고 있습니다. 취소 후 닫아 주세요."; } };
    }

    private void InvalidateReport()
    {
        report = null; save.Enabled = false; grid.DataSource = null; log.Clear();
        convert.Enabled = false;
        portResources.Enabled = false;
        mappingGuide.Enabled = false; lastMappingHtml = null;
        status.Text = "설정이 변경되었습니다. 사전 검사를 실행해 주세요.";
        RefreshWorkspace();
    }
    private async Task InspectAsync()
    {
        busy = true; inspect.Enabled = false; cancel.Enabled = true; save.Enabled = false; convert.Enabled = false;
        mappingGuide.Enabled = false; lastMappingHtml = null;
        portResources.Enabled = false;
        source.Enabled = target.Enabled = direction.Enabled = false;
        report = null; grid.DataSource = null; log.Clear(); status.Text = "파일 목록 검사 중…";
        using var tokenSource = new CancellationTokenSource(); cancellation = tokenSource;
        try
        {
            var sourcePath = source.Text; var targetPath = target.Text;
            var engine = direction.SelectedIndex == 0 ? Engine.Unity : Engine.Unreal;
            report = await Task.Run(() => ProjectInspector.Inspect(sourcePath, targetPath, engine, tokenSource.Token));
            RefreshWorkspace();
            log.Text = string.Join(Environment.NewLine, report.Findings.Select(f => $"[{f.Severity}] {f.Message}"))
                + Environment.NewLine + string.Join(Environment.NewLine, report.Scripts.Select(s => $"[스크립트] {s.Path}: {s.Status}"));
            status.Text = $"조사 완료: 파일 {report.Assets.Count}개 · 스크립트 {report.Scripts.Count}개 / 실제 포팅 미완료";
            save.Enabled = true;
            convert.Enabled = report.Assets.Count > 0 && !report.Findings.Any(f => f.Severity == "오류");
            portResources.Enabled = convert.Enabled;
        }
        catch (OperationCanceledException) { status.Text = "검사를 취소했습니다."; }
        catch (Exception ex) { status.Text = "검사 실패"; log.Text = ex.Message; }
        finally { busy = false; cancellation = null; inspect.Enabled = true; cancel.Enabled = false; source.Enabled = target.Enabled = direction.Enabled = true; }
    }
    private void SaveReport()
    {
        if (report is null) return;
        using var dialog = new SaveFileDialog { Filter = "HTML 포팅 보고서|*.html|JSON 보고서|*.json", FileName = "migration-inspection.html" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllText(dialog.FileName, dialog.FilterIndex == 1 ? ScriptPorting.ToHtml(report) : report.ToJson()); status.Text = "보고서를 저장했습니다."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, "저장 실패"); }
    }

    private async Task ConvertAsync()
    {
        if (report is null) return;
        using var picker = new FolderBrowserDialog { Description = "부분 코드 생성 결과를 저장할 폴더를 선택해 주세요. 고유한 하위 폴더가 생성됩니다." };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        var current = report; var destination = picker.SelectedPath;
        busy = true; inspect.Enabled = convert.Enabled = save.Enabled = false;
        mappingGuide.Enabled = false; lastMappingHtml = null;
        source.Enabled = target.Enabled = direction.Enabled = false; cancel.Enabled = true;
        using var tokenSource = new CancellationTokenSource(); cancellation = tokenSource;
        try
        {
            status.Text = "스크립트 부분 코드 생성 중…";
            var bundle = await Task.Run(() => ConversionBundle.Export(current, destination, tokenSource.Token));
            PresentBundle(bundle);
        }
        catch (OperationCanceledException) { status.Text = "코드 생성을 취소했습니다."; }
        catch (Exception ex) { status.Text = "코드 생성 실패"; log.Text = ex.Message; }
        finally
        {
            busy = false; cancellation = null; inspect.Enabled = save.Enabled = convert.Enabled = true;
            source.Enabled = target.Enabled = direction.Enabled = true; cancel.Enabled = false;
        }
    }

    private void PresentBundle(ConversionBundleResult bundle)
    {
        log.Text = string.Join(Environment.NewLine, bundle.Mapping.Select(m => $"[{m.Status}] {m.SourcePath}: {m.Rule}"));
        log.AppendText(Environment.NewLine + "출력: " + bundle.Directory);
        lastMappingHtml = Path.Combine(bundle.Directory, "porting-map.html"); mappingGuide.Enabled = true;
        log.AppendText(Environment.NewLine + "원본→대상 연결 안내: " + lastMappingHtml);
        status.Text = $"코드 생성 {bundle.Files.Count(f => f.Generated)}개 / 미변환 파일 {bundle.Mapping.Count(m => m.ActualOutputPath is null)}개 — 프로젝트 포팅 미완료";
    }

    private async Task PortResourcesAsync()
    {
        try { await PortResourcesCoreAsync(); }
        catch (Exception ex) { status.Text = "리소스 포팅 시작 실패"; log.AppendText(Environment.NewLine + ex.Message); }
    }
    private async Task PortResourcesCoreAsync()
    {
        if (busy || report is null) return;
        var current = report;
        string? Executable(string title, string guess)
        {
            if (File.Exists(guess)) return guess;
            using var dialog = new OpenFileDialog { Title = title, Filter = "엔진 실행 파일|*.exe", CheckFileExists = true };
            return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }
        var unityProject = current.SourceEngine == Engine.Unity ? current.Source : current.Target;
        var unityVersion = File.ReadLines(Path.Combine(unityProject, "ProjectSettings", "ProjectVersion.txt")).First(l => l.StartsWith("m_EditorVersion:")).Split(':', 2)[1].Trim();
        var unity = Executable("해당 버전의 Unity.exe 선택", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor", unityVersion, "Editor", "Unity.exe"));
        if (unity is null) return;
        var unrealProject = current.SourceEngine == Engine.Unreal ? current.Source : current.Target;
        string unrealVersion;
        using (var descriptor = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(unrealProject, "*.uproject").Single())))
            unrealVersion = descriptor.RootElement.TryGetProperty("EngineAssociation", out var v) ? v.GetString() ?? "" : "";
        var unreal = Executable("해당 버전의 UnrealEditor-Cmd.exe 선택", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games", "UE_" + unrealVersion, "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe"));
        if (unreal is null) return;
        using var output = new FolderBrowserDialog { Description = "독립 작업 복사본과 포팅 결과를 저장할 폴더를 선택하세요. 원본·선택한 대상 프로젝트는 수정하지 않습니다." };
        if (output.ShowDialog(this) != DialogResult.OK) return;
        busy = true; inspect.Enabled = save.Enabled = convert.Enabled = portResources.Enabled = mappingGuide.Enabled = false;
        source.Enabled = target.Enabled = direction.Enabled = false; cancel.Enabled = true;
        using var cts = new CancellationTokenSource(); cancellation = cts;
        try
        {
            var progress = new Progress<string>(message => { status.Text = message; log.AppendText(Environment.NewLine + message); });
            var result = await ResourcePorting.RunAsync(current, output.SelectedPath, new(unity, unreal), Path.Combine(AppContext.BaseDirectory, "adapters"), progress, cts.Token);
            lastMappingHtml = Path.Combine(result.RunDirectory, "resource-port.html"); mappingGuide.Enabled = true;
            status.Text = "실제 장면·지원 스크립트·참조 검증 통과 / 전체 프로젝트 포팅 미완료";
            log.AppendText(Environment.NewLine + "대상 결과 프로젝트: " + result.TargetCopy + Environment.NewLine + "검증/연결 대응표: " + lastMappingHtml);
        }
        catch (OperationCanceledException) { status.Text = "리소스 포팅을 취소했습니다. 원본은 보존되었습니다."; }
        catch (Exception ex) { status.Text = "리소스 포팅 미완료 — 로그 확인 필요"; log.AppendText(Environment.NewLine + ex.Message); }
        finally { busy = false; cancellation = null; inspect.Enabled = save.Enabled = convert.Enabled = portResources.Enabled = true; source.Enabled = target.Enabled = direction.Enabled = true; cancel.Enabled = false; }
    }

}
