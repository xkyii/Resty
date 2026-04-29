using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Core.Environment;
using Resty.Gui.Services;

namespace Resty.Gui.Views;

/// <summary>
/// 集合侧边栏视图：显示工作区内的 .http 文件树和请求列表。
/// P7 新增：工作区名称标题 + 工作区/环境 切换 Tab。
/// P8 新增：环境列表 + 变量面板。
/// </summary>
public sealed class SidebarView
{
    // ── 色彩 ──────────────────────────────────────────────────────
    private static readonly Color BgPanel   = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color BgSurface = Color.FromRgb(0x37, 0x37, 0x38);
    private static readonly Color Accent    = Color.FromRgb(0x00, 0x7A, 0xCC);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);

    // 方法标签颜色（前景色）
    private static readonly Dictionary<string, Color> MethodColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET"]     = Color.FromRgb(0x61, 0xAF, 0xEF),
        ["POST"]    = Color.FromRgb(0x98, 0xC3, 0x79),
        ["PUT"]     = Color.FromRgb(0xE5, 0xC0, 0x7B),
        ["PATCH"]   = Color.FromRgb(0xD1, 0x9A, 0x66),
        ["DELETE"]  = Color.FromRgb(0xE0, 0x6C, 0x75),
        ["HEAD"]    = Color.FromRgb(0xAB, 0xB2, 0xBF),
        ["OPTIONS"] = Color.FromRgb(0xAB, 0xB2, 0xBF),
    };

    private static Color MethodColor(string method) =>
        MethodColors.TryGetValue(method, out var c) ? c : Color.FromRgb(0xAB, 0xB2, 0xBF);

    // ── 字段 ──────────────────────────────────────────────────────
    private readonly TextBox     _searchBox;
    private readonly StackPanel  _treeContainer;
    private readonly DockPanel   _root;
    private WorkspaceService?    _workspace;

    // P7: 标题 + 切换 Tab
    private readonly TextBlock _workspaceNameLabel;
    private readonly Button    _collectionTabBtn;
    private readonly Button    _envTabBtn;
    private readonly Border    _collectionTabLine;
    private readonly Border    _envTabLine;
    private readonly Border    _contentArea;           // 切换：集合内容 ↔ 环境面板
    private readonly UIElement _collectionContent;
    private readonly DockPanel _envContent;

    // P8: 环境面板
    private readonly StackPanel _envListPanel;
    private readonly StackPanel _envVarPanel;
    private bool   _isEnvMode    = false;
    private string _activeEnv    = string.Empty;   // 当前激活环境
    private string _selectedEnv  = string.Empty;   // 环境面板里高亮的行

    /// <summary>返回可嵌入父布局的 UIElement。</summary>
    public UIElement RootElement => _root;

    public event Action<HttpFileNode, RequestNode>? RequestSelected;

    /// <summary>用户在环境面板中选择了某个环境时触发。</summary>
    public event Action<string>? EnvSelected;

    public SidebarView()
    {
        // ── P7: 顶部标题行（工作区名 + "···"）────────────────────
        _workspaceNameLabel = new TextBlock
        {
            Text              = "无工作区",
            FontSize          = 11,
            FontWeight        = FontWeight.SemiBold,
            Foreground        = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(12, 0, 0, 0),
            TextTrimming      = TextTrimming.CharacterEllipsis,
        };

        var headerRow = new DockPanel { Height = 30 };
        headerRow.Add(_workspaceNameLabel);

        // ── P7: 切换 Tab 行（[工作区] [环境]）───────────────────
        _collectionTabLine = new Border { Height = 2, Background = Accent };
        var colLbl = new TextBlock { Text = "工作区", FontSize = 12, Foreground = TextPri, VerticalAlignment = VerticalAlignment.Center };
        var colDock = new DockPanel();
        colDock.Add(_collectionTabLine.DockBottom());
        colDock.Add(colLbl);
        _collectionTabBtn = new Button { Height = 32, Padding = new Thickness(14, 0) };
        _collectionTabBtn.Content(colDock as Element).Background(Color.Transparent);

        _envTabLine = new Border { Height = 2, Background = Color.Transparent };
        var envLbl = new TextBlock { Text = "环境", FontSize = 12, Foreground = TextSec, VerticalAlignment = VerticalAlignment.Center };
        var envDock = new DockPanel();
        envDock.Add(_envTabLine.DockBottom());
        envDock.Add(envLbl);
        _envTabBtn = new Button { Height = 32, Padding = new Thickness(14, 0) };
        _envTabBtn.Content(envDock as Element).Background(Color.Transparent);

        _collectionTabBtn.Click += () => SwitchMode(false);
        _envTabBtn.Click        += () => SwitchMode(true);

        var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        tabRow.Add(_collectionTabBtn);
        tabRow.Add(_envTabBtn);

        var tabDock = new DockPanel();
        tabDock.Add(new Border { Height = 1, Background = BorderCol }.DockBottom());
        tabDock.Add(tabRow);

        var tabBorder = new Border
        {
            Height = 33,
            Child  = tabDock,
        };

        // ── 集合内容（搜索框 + 文件树）───────────────────────────
        _searchBox = new TextBox
        {
            Placeholder       = "搜索请求…",
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _searchBox.TextChanged += _ => RefreshTree();

        var searchBorder = new Border
        {
            Height  = 34,
            Padding = new Thickness(8, 4),
            Child   = _searchBox,
        };

        _treeContainer = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        var treeScroll = new ScrollViewer { Content = _treeContainer };

        var collectionDock = new DockPanel();
        collectionDock.Add(searchBorder.DockTop());
        collectionDock.Add(treeScroll);
        _collectionContent = collectionDock;

        // ── P8: 环境面板（环境列表 + 变量展示）──────────────────
        _envListPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        _envVarPanel  = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing     = 0,
            Margin      = new Thickness(0, 4, 0, 0),
        };

        var envListScroll = new ScrollViewer { Content = BuildEnvPanelContent() };
        _envContent = new DockPanel();
        _envContent.Add(envListScroll);

        // ── 内容区（默认集合模式）────────────────────────────────
        _contentArea = new Border { Child = _collectionContent };

        // ── Root ────────────────────────────────────────────────
        _root = new DockPanel();
        _root.Add(new Border { Height = 30, Background = BgPanel, Child = headerRow }.DockTop());
        _root.Add(tabBorder.DockTop());
        _root.Add(_contentArea);
    }

    // ── 切换集合/环境模式 ────────────────────────────────────────
    private void SwitchMode(bool toEnv)
    {
        _isEnvMode = toEnv;

        _collectionTabLine.Background = toEnv ? Color.Transparent : Accent;
        _envTabLine.Background        = toEnv ? Accent : Color.Transparent;
        _collectionTabBtn.Foreground(toEnv ? TextSec : TextPri);
        _envTabBtn.Foreground(toEnv ? TextPri : TextSec);

        if (toEnv)
        {
            RefreshEnvPanel();
            _contentArea.Child = _envContent;
        }
        else
        {
            _contentArea.Child = _collectionContent;
        }
    }

    // ── 构建环境面板整体布局 ────────────────────────────────────
    private UIElement BuildEnvPanelContent()
    {
        var sp = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        sp.Add(_envListPanel);
        sp.Add(_envVarPanel);
        return sp;
    }

    // ── 刷新环境面板 ─────────────────────────────────────────────
    private void RefreshEnvPanel()
    {
        _envListPanel.Clear();
        _envVarPanel.Clear();

        if (_workspace is null || _workspace.AvailableEnvironments.Count == 0)
        {
            _envListPanel.Add(new TextBlock
            {
                Text       = "未找到环境配置\nhttp-client.env.json",
                FontSize   = 12,
                Foreground = TextSec,
                Margin     = new Thickness(16, 12),
            });
            return;
        }

        // 环境列表
        var header = new TextBlock
        {
            Text       = "环境",
            FontSize   = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextSec,
            Margin     = new Thickness(12, 10, 0, 4),
        };
        _envListPanel.Add(header);

        foreach (var env in _workspace.AvailableEnvironments)
        {
            var isActive   = env == _activeEnv;
            var isSelected = env == _selectedEnv;

            var dot = new TextBlock
            {
                Text       = "●",
                FontSize   = 8,
                Foreground = isActive ? Color.FromRgb(0x4E, 0xC9, 0xB0) : Color.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin     = new Thickness(0, 0, 4, 0),
            };

            var label = new TextBlock
            {
                Text              = env,
                FontSize          = 13,
                Foreground        = isSelected ? Color.FromRgb(0x4F, 0xC1, 0xFF) : TextPri,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming      = TextTrimming.CharacterEllipsis,
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing     = 0,
                Padding     = new Thickness(12, 0, 8, 0),
            };
            row.Add(dot);
            row.Add(label);

            var capturedEnv = env;
            var btn = new Button { Height = 30, Padding = new Thickness(0) };
            btn.Content(row as Element).Background(Color.Transparent);
            btn.Click += () =>
            {
                _selectedEnv = capturedEnv;
                _activeEnv   = capturedEnv;
                EnvSelected?.Invoke(capturedEnv);
                RefreshEnvPanel();
            };
            btn.MouseEnter += () => btn.Background(Color.FromRgb(0x2A, 0x2D, 0x2E));
            btn.MouseLeave += () => btn.Background(Color.Transparent);

            _envListPanel.Add(btn);
        }

        // 变量展示区
        ShowEnvVars(_selectedEnv.Length > 0 ? _selectedEnv : _activeEnv);
    }

    private void ShowEnvVars(string envName)
    {
        _envVarPanel.Clear();
        if (_workspace is null || string.IsNullOrEmpty(envName)) return;

        try
        {
            var fakePath = Path.Combine(_workspace.WorkspacePath, "dummy.http");
            var resolver = EnvironmentResolver.Load(fakePath, envName);
            if (resolver.Variables.Count == 0) return;

            _envVarPanel.Add(new Border
            {
                Height     = 1,
                Background = BorderCol,
                Margin     = new Thickness(0, 6, 0, 0),
            });

            var varHeader = new TextBlock
            {
                Text       = $"变量 ({envName})",
                FontSize   = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = TextSec,
                Margin     = new Thickness(12, 6, 0, 4),
            };
            _envVarPanel.Add(varHeader);

            foreach (var kv in resolver.Variables)
            {
                var keyLabel = new TextBlock
                {
                    Text      = kv.Key,
                    FontSize  = 11,
                    Foreground = Color.FromRgb(0x9C, 0xDC, 0xFE),
                    Margin    = new Thickness(12, 2, 4, 2),
                };
                var valLabel = new TextBlock
                {
                    Text         = kv.Value,
                    FontSize     = 11,
                    Foreground   = Color.FromRgb(0xCE, 0x91, 0x78),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin       = new Thickness(0, 2, 8, 2),
                };
                var varRow = new DockPanel { Height = 20 };
                varRow.Add(keyLabel.DockLeft());
                varRow.Add(valLabel);
                _envVarPanel.Add(varRow);
            }
        }
        catch { }
    }

    /// <summary>加载工作区数据并刷新。</summary>
    public void SetWorkspace(WorkspaceService workspace)
    {
        _workspace = workspace;
        _workspaceNameLabel.Text = workspace.WorkspaceName;
        _workspaceNameLabel.Foreground = TextPri;
        if (_isEnvMode) RefreshEnvPanel();
        else RefreshTree();
    }

    /// <summary>设置当前激活环境（用于环境面板高亮）。</summary>
    public void SetCurrentEnv(string env)
    {
        _activeEnv = env;
        if (string.IsNullOrEmpty(_selectedEnv)) _selectedEnv = env;
        if (_isEnvMode) RefreshEnvPanel();
    }

    // ── 内部刷新（集合树）────────────────────────────────────────
    private void Refresh() => RefreshTree();  // 兼容旧调用

    private void RefreshTree()
    {
        _treeContainer.Clear();
        if (_workspace is null) return;

        var filter = _searchBox.Text?.Trim() ?? string.Empty;

        foreach (var file in _workspace.Files)
        {
            var matchingRequests = string.IsNullOrEmpty(filter)
                ? file.Requests
                : file.Requests.Where(r => r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!matchingRequests.Any() && !string.IsNullOrEmpty(filter)) continue;

            _treeContainer.Add(BuildFileNode(file, matchingRequests));
        }

        if (_treeContainer.Children.Count == 0 && !string.IsNullOrEmpty(filter))
        {
            _treeContainer.Add(new TextBlock
            {
                Text = "无匹配请求",
                FontSize = 12,
                Foreground = Color.FromRgb(0x85, 0x85, 0x85),
                Margin = new Thickness(16, 8),
            });
        }
    }

    private Element BuildFileNode(HttpFileNode file, IEnumerable<RequestNode> requests)
    {
        var expanded = new ObservableValue<bool>(true);

        // ── 文件行 ───────────────────────────────────────────────
        var chevron = new TextBlock
        {
            FontSize  = 10,
            Foreground = Color.FromRgb(0x85, 0x85, 0x85),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0),
        };
        chevron.BindText(expanded, v => v ? "▾" : "▸");

        var fileLabel = new TextBlock
        {
            Text      = file.FileName,
            FontSize  = 13,
            Foreground = Color.FromRgb(0xCC, 0xCC, 0xCC),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };

        var fileRowContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            Padding = new Thickness(8, 0),
        };
        fileRowContent.Add(chevron);
        fileRowContent.Add(fileLabel);

        var fileRow = new Button { Height = 28 };
        fileRow.Content(fileRowContent as Element)
               .Background(Color.Transparent)
               .Foreground(Color.FromRgb(0xCC, 0xCC, 0xCC))
               .Padding(0, 0);
        fileRow.Click += () => expanded.Value = !expanded.Value;

        // 文件节点右键菜单
        fileRow.ContextMenu(new ContextMenu()
            .Item("在资源管理器中显示", () =>
            {
                try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.FilePath}\""); } catch { }
            })
            .Item("复制路径", () => CopyToClipboard(file.FilePath)));

        // ── 子请求列表 ───────────────────────────────────────────
        var childPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };
        childPanel.BindIsVisible(expanded);

        foreach (var req in requests)
        {
            childPanel.Add(BuildRequestNode(file, req));
        }

        // ── 组合 ─────────────────────────────────────────────────
        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };
        container.Add(fileRow);
        container.Add(childPanel);
        return container;
    }

    private Element BuildRequestNode(HttpFileNode file, RequestNode req)
    {
        // 方法标签
        var badge = new Border
        {
            Padding       = new Thickness(4, 1),
            CornerRadius  = 3,
            Background    = MethodColor(req.Method).WithAlpha((byte)46),
            Child = new TextBlock
            {
                Text       = req.Method,
                FontSize   = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = MethodColor(req.Method),
            },
        };

        var nameLabel = new TextBlock
        {
            Text      = req.Name,
            FontSize  = 12,
            Foreground = Color.FromRgb(0xCC, 0xCC, 0xCC),
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(6, 0, 0, 0),
            TextTrimming  = TextTrimming.CharacterEllipsis,
        };

        var rowContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            Padding = new Thickness(24, 0, 8, 0),
        };
        rowContent.Add(badge);
        rowContent.Add(nameLabel);

        var row = new Button { Height = 26 };
        row.Content(rowContent as Element)
           .Background(Color.Transparent)
           .Foreground(Color.FromRgb(0xCC, 0xCC, 0xCC))
           .Padding(0, 0);
        row.Click += () => RequestSelected?.Invoke(file, req);

        // 请求节点右键菜单
        row.ContextMenu(new ContextMenu()
            .Item("打开", () => RequestSelected?.Invoke(file, req))
            .Item("复制请求名称", () => CopyToClipboard(req.Name)));
        return row;
    }

    // ── 辅助方法 ──────────────────────────────────────────────────
    private static void CopyToClipboard(string text)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cmd", "/c clip")
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow  = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return;
            proc.StandardInput.Write(text);
            proc.StandardInput.Close();
            proc.WaitForExit(2000);
        }
        catch { }
    }
}
