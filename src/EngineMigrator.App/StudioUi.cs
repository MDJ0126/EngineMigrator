using System.Drawing.Drawing2D;
using EngineMigrator.Core;

namespace EngineMigrator.App;

public sealed partial class MainForm
{
    private static readonly Color Paper = Color.FromArgb(246, 243, 237);
    private static readonly Color Charcoal = Color.FromArgb(37, 35, 32);
    private static readonly Color Muted = Color.FromArgb(126, 119, 109);
    private static readonly Color Accent = Color.FromArgb(222, 91, 43);
    private static readonly Color Border = Color.FromArgb(229, 223, 214);
    private readonly List<Button> workspaceTabs = [];
    private readonly Label summary = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
    private readonly Label route = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
    private readonly List<Label> engineNames = [];
    private readonly List<PictureBox> engineIcons = [];
    private readonly Panel emptyState = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly TextBox filter = new() { BorderStyle = BorderStyle.None, PlaceholderText = "파일·스크립트·진단 검색", Dock = DockStyle.Fill };
    private readonly List<Image> logos = [];
    private int workspaceTab;

    private static Label TextLabel(string text, float size = 9, Color? color = null, bool bold = false) => new()
    {
        Text = text, ForeColor = color ?? Charcoal, Font = new("맑은 고딕", size, bold ? FontStyle.Bold : FontStyle.Regular),
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty
    };
    private static void Skin(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.BorderSize = 1; button.BackColor = primary ? Accent : Color.White;
        button.ForeColor = primary ? Color.White : Charcoal; button.Height = 38;
        button.Font = new("맑은 고딕", 9, primary ? FontStyle.Bold : FontStyle.Regular);
        button.Padding = new(12, 4, 12, 4); button.Margin = new(0, 0, 10, 0); button.AutoSize = true; button.Cursor = Cursors.Hand;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(197, 73, 28) : Paper;
    }
    private void OpenStudioGuide()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "guide.html")) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "가이드 열기 실패"); }
    }

    private void InitializeStudio()
    {
        Text = "EngineMigrator · Migration Studio"; Size = new(1320, 940); MinimumSize = new(1100, 840);
        StartPosition = FormStartPosition.CenterScreen; Font = new("맑은 고딕", 9); BackColor = Paper; ForeColor = Charcoal;
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        shell.ColumnStyles.Add(new(SizeType.Absolute, 202)); shell.ColumnStyles.Add(new(SizeType.Percent, 100)); Controls.Add(shell);
        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Charcoal, ColumnCount = 1, RowCount = 9, Padding = new(22, 28, 18, 22), Margin = Padding.Empty };
        foreach (var h in new[] { 56, 42, 46, 46, 46, 1 }) sidebar.RowStyles.Add(new(SizeType.Absolute, h));
        sidebar.RowStyles.Add(new(SizeType.Percent, 100)); sidebar.RowStyles.Add(new(SizeType.Absolute, 100)); sidebar.RowStyles.Add(new(SizeType.Absolute, 54));
        var brand = TextLabel("M /", 27, Color.FromArgb(248, 137, 83), true); sidebar.Controls.Add(brand, 0, 0);
        sidebar.Controls.Add(TextLabel("ENGINE MIGRATOR", 10, Color.FromArgb(245, 238, 227), true), 0, 1);
        var active = new Button { Text = "  ◇   마이그레이션", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(66, 53, 42), ForeColor = Color.FromArgb(255, 179, 124), TextAlign = ContentAlignment.MiddleLeft, Margin = new(0, 4, 0, 4) };
        active.FlatAppearance.BorderSize = 0; active.Click += (_, _) => { workspaceTab = 0; RefreshWorkspace(); }; sidebar.Controls.Add(active, 0, 2);
        var guide = new Button { Text = "  ▤   사용 가이드   ↗", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(202, 193, 179), TextAlign = ContentAlignment.MiddleLeft, Margin = new(0, 4, 0, 4), Cursor = Cursors.Hand };
        guide.FlatAppearance.BorderSize = 0; guide.Click += (_, _) => OpenStudioGuide(); sidebar.Controls.Add(guide, 0, 3);
        sidebar.Controls.Add(TextLabel("     양방향 포팅 워크스페이스", 8, Color.FromArgb(148, 140, 127)), 0, 4);
        var ai = new SoftCard { Dock = DockStyle.Fill, BackColor = Color.FromArgb(48, 45, 40), Padding = new(14), Margin = new(0, 10, 0, 10), Stroke = Color.FromArgb(68, 62, 54) };
        var aiText = TextLabel("로컬 AI 어시스턴트\n\n선택 기능 · 연결 준비 중", 9, Color.FromArgb(198, 188, 173)); ai.Controls.Add(aiText); sidebar.Controls.Add(ai, 0, 7);
        sidebar.Controls.Add(TextLabel("DEVELOPMENT PREVIEW\n독립 개발 도구", 8, Color.FromArgb(150, 142, 130)), 0, 8);
        shell.Controls.Add(sidebar, 0, 0);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Padding = new(28, 20, 28, 12), Margin = Padding.Empty };
        foreach (var h in new[] { 80, 36, 182, 62, 40 }) content.RowStyles.Add(new(SizeType.Absolute, h));
        content.RowStyles.Add(new(SizeType.Percent, 100)); content.RowStyles.Add(new(SizeType.Absolute, 14)); content.RowStyles.Add(new(SizeType.Absolute, 94)); content.RowStyles.Add(new(SizeType.Absolute, 30));
        shell.Controls.Add(content, 1, 0);
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new(SizeType.Percent, 75)); heading.ColumnStyles.Add(new(SizeType.Percent, 25));
        heading.RowStyles.Add(new(SizeType.Absolute, 24)); heading.RowStyles.Add(new(SizeType.Percent, 100));
        heading.Controls.Add(TextLabel("MIGRATION  /  WORKSPACE", 8, Muted, true), 0, 0);
        heading.Controls.Add(TextLabel("프로젝트를, 다음 엔진으로.", 23, Charcoal, true), 0, 1);
        var badge = TextLabel("●  개발 프리뷰", 9, Accent, true); badge.TextAlign = ContentAlignment.MiddleRight; heading.Controls.Add(badge, 1, 0);
        route.ForeColor = Muted; route.TextAlign = ContentAlignment.MiddleRight; heading.Controls.Add(route, 1, 1); content.Controls.Add(heading, 0, 0);
        var section = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        section.RowStyles.Add(new(SizeType.Percent, 100));
        section.ColumnStyles.Add(new(SizeType.Percent, 100)); section.ColumnStyles.Add(new(SizeType.Absolute, 124));
        section.Controls.Add(TextLabel("01   프로젝트 연결", 10, Charcoal, true), 0, 0);
        var swap = new Button { Text = "⇄  방향 바꾸기", Dock = DockStyle.Fill }; Skin(swap); swap.AutoSize = false; swap.Padding = Padding.Empty; swap.Margin = new(0, 1, 0, 4);
        direction.Items.AddRange(["Unity → Unreal", "Unreal → Unity"]); direction.SelectedIndex = 0;
        swap.Click += (_, _) => { if (!busy) direction.SelectedIndex = 1 - direction.SelectedIndex; };
        direction.SelectedIndexChanged += (_, _) => RefreshEngines(); section.Controls.Add(swap, 1, 0); content.Controls.Add(section, 0, 1);
        foreach (var file in new[] { "unity.png", "unreal.png" })
        { using var img = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", file)); logos.Add(new Bitmap(img)); }
        var engines = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        engines.ColumnStyles.Add(new(SizeType.Percent, 50)); engines.ColumnStyles.Add(new(SizeType.Absolute, 34)); engines.ColumnStyles.Add(new(SizeType.Percent, 50));
        engines.Controls.Add(EngineCard("SOURCE  /  원본", source, 0), 0, 0);
        var arrow = TextLabel("→", 17, Accent, true); arrow.AutoEllipsis = false; arrow.TextAlign = ContentAlignment.MiddleCenter; engines.Controls.Add(arrow, 1, 0);
        engines.Controls.Add(EngineCard("TARGET  /  대상", target, 1), 2, 0); content.Controls.Add(engines, 0, 2); RefreshEngines();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(0, 14, 0, 0), Margin = Padding.Empty, WrapContents = false };
        Skin(inspect, true); Skin(portResources); Skin(convert); Skin(save); Skin(mappingGuide); Skin(cancel);
        inspect.Text = "프로젝트 검사  →"; convert.Text = "코드 생성"; save.Text = "보고서 저장";
        actions.Controls.AddRange([inspect, portResources, convert, save, mappingGuide, cancel]); content.Controls.Add(actions, 0, 3);
        var notice = new SoftCard { Dock = DockStyle.Fill, BackColor = Color.FromArgb(249, 233, 208), Stroke = Color.FromArgb(237, 217, 187), Padding = new(12, 0, 12, 0), Margin = new(0, 0, 0, 9) };
        notice.Controls.Add(TextLabel("제한 지원  ·  다중 장면·기본 프리팹·객체 복제·삭제·장면 전환·물리·애니메이션·UI. 전체 게임 포팅은 개발 중입니다.", 8.5f, Color.FromArgb(123, 86, 46))); content.Controls.Add(notice, 0, 4);
        content.Controls.Add(ResultsCard(), 0, 5);
        var logs = new SoftCard { Dock = DockStyle.Fill, Padding = new(14, 10, 14, 8), BackColor = Color.FromArgb(238, 234, 226), Margin = Padding.Empty };
        var logLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        logLayout.RowStyles.Add(new(SizeType.Absolute, 23)); logLayout.RowStyles.Add(new(SizeType.Percent, 100));
        logLayout.Controls.Add(TextLabel("실행 로그", 8, Muted, true), 0, 0);
        log.BorderStyle = BorderStyle.None; log.BackColor = logs.BackColor; log.ForeColor = Muted; log.Font = new("맑은 고딕", 8);
        log.Text = "준비되었습니다. 원본과 대상 프로젝트를 연결해 주세요."; logLayout.Controls.Add(log, 0, 1); logs.Controls.Add(logLayout); content.Controls.Add(logs, 0, 7);
        status.Dock = DockStyle.Fill; status.AutoSize = false; status.TextAlign = ContentAlignment.MiddleLeft; status.ForeColor = Muted; status.Font = new("맑은 고딕", 8);
        content.Controls.Add(status, 0, 8);
        FormClosed += (_, _) => { foreach (var logo in logos) logo.Dispose(); };
        RefreshWorkspace();
    }

    private Control EngineCard(string title, TextBox path, int index)
    {
        var card = new SoftCard { Dock = DockStyle.Fill, Padding = new(18, 12, 18, 12), BackColor = Color.White, Margin = new(0, 5, 0, 5) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = Padding.Empty };
        layout.RowStyles.Add(new(SizeType.Absolute, 22)); layout.RowStyles.Add(new(SizeType.Absolute, 64)); layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.Controls.Add(TextLabel(title, 8, Muted, true), 0, 0);
        var engine = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        engine.ColumnStyles.Add(new(SizeType.Absolute, 66)); engine.ColumnStyles.Add(new(SizeType.Percent, 100));
        var iconTile = new SoftCard { BackColor = Color.FromArgb(27, 26, 25), Size = new(52, 52), Padding = new(4), Margin = new(0, 2, 10, 10), Stroke = Color.FromArgb(27, 26, 25) };
        var icon = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = iconTile.BackColor }; iconTile.Controls.Add(icon); engineIcons.Add(icon);
        var name = TextLabel("", 19, Charcoal, true); engineNames.Add(name); engine.Controls.Add(iconTile, 0, 0); engine.Controls.Add(name, 1, 0); layout.Controls.Add(engine, 0, 1);
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new(0, 5, 0, 5) };
        pathRow.RowStyles.Add(new(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new(SizeType.Absolute, 72));
        var input = new Panel { Dock = DockStyle.Fill, BackColor = Paper, Padding = new(9, 9, 6, 7), Margin = new(0, 2, 8, 3) };
        path.BorderStyle = BorderStyle.None; path.BackColor = Paper; path.ForeColor = Charcoal; path.Font = new("맑은 고딕", 8.5f);
        path.PlaceholderText = index == 0 ? "원본 프로젝트 폴더를 선택해 주세요" : "대상 프로젝트 폴더를 선택해 주세요";
        input.Controls.Add(path);
        var hint = TextLabel("프로젝트 폴더를 선택해 주세요", 8, Muted);
        input.Controls.Add(hint); hint.BringToFront(); path.Visible = false;
        hint.Click += (_, _) => { hint.Visible = false; path.Visible = true; path.Focus(); };
        void UpdateHint() { var show = path.Text.Length == 0 && !path.Focused; hint.Visible = show; path.Visible = !show; }
        path.TextChanged += (_, _) => UpdateHint();
        path.Enter += (_, _) => hint.Visible = false; path.Leave += (_, _) => UpdateHint();
        pathRow.Controls.Add(input, 0, 0);
        var browse = new Button { Text = "선택", Dock = DockStyle.Fill }; Skin(browse); browse.Padding = Padding.Empty; browse.AutoSize = false; browse.Margin = new(0, 2, 0, 3);
        browse.Click += (_, _) => { if (busy) return; using var picker = new FolderBrowserDialog(); if (picker.ShowDialog(this) == DialogResult.OK) path.Text = picker.SelectedPath; };
        pathRow.Controls.Add(browse, 1, 0); layout.Controls.Add(pathRow, 0, 2); card.Controls.Add(layout); return card;
    }

    private void RefreshEngines()
    {
        if (engineNames.Count != 2 || logos.Count != 2) return;
        var first = direction.SelectedIndex;
        engineNames[0].Text = first == 0 ? "Unity" : "Unreal Engine"; engineNames[1].Text = first == 0 ? "Unreal Engine" : "Unity";
        engineIcons[0].Image = logos[first]; engineIcons[1].Image = logos[1 - first];
        route.Text = first == 0 ? "Unity  →  Unreal Engine" : "Unreal Engine  →  Unity";
    }

    private Control ResultsCard()
    {
        var card = new SoftCard { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new(16, 8, 16, 12), Margin = Padding.Empty };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        layout.RowStyles.Add(new(SizeType.Absolute, 37)); layout.RowStyles.Add(new(SizeType.Absolute, 35)); layout.RowStyles.Add(new(SizeType.Percent, 100));
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        top.ColumnStyles.Add(new(SizeType.Percent, 60)); top.ColumnStyles.Add(new(SizeType.Percent, 40));
        top.Controls.Add(TextLabel("02   프로젝트 살펴보기", 10, Charcoal, true), 0, 0); summary.ForeColor = Muted; summary.Font = new("맑은 고딕", 8); top.Controls.Add(summary, 1, 0); layout.Controls.Add(top, 0, 0);
        var tabs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        tabs.ColumnStyles.Add(new(SizeType.Percent, 65)); tabs.ColumnStyles.Add(new(SizeType.Percent, 35));
        var tabFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        foreach (var (text, i) in new[] { ("파일 목록", 0), ("스크립트", 1), ("진단", 2) })
        {
            var tab = new Button { Text = text, Width = 88, Height = 30, FlatStyle = FlatStyle.Flat, Margin = new(0, 0, 8, 0), Cursor = Cursors.Hand };
            tab.FlatAppearance.BorderSize = 0; tab.Click += (_, _) => { workspaceTab = i; RefreshWorkspace(); }; workspaceTabs.Add(tab); tabFlow.Controls.Add(tab);
        }
        tabs.Controls.Add(tabFlow, 0, 0);
        var filterBox = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Paper, Padding = new(10, 3, 6, 3), Margin = new(0, 0, 0, 5) };
        filterBox.ColumnStyles.Add(new(SizeType.Absolute, 38)); filterBox.ColumnStyles.Add(new(SizeType.Percent, 100)); filterBox.RowStyles.Add(new(SizeType.Percent, 100));
        filter.BackColor = Paper; filter.ForeColor = Charcoal; filter.Font = new("맑은 고딕", 8); filter.Margin = Padding.Empty; filterBox.Controls.Add(TextLabel("검색", 8, Muted), 0, 0); filterBox.Controls.Add(filter, 1, 0); tabs.Controls.Add(filterBox, 1, 0);
        layout.Controls.Add(tabs, 0, 1); filter.TextChanged += (_, _) => RefreshWorkspace();
        var view = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty }; ConfigureStudioGrid(); view.Controls.Add(grid);
        emptyState.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if(emptyState.Height<150)
            {
                using var titleFont=new Font("맑은 고딕",10,FontStyle.Bold);using var detailFont=new Font("맑은 고딕",8);
                var middle=emptyState.Height/2;
                TextRenderer.DrawText(e.Graphics,"아직 연결된 프로젝트가 없습니다",titleFont,new Rectangle(8,Math.Max(0,middle-24),emptyState.Width-16,25),Charcoal,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics,"위에서 폴더를 선택하고 프로젝트 검사를 시작해 주세요.",detailFont,new Rectangle(8,middle+5,emptyState.Width-16,24),Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                return;
            }
            var cx = emptyState.Width / 2; var y = Math.Max(12, emptyState.Height / 2 - 69);
            using var circle = new SolidBrush(Paper); e.Graphics.FillEllipse(circle, cx - 24, y, 48, 48);
            using var font = new Font("맑은 고딕", 19); TextRenderer.DrawText(e.Graphics, "⇄", font, new Rectangle(cx - 24, y, 48, 48), Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using var bold = new Font("맑은 고딕", 11, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, "아직 연결된 프로젝트가 없습니다", bold, new Rectangle(0, y + 62, emptyState.Width, 26), Charcoal, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(e.Graphics, "위에서 폴더를 선택하고 프로젝트 검사를 시작해 주세요.", Font, new Rectangle(0, y + 93, emptyState.Width, 27), Muted, TextFormatFlags.HorizontalCenter);
        };
        emptyState.Resize+=(_,_)=>emptyState.Invalidate();
        view.Controls.Add(emptyState); layout.Controls.Add(view, 0, 2); card.Controls.Add(layout); return card;
    }

    private void ConfigureStudioGrid()
    {
        grid.BackgroundColor = Color.White; grid.BorderStyle = BorderStyle.None; grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false; grid.RowHeadersVisible = false; grid.ColumnHeadersHeight = 34;
        grid.ColumnHeadersDefaultCellStyle = new() { BackColor = Paper, ForeColor = Muted, Font = new("맑은 고딕", 8, FontStyle.Bold), SelectionBackColor = Paper, Padding = new(8, 0, 0, 0) };
        grid.DefaultCellStyle = new() { BackColor = Color.White, ForeColor = Charcoal, SelectionBackColor = Color.FromArgb(250, 229, 209), SelectionForeColor = Charcoal, Font = new("맑은 고딕", 8), Padding = new(8, 0, 0, 0) };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 250, 247); grid.RowTemplate.Height = 32;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false; grid.AllowUserToResizeRows = false;
    }

    private void RefreshWorkspace()
    {
        if (workspaceTabs.Count == 0) return;
        for (var i = 0; i < workspaceTabs.Count; i++)
        { workspaceTabs[i].BackColor = i == workspaceTab ? Color.FromArgb(251, 233, 218) : Color.White; workspaceTabs[i].ForeColor = i == workspaceTab ? Accent : Muted; }
        grid.DataSource = null; grid.Columns.Clear();
        if (report is null) { emptyState.Visible = true; summary.Text = "검사 대기"; return; }
        emptyState.Visible = false;
        bool Match(params string[] values) => string.IsNullOrWhiteSpace(filter.Text) || values.Any(v => v.Contains(filter.Text, StringComparison.OrdinalIgnoreCase));
        if (workspaceTab == 0) grid.DataSource = report.Assets.Where(a => Match(a.Path, a.Kind)).Select(a => new { 파일 = a.Path, 유형 = a.Kind, 바이트 = a.Bytes }).ToArray();
        else if (workspaceTab == 1) grid.DataSource = report.Scripts.Where(s => Match(s.Path, s.Language, s.Status)).Select(s => new { 스크립트 = s.Path, 언어 = s.Language, 상태 = s.Status }).ToArray();
        else grid.DataSource = report.Findings.Where(f => Match(f.Severity, f.Message)).Select(f => new { 수준 = f.Severity, 내용 = f.Message }).ToArray();
        summary.Text = $"파일 {report.Assets.Count:N0}  /  스크립트 {report.Scripts.Count:N0}";
        if (grid.Columns.Count > 0) grid.Columns[0].FillWeight = workspaceTab == 2 ? 15 : 55;
    }

    internal async Task VerifyStudioAsync()
    {
        Directory.CreateDirectory("artifacts");
        void Capture(string name) { PerformLayout(); using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save("artifacts/" + name + ".png"); }
        await Task.Delay(80); Capture("studio-preview");
        var root = Path.Combine(Path.GetTempPath(), "EngineMigrator-Studio-" + Guid.NewGuid().ToString("N"));
        try
        {
            var unity = Path.Combine(root, "Unity"); var unreal = Path.Combine(root, "Unreal");
            Directory.CreateDirectory(Path.Combine(unity, "Assets")); Directory.CreateDirectory(Path.Combine(unity, "ProjectSettings")); Directory.CreateDirectory(unreal);
            File.WriteAllText(Path.Combine(unity, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.0.83f1");
            File.WriteAllText(Path.Combine(unity, "Assets", "CombatMath.cs"), "public static class CombatMath { public static double Damage(double a, double b) => a - b; }");
            File.WriteAllText(Path.Combine(unreal, "Demo.uproject"), "{\"EngineAssociation\":\"5.5\"}");
            source.Text = unity; target.Text = unreal; await InspectAsync();
            if (report?.Scripts.Count != 1 || !convert.Enabled || !portResources.Enabled || emptyState.Visible) throw new InvalidOperationException("Inspection/resource action binding failed");
            convert.PerformClick();await Task.Delay(40);if(!codeMenu.Visible||codeMenu.Items.Count!=3||codeMenu.Items[1].Text!="Blueprint 함수 변환"||codeMenu.Items[2].Text!="MonoBehaviour / Actor 클래스 변환")throw new InvalidOperationException("Blueprint/Actor conversion menu binding failed");Capture("studio-code-menu");codeMenu.Hide();
            workspaceTab = 1; RefreshWorkspace(); if (grid.Rows.Count != 1) throw new InvalidOperationException("Script tab failed");
            filter.Text = "no-match-xyz"; if (grid.Rows.Count != 0) throw new InvalidOperationException("Filter failed"); filter.Clear();
            workspaceTab = 2; RefreshWorkspace(); if (grid.Rows.Count != report.Findings.Count) throw new InvalidOperationException("Diagnostics tab failed");
            var bundle = await Task.Run(() => ConversionBundle.Export(report, Path.Combine(root, "exports")));
            PresentBundle(bundle);
            if (!mappingGuide.Enabled || !File.Exists(lastMappingHtml)) throw new InvalidOperationException("Generated mapping report UI failed");
            workspaceTab = 0; RefreshWorkspace(); Capture("studio-results");
            direction.SelectedIndex = 1; if (engineNames[0].Text != "Unreal Engine" || report is not null || convert.Enabled || portResources.Enabled || mappingGuide.Enabled || lastMappingHtml is not null) throw new InvalidOperationException("Direction/invalidation failed");
            Size = MinimumSize; await Task.Delay(80); Capture("studio-minimum");
            File.WriteAllText("artifacts/studio-checks.txt", "PASS icons loaded, initial/results/minimum-size rendering, inspection/resource action binding, script/diagnostic tabs, search, code generation and mapping report button, direction switch and stale result invalidation. Synthetic fixture; no engine project port test.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

internal sealed class SoftCard : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Stroke { get; set; } = Color.FromArgb(229, 223, 214);
    public SoftCard() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Stroke); using var path = new GraphicsPath();
        var rect = new Rectangle(0, 0, Width - 1, Height - 1); const int d = 16;
        if (rect.Width < d || rect.Height < d) return;
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90); path.CloseFigure();
        e.Graphics.DrawPath(pen, path);
    }
}
