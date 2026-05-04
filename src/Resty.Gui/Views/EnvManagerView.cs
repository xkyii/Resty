using System.Text.Json;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Services;

namespace Resty.Gui.Views;

/// <summary>
/// 环境变量详情面板：占据右侧主区域，提供 4 个 Tab（公开/私有/文件/内置）。
/// 当侧边栏切换到"环境"Tab 时显示；通过 SelectEnv() 更新当前展示的环境。
/// </summary>
public sealed class EnvManagerView
{
    // ── 色彩 ──────────────────────────────────────────────────────
    private static readonly Color BgRight   = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color BgHover   = Color.FromRgb(0x2A, 0x2D, 0x2E);
    private static readonly Color Accent    = Color.FromRgb(0x00, 0x7A, 0xCC);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);
    private static readonly Color KeyColor  = Color.FromRgb(0x9C, 0xDC, 0xFE);
    private static readonly Color ValColor  = Color.FromRgb(0xCE, 0x91, 0x78);

    // ── 状态 ──────────────────────────────────────────────────────
    private WorkspaceService? _workspace;
    private string _selectedEnv = string.Empty;

    // ── UI 引用 ───────────────────────────────────────────────────
    private readonly Border       _container;
    private TabControl            _tabControl = new();

    // 可编辑行（公开/私有 Tab）
    private readonly List<(TextBox Key, TextBox Val)> _publicRows  = [];
    private readonly List<(TextBox Key, TextBox Val)> _privateRows = [];

    public UIElement RootElement { get; }

    public EnvManagerView()
    {
        _container  = new Border { Background = BgRight };
        RootElement = new Border { Background = BgRight, Child = _container };
        RebuildTabs();
    }

    // ─────────────────────────────────────────────────────────────
    // 公开方法
    // ─────────────────────────────────────────────────────────────

    public void SetWorkspace(WorkspaceService workspace)
    {
        _workspace   = workspace;
        _selectedEnv = string.Empty;
        RebuildTabs();
    }

    /// <summary>切换当前展示的环境（由侧边栏环境选中事件驱动）。</summary>
    public void SelectEnv(string envName)
    {
        _selectedEnv = envName;
        RebuildTabs();
    }

    // ─────────────────────────────────────────────────────────────
    // TabControl 构建
    // ─────────────────────────────────────────────────────────────

    private void RebuildTabs()
    {
        _tabControl = new TabControl();

        string[]    labels   = ["公开变量", "私有变量", "文件变量", "内置"];
        UIElement[] contents = [BuildPublicTab(), BuildPrivateTab(), BuildFileTab(), BuildBuiltinTab()];

        for (int i = 0; i < 4; i++)
        {
            TabItem item = null!;
            new TabItem().Ref(out item)
                .Header(new Label().Text(labels[i]).FontSize(12))
                .Content(contents[i] as Element);
            _tabControl.AddTab(item);
        }

        _container.Child = _tabControl;
    }

    // ─────────────────────────────────────────────────────────────
    // Tab 内容
    // ─────────────────────────────────────────────────────────────

    private UIElement BuildPublicTab()
    {
        _publicRows.Clear();
        if (_workspace is null || string.IsNullOrEmpty(_selectedEnv))
            return BuildHint("在左侧选择一个环境查看变量");
        var filePath = Path.Combine(_workspace.WorkspacePath, "http-client.env.json");
        var vars     = LoadVars(filePath, _selectedEnv);
        return BuildVarTable(vars, _publicRows, () => SaveVars(filePath, _selectedEnv, CollectRows(_publicRows)), "http-client.env.json");
    }

    private UIElement BuildPrivateTab()
    {
        _privateRows.Clear();
        if (_workspace is null || string.IsNullOrEmpty(_selectedEnv))
            return BuildHint("在左侧选择一个环境查看变量");
        var filePath = Path.Combine(_workspace.WorkspacePath, "http-client.private.env.json");
        var vars     = LoadVars(filePath, _selectedEnv);
        return BuildVarTable(vars, _privateRows, () => SaveVars(filePath, _selectedEnv, CollectRows(_privateRows)), "http-client.private.env.json（不提交 git）");
    }

    private UIElement BuildFileTab() =>
        BuildHint("文件变量（@var = value）\n功能待实现：需要打开请求标签页后才能读取");

    private UIElement BuildBuiltinTab()
    {
        var sp = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0, Margin = new Thickness(24, 16, 24, 0) };
        sp.Add(new TextBlock { Text = "内置动态变量（在 .http 文件中直接使用，无需配置）", FontSize = 12, Foreground = TextSec, Margin = new Thickness(0, 0, 0, 12) });
        var builtins = new[]
        {
            ("$uuid",         "生成 UUID v4"),
            ("$timestamp",    "当前 Unix 时间戳（秒）"),
            ("$isoTimestamp", "当前时间 ISO 8601（UTC）"),
            ("$randomInt",    "0–1000 随机整数"),
            ("$env.VAR_NAME", "读取系统环境变量"),
        };
        foreach (var (key, desc) in builtins)
        {
            var row = new DockPanel { Height = 32 };
            row.Add(new TextBlock { Text = $"{{{{{key}}}}}", FontSize = 12, Foreground = KeyColor, VerticalAlignment = VerticalAlignment.Center, Width = 220 }.DockLeft());
            row.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = TextSec, VerticalAlignment = VerticalAlignment.Center });
            sp.Add(row);
            sp.Add(new Border { Height = 1, Background = BorderCol });
        }
        sp.Add(new TextBlock { Text = "当前版本：动态变量暂不解析，发送请求时原样传递。", FontSize = 11, Foreground = TextSec, Margin = new Thickness(0, 16, 0, 0) });
        return new ScrollViewer { Content = sp };
    }

    // ─────────────────────────────────────────────────────────────
    // 变量编辑表格
    // ─────────────────────────────────────────────────────────────

    private UIElement BuildVarTable(Dictionary<string, string> initialVars, List<(TextBox Key, TextBox Val)> rows, Action onSave, string sourceNote)
    {
        var colHeader = new DockPanel { Height = 32, Margin = new Thickness(24, 12, 24, 0) };
        colHeader.Add(new TextBlock { Text = "键", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = TextSec, Width = 220 }.DockLeft());
        colHeader.Add(new TextBlock { Text = "值", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = TextSec });

        var rowsPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0, Margin = new Thickness(24, 0, 24, 0) };

        void AddRow(string key = "", string val = "")
        {
            var keyBox = new TextBox { Text = key, FontSize = 12, Width = 220, Margin = new Thickness(0, 0, 8, 0), Foreground = KeyColor };
            var valBox = new TextBox { Text = val, FontSize = 12, Foreground = ValColor };
            rows.Add((keyBox, valBox));

            var delBtn = new Button { Width = 24, Height = 24, Padding = new Thickness(0) };
            delBtn.Content(new TextBlock { Text = "×", FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element)
                  .Background(Color.Transparent).Foreground(TextSec);
            var capturedRow = (keyBox, valBox);
            delBtn.Click += () => { rows.Remove(capturedRow); RebuildTabs(); };
            delBtn.MouseEnter += () => delBtn.Background(BgHover);
            delBtn.MouseLeave += () => delBtn.Background(Color.Transparent);

            var row = new DockPanel { Height = 34 };
            row.Add(delBtn.DockRight());
            row.Add(keyBox.DockLeft());
            row.Add(valBox);
            rowsPanel.Add(row);
            rowsPanel.Add(new Border { Height = 1, Background = BorderCol });
        }

        foreach (var kv in initialVars) AddRow(kv.Key, kv.Value);

        var addBtn = new Button { Height = 32, Padding = new Thickness(0) };
        addBtn.Content(new TextBlock { Text = "+ 添加变量", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center } as Element)
              .Background(Color.Transparent).Foreground(Accent);
        addBtn.Click += () => AddRow();

        var saveBtn = new Button { Height = 32, Width = 72, Padding = new Thickness(0) };
        saveBtn.Content(new TextBlock { Text = "保存", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element)
               .Background(Accent).Foreground(Color.White);
        saveBtn.Click      += () => onSave();
        saveBtn.MouseEnter += () => saveBtn.Background(Color.FromRgb(0x1F, 0x8A, 0xD4));
        saveBtn.MouseLeave += () => saveBtn.Background(Accent);

        var bottomBar = new DockPanel { Height = 40, Margin = new Thickness(24, 8, 24, 0) };
        bottomBar.Add(saveBtn.DockRight());
        bottomBar.Add(addBtn);

        var content = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        content.Add(colHeader);
        content.Add(new Border { Height = 1, Background = BorderCol, Margin = new Thickness(24, 0) });
        content.Add(rowsPanel);
        content.Add(bottomBar);
        content.Add(new Border { Margin = new Thickness(24, 0, 24, 0), Child = new TextBlock { Text = $"来源：{sourceNote}", FontSize = 10, Foreground = TextSec, Margin = new Thickness(0, 6, 0, 0) } });
        return new ScrollViewer { Content = content };
    }

    private static UIElement BuildHint(string text) => new Border
    {
        Child = new TextBlock { Text = text, FontSize = 13, Foreground = Color.FromRgb(0x85, 0x85, 0x85), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
    };

    // ─────────────────────────────────────────────────────────────
    // JSON 读写
    // ─────────────────────────────────────────────────────────────

    private static Dictionary<string, string> LoadVars(string filePath, string envName)
    {
        if (!File.Exists(filePath)) return new();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            if (!doc.RootElement.TryGetProperty(envName, out var env) || env.ValueKind != JsonValueKind.Object) return new();
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in env.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.String)
                    result[prop.Name] = prop.Value.GetString() ?? string.Empty;
            return result;
        }
        catch { return new(); }
    }

    private static void SaveVars(string filePath, string envName, Dictionary<string, string> vars)
    {
        var all = new Dictionary<string, Dictionary<string, string>>();
        if (File.Exists(filePath))
            try { all = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(filePath)) ?? all; } catch { }
        all[envName] = vars;
        File.WriteAllText(filePath, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Dictionary<string, string> CollectRows(List<(TextBox Key, TextBox Val)> rows)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in rows)
        {
            var key = k.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(key)) dict[key] = v.Text ?? string.Empty;
        }
        return dict;
    }
}
