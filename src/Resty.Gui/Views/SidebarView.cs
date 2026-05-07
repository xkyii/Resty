using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Services;
using System.Runtime.InteropServices;

namespace Resty.Gui.Views;

/// <summary>
/// 集合侧边栏视图：顶部"工作区/环境"Tab 切换，
/// 工作区 Tab 显示请求树，环境 Tab 显示环境列表（变量详情在右侧主区域）。
/// </summary>
public sealed class SidebarView
{
    // P/Invoke 用于发送右键菜单消息
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private const uint WM_CONTEXTMENU = 0x007B;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy, mouseData;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    // ── 色彩 ──────────────────────────────────────────────────────
    private static readonly Color BgPanel   = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color BgHover   = Color.FromRgb(0x2A, 0x2D, 0x2E);
    private static readonly Color BgActive  = Color.FromRgb(0x04, 0x39, 0x5E);
    private static readonly Color Accent    = Color.FromRgb(0x00, 0x7A, 0xCC);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);
    private static readonly Color GreenDot  = Color.FromRgb(0x4E, 0xC9, 0xB0);

    // 方法标签颜色
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
    private static Color MethodColor(string m) =>
        MethodColors.TryGetValue(m, out var c) ? c : Color.FromRgb(0xAB, 0xB2, 0xBF);

    // ── 字段 ──────────────────────────────────────────────────────
    private WorkspaceService? _workspace;
    private bool   _isEnvMode   = false;
    private string _activeEnv   = string.Empty;
    private string _selectedEnv = string.Empty;
    private bool   _syncTabToSelection = false;  // 跟随打开标志
    private HttpFileNode? _activeFile;
    private RequestNode? _activeRequest;

    private readonly TextBox    _searchBox;
    private readonly StackPanel _treeContainer;
    private readonly StackPanel _envListPanel;
    private readonly Border     _contentArea;
    private readonly UIElement  _collectionContent;
    private readonly Border     _collectionTabLine;
    private readonly Border     _envTabLine;
    private readonly Button     _collectionTabBtn;
    private readonly Button     _envTabBtn;
    private readonly Button     _operationsBtn;
    private readonly DockPanel  _rootDock;
    private UIElement?          _envContentEl;

    // ── 公开 ──────────────────────────────────────────────────────
    public UIElement RootElement { get; }

    public event Action<HttpFileNode, RequestNode>? RequestSelected;
    public event Action<bool>? EnvModeChanged;
    public event Action<string>? EnvActivated;
    /// <summary>新建 .http 文件后触发，参数为文件绝对路径。</summary>
    public event Action<string>? NewFileCreated;

    /// <summary>设置活跃的请求，高京显示左侧树中对应的项。</summary>
    public void SetActiveRequest(HttpFileNode? file, RequestNode? request)
    {
        _activeFile = file;
        _activeRequest = request;
        // 刷新树形结构以更新高亮
        RefreshTree();
    }
    /// <summary>当跟随打开启用时，Tab 激活时触发，参数为要高亮的文件和请求。</summary>
    public event Action<HttpFileNode, RequestNode>? SyncTabActive;

    // ─────────────────────────────────────────────────────────────
    public SidebarView()
    {
        // Tab 行 — 图标按钮居中
        _collectionTabLine = new Border { Height = 2, Background = Accent };
        var colIcon = new TextBlock { Text = "☰", FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var colDock = new DockPanel();
        colDock.Add(_collectionTabLine.DockBottom());
        colDock.Add(colIcon);
        _collectionTabBtn = new Button { Width = 48, Height = 32, Padding = new Thickness(0) };
        _collectionTabBtn.Content(colDock as Element).Background(Color.Transparent).Foreground(TextPri);

        _envTabLine = new Border { Height = 2, Background = Color.Transparent };
        var envIcon = new TextBlock { Text = "◎", FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var envDock = new DockPanel();
        envDock.Add(_envTabLine.DockBottom());
        envDock.Add(envIcon);
        _envTabBtn = new Button { Width = 48, Height = 32, Padding = new Thickness(0) };
        _envTabBtn.Content(envDock as Element).Background(Color.Transparent).Foreground(TextSec);

        _collectionTabBtn.Click += () => SwitchMode(false);
        _envTabBtn.Click        += () => SwitchMode(true);

        // 操作菜单按钮
        _operationsBtn = BuildOperationsButton();

        // Tab 按钮居中，操作按钮靠右
        var tabsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, HorizontalAlignment = HorizontalAlignment.Center };
        tabsPanel.Add(_collectionTabBtn);
        tabsPanel.Add(_envTabBtn);

        var tabRow = new DockPanel { Height = 32 };
        tabRow.Add(_operationsBtn.DockRight());
        tabRow.Add(tabsPanel);

        var tabDock = new DockPanel();
        tabDock.Add(new Border { Height = 1, Background = BorderCol }.DockBottom());
        tabDock.Add(tabRow);

        // 集合内容
        _searchBox = new TextBox { Placeholder = "搜索请求…", FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        _searchBox.TextChanged += _ => RefreshTree();
        _treeContainer = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        var collectionDock = new DockPanel();
        collectionDock.Add(new Border { Height = 34, Padding = new Thickness(8, 4), Child = _searchBox }.DockTop());
        collectionDock.Add(new ScrollViewer { Content = _treeContainer });
        _collectionContent = collectionDock;

        // 环境列表
        _envListPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };

        _contentArea = new Border { Child = _collectionContent };

        _rootDock = new DockPanel();
        _rootDock.Add(new Border { Height = 33, Child = tabDock }.DockTop());
        _rootDock.Add(_contentArea);
        RootElement = _rootDock;
    }

    // ── 公开方法 ──────────────────────────────────────────────────
    public void SetWorkspace(WorkspaceService workspace)
    {
        _workspace = workspace;
        if (_isEnvMode) RefreshEnvList();
        else RefreshTree();
    }

    public void SetActiveEnv(string env)
    {
        _activeEnv = env;
        if (_isEnvMode) RefreshEnvList();
    }

    // ── Tab 切换 ──────────────────────────────────────────────────
    private void SwitchMode(bool toEnv)
    {
        _isEnvMode = toEnv;
        _collectionTabLine.Background = toEnv ? Color.Transparent : Accent;
        _envTabLine.Background        = toEnv ? Accent : Color.Transparent;
        _collectionTabBtn.Foreground(toEnv ? TextSec : TextPri);
        _envTabBtn.Foreground(toEnv ? TextPri : TextSec);

        if (toEnv)
        {
            RefreshEnvList();
            if (_envContentEl is null)
            {
                var dp  = new DockPanel();
                dp.Add(new ScrollViewer { Content = _envListPanel });
                _envContentEl = dp;
            }
            _contentArea.Child = _envContentEl;
        }
        else
        {
            _contentArea.Child = _collectionContent;
        }
        EnvModeChanged?.Invoke(toEnv);
    }

    // ── 环境列表 ──────────────────────────────────────────────────
    private void RefreshEnvList()
    {
        _envListPanel.Clear();
        if (_workspace is null || _workspace.AvailableEnvironments.Count == 0)
        {
            _envListPanel.Add(new TextBlock { Text = "未找到环境配置\nhttp-client.env.json", FontSize = 12, Foreground = TextSec, Margin = new Thickness(12, 16) });
            return;
        }
        foreach (var env in _workspace.AvailableEnvironments)
            _envListPanel.Add(BuildEnvRow(env));
    }

    private UIElement BuildEnvRow(string env)
    {
        var isActive   = env == _activeEnv;
        var isSelected = env == _selectedEnv;

        var dot = new TextBlock { Text = "●", FontSize = 8, Foreground = isActive ? GreenDot : Color.Transparent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        var label = new TextBlock { Text = env, FontSize = 13, Foreground = isActive ? TextPri : TextSec, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

        var capturedEnv = env;
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Padding = new Thickness(12, 0, 12, 0) };
        content.Add(dot);
        content.Add(label);

        var rowInner = new DockPanel { Height = 36 };
        rowInner.Add(content);

        var rowBorder = new Border { Background = isSelected ? BgActive : Color.Transparent, Child = rowInner };
        rowBorder.MouseEnter += () => { if (env != _selectedEnv) rowBorder.Background = BgHover; };
        rowBorder.MouseLeave += () => { if (env != _selectedEnv) rowBorder.Background = Color.Transparent; };

        var btn = new Button { Height = 36, Padding = new Thickness(0) };
        btn.Content(rowBorder as Element).Background(Color.Transparent);
        btn.Click += () =>
        {
            _selectedEnv = capturedEnv;
            _activeEnv   = capturedEnv;
            EnvActivated?.Invoke(capturedEnv);
            RefreshEnvList();
        };
        // 右键菜单：重命名、删除
        btn.ContextMenu(new ContextMenu()
            .Item("重命名", () => BeginRenameEnv(capturedEnv))
            .Item("删除", () => DeleteEnv(capturedEnv)));
        return btn;
    }

    private void BeginNewEnv()
    {
        if (_workspace is null) return;
        var nameBox = new TextBox { Placeholder = "输入环境名称…", FontSize = 12, Margin = new Thickness(12, 4, 4, 4) };
        var confirmBtn = new Button { Width = 28, Height = 28, Padding = new Thickness(0) };
        confirmBtn.Content(new TextBlock { Text = "✓", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element).Background(Accent).Foreground(Color.White);
        var cancelBtn = new Button { Width = 28, Height = 28, Padding = new Thickness(0) };
        cancelBtn.Content(new TextBlock { Text = "✕", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element).Background(Color.Transparent).Foreground(TextSec);
        var newRow = new DockPanel { Height = 36 };
        newRow.Add(cancelBtn.DockRight());
        newRow.Add(confirmBtn.DockRight());
        newRow.Add(nameBox);
        _envListPanel.Clear();
        _envListPanel.Add(new Border { Background = BgActive, Child = newRow });
        if (_workspace is not null)
            foreach (var e in _workspace.AvailableEnvironments)
                _envListPanel.Add(BuildEnvRow(e));
        confirmBtn.Click += () => { var n = nameBox.Text?.Trim() ?? string.Empty; if (!string.IsNullOrEmpty(n)) CreateEnv(n); };
        cancelBtn.Click  += RefreshEnvList;
    }

    private void BeginRenameEnv(string oldName)
    {
        if (_workspace is null) return;
        var nameBox = new TextBox { Text = oldName, FontSize = 12, Margin = new Thickness(12, 4, 4, 4) };
        nameBox.SelectAll();
        var confirmBtn = new Button { Width = 28, Height = 28, Padding = new Thickness(0) };
        confirmBtn.Content(new TextBlock { Text = "✓", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element).Background(Accent).Foreground(Color.White);
        var cancelBtn = new Button { Width = 28, Height = 28, Padding = new Thickness(0) };
        cancelBtn.Content(new TextBlock { Text = "✕", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element).Background(Color.Transparent).Foreground(TextSec);
        var editRow = new DockPanel { Height = 36 };
        editRow.Add(cancelBtn.DockRight());
        editRow.Add(confirmBtn.DockRight());
        editRow.Add(nameBox);

        // 清空列表并重建，带有编辑行在最前面
        _envListPanel.Clear();
        _envListPanel.Add(new Border { Background = BgActive, Child = editRow });
        if (_workspace is not null)
            foreach (var e in _workspace.AvailableEnvironments)
                if (e != oldName)  // 跳过旧的环境名，稍后会被重建
                    _envListPanel.Add(BuildEnvRow(e));

        confirmBtn.Click += () => { var n = nameBox.Text?.Trim() ?? string.Empty; if (!string.IsNullOrEmpty(n) && n != oldName) RenameEnv(oldName, n); else RefreshEnvList(); };
        cancelBtn.Click  += RefreshEnvList;
    }

    private void RenameEnv(string oldName, string newName)
    {
        if (_workspace is null) return;
        RenameEnvInFile(System.IO.Path.Combine(_workspace.WorkspacePath, "http-client.env.json"), oldName, newName);
        RenameEnvInFile(System.IO.Path.Combine(_workspace.WorkspacePath, "http-client.private.env.json"), oldName, newName);
        _workspace.Load(_workspace.WorkspacePath);
        if (_selectedEnv == oldName) _selectedEnv = newName;
        if (_activeEnv == oldName) { _activeEnv = newName; EnvActivated?.Invoke(newName); }
        RefreshEnvList();
    }

    private static void RenameEnvInFile(string filePath, string oldName, string newName)
    {
        if (!System.IO.File.Exists(filePath)) return;
        try
        {
            var all = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>(System.IO.File.ReadAllText(filePath)) ?? new();
            if (all.TryGetValue(oldName, out var envValues))
            {
                all.Remove(oldName);
                all[newName] = envValues;
                System.IO.File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(all, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { }
    }

    private void CreateEnv(string envName)
    {
        if (_workspace is null) return;
        var filePath = System.IO.Path.Combine(_workspace.WorkspacePath, "http-client.env.json");
        try
        {
            var all = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();
            if (System.IO.File.Exists(filePath))
                try { all = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>(System.IO.File.ReadAllText(filePath)) ?? all; } catch { }
            if (!all.ContainsKey(envName))
            {
                all[envName] = [];
                System.IO.File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(all, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            _workspace.Load(_workspace.WorkspacePath);
            _selectedEnv = envName;
            _activeEnv   = envName;
            EnvActivated?.Invoke(envName);
            RefreshEnvList();
        }
        catch { }
    }

    private void DeleteEnv(string envName)
    {
        if (_workspace is null) return;
        RemoveEnvFromFile(System.IO.Path.Combine(_workspace.WorkspacePath, "http-client.env.json"), envName);
        RemoveEnvFromFile(System.IO.Path.Combine(_workspace.WorkspacePath, "http-client.private.env.json"), envName);
        _workspace.Load(_workspace.WorkspacePath);
        if (_selectedEnv == envName) _selectedEnv = string.Empty;
        if (_activeEnv == envName) { _activeEnv = string.Empty; EnvActivated?.Invoke(string.Empty); }
        RefreshEnvList();
    }

    private static void RemoveEnvFromFile(string filePath, string envName)
    {
        if (!System.IO.File.Exists(filePath)) return;
        try
        {
            var all = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>>(System.IO.File.ReadAllText(filePath)) ?? new();
            if (all.Remove(envName))
                System.IO.File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(all, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    // ── 集合树 ────────────────────────────────────────────────────
    private void BeginNewFile()
    {
        if (_workspace is null) return;
        var nameBox = new TextBox { Placeholder = "文件名 (不含 .http)…", FontSize = 12, Margin = new Thickness(12, 4, 4, 4) };
        var confirmBtn = new Button { Width = 28, Height = 28, Padding = new Thickness(0) };
        confirmBtn.Content(new TextBlock { Text = "✓", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element)
            .Background(Accent).Foreground(Color.White);
        var cancelBtn = new Button { Width = 28, Height = 28, Padding = new Thickness(0) };
        cancelBtn.Content(new TextBlock { Text = "✕", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element)
            .Background(Color.Transparent).Foreground(TextSec);
        var newRow = new DockPanel { Height = 36 };
        newRow.Add(cancelBtn.DockRight());
        newRow.Add(confirmBtn.DockRight());
        newRow.Add(nameBox);
        _treeContainer.Clear();
        _treeContainer.Add(new Border { Background = BgActive, Child = newRow });
        confirmBtn.Click += () => { var n = nameBox.Text?.Trim() ?? string.Empty; if (!string.IsNullOrEmpty(n)) CreateHttpFile(n); };
        cancelBtn.Click  += RefreshTree;
    }

    private void CreateHttpFile(string name)
    {
        if (_workspace is null) return;
        // 清理非法字符
        var safeName = string.Concat(name.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
        if (string.IsNullOrEmpty(safeName)) { RefreshTree(); return; }
        if (!safeName.EndsWith(".http", StringComparison.OrdinalIgnoreCase))
            safeName += ".http";
        var filePath = System.IO.Path.Combine(_workspace.WorkspacePath, safeName);
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                // 写入默认模板（含一个示例 GET 请求）
                System.IO.File.WriteAllText(filePath,
                    $"### New Request{Environment.NewLine}GET https://example.com{Environment.NewLine}{Environment.NewLine}");
            }
            _workspace.Load(_workspace.WorkspacePath);
            RefreshTree();
            NewFileCreated?.Invoke(filePath);
        }
        catch { RefreshTree(); }
    }

    private void BeginNewRequest(string filePath, HttpFileNode file)
    {
        if (_workspace is null) return;
        var nameBox = new TextBox { Placeholder = "请求名称…", FontSize = 12, Margin = new Thickness(12, 4, 4, 4) };
        var confirmBtn = new Button { Width = 28, Height = 28, Padding = new Thickness(0) };
        confirmBtn.Content(new TextBlock { Text = "✓", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element)
            .Background(Accent).Foreground(Color.White);
        var cancelBtn = new Button { Width = 28, Height = 28, Padding = new Thickness(0) };
        cancelBtn.Content(new TextBlock { Text = "✕", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } as Element)
            .Background(Color.Transparent).Foreground(TextSec);
        var newRow = new DockPanel { Height = 36 };
        newRow.Add(cancelBtn.DockRight());
        newRow.Add(confirmBtn.DockRight());
        newRow.Add(nameBox);
        confirmBtn.Click += () =>
        {
            var n = nameBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(n)) CreateRequest(filePath, n, file);
        };
        cancelBtn.Click  += RefreshTree;
        // 临时插入编辑行到树顶部
        _treeContainer.Insert(0, new Border { Background = BgActive, Child = newRow });
    }

    private void CreateRequest(string filePath, string requestName, HttpFileNode file)
    {
        if (_workspace is null) return;
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                var content = System.IO.File.ReadAllText(filePath);
                var newRequestText = $"\r\n### {requestName}\r\nGET https://example.com\r\n";
                System.IO.File.WriteAllText(filePath, content + newRequestText);
                _workspace.Load(_workspace.WorkspacePath);
                RefreshTree();
                // 自动打开新请求
                var newReq = file.Requests.FirstOrDefault(r => r.Name == requestName);
                if (newReq != null)
                    RequestSelected?.Invoke(file, newReq);
            }
        }
        catch { RefreshTree(); }
    }

    private void RefreshTree()
    {
        _treeContainer.Clear();
        if (_workspace is null) return;
        var filter = _searchBox.Text?.Trim() ?? string.Empty;
        foreach (var file in _workspace.Files)
        {
            var reqs = string.IsNullOrEmpty(filter)
                ? file.Requests
                : file.Requests.Where(r => r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!reqs.Any() && !string.IsNullOrEmpty(filter)) continue;
            _treeContainer.Add(BuildFileNode(file, reqs));
        }
        if (_treeContainer.Children.Count == 0 && !string.IsNullOrEmpty(filter))
            _treeContainer.Add(new TextBlock { Text = "无匹配请求", FontSize = 12, Foreground = TextSec, Margin = new Thickness(16, 8) });
    }

    private Element BuildFileNode(HttpFileNode file, IEnumerable<RequestNode> requests)
    {
        var expanded = new ObservableValue<bool>(true);
        var chevron = new TextBlock { FontSize = 10, Foreground = TextSec, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0) };
        chevron.BindText(expanded, v => v ? "▾" : "▸");
        var fileLabel = new TextBlock { Text = file.FileName, FontSize = 13, Foreground = TextPri, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        var fileRowContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Padding = new Thickness(8, 0) };
        fileRowContent.Add(chevron);
        fileRowContent.Add(fileLabel);
        var fileRow = new Button { Height = 28 };
        fileRow.Content(fileRowContent as Element).Background(Color.Transparent).Foreground(TextPri).Padding(0, 0);
        fileRow.Click += () => expanded.Value = !expanded.Value;
        var capturedFilePath = file.FilePath;
        fileRow.ContextMenu(new ContextMenu()
            .Item("新建请求", () => BeginNewRequest(capturedFilePath, file))
            .Item("在资源管理器中显示", () => { try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.FilePath}\""); } catch { } })
            .Item("复制路径", () => CopyToClipboard(file.FilePath)));
        var childPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        childPanel.BindIsVisible(expanded);
        foreach (var req in requests) childPanel.Add(BuildRequestNode(file, req));
        var container = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        container.Add(fileRow);
        container.Add(childPanel);
        return container;
    }

    private Element BuildRequestNode(HttpFileNode file, RequestNode req)
    {
        var isActive = _activeFile == file && _activeRequest == req;
        var badge = new Border
        {
            Padding = new Thickness(4, 1), CornerRadius = 3,
            Background = MethodColor(req.Method).WithAlpha((byte)46),
            Child = new TextBlock { Text = req.Method, FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = MethodColor(req.Method) },
        };
        var nameLabel = new TextBlock { Text = req.Name, FontSize = 12, Foreground = TextPri, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        var rowContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Padding = new Thickness(24, 0, 8, 0) };
        rowContent.Add(badge);
        rowContent.Add(nameLabel);
        var row = new Button { Height = 26 };
        row.Content(rowContent as Element).Background(isActive ? BgActive : Color.Transparent).Foreground(TextPri).Padding(0, 0);
        row.Click += () => RequestSelected?.Invoke(file, req);
        row.ContextMenu(new ContextMenu().Item("复制请求名称", () => CopyToClipboard(req.Name)));
        return row;
    }

    private Button BuildOperationsButton()
    {
        var icon = new TextBlock { Text = "···", FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = TextSec };
        var btn = new Button { Width = 48, Height = 32, Padding = new Thickness(0) };
        btn.Content(icon as Element).Background(Color.Transparent);

        btn.Click += () =>
        {
            var menu = new ContextMenu();
            if (!_isEnvMode)
            {
                menu.Item("新建HTTP请求", BeginNewFile);
                menu.Item(_syncTabToSelection ? "✓ 跟随打开" : "☐ 跟随打开", () =>
                {
                    _syncTabToSelection = !_syncTabToSelection;
                });
            }
            else
            {
                menu.Item("新建环境", BeginNewEnv);
            }
            btn.ContextMenu(menu);
            // 模拟右键点击来显示菜单
            try
            {
                var inputs = new INPUT[]
                {
                    new() { type = INPUT_MOUSE, mi = new() { dwFlags = MOUSEEVENTF_RIGHTDOWN } },
                    new() { type = INPUT_MOUSE, mi = new() { dwFlags = MOUSEEVENTF_RIGHTUP } }
                };
                SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            }
            catch { }
        };

        btn.MouseEnter += () => icon.Foreground = TextPri;
        btn.MouseLeave += () => icon.Foreground = TextSec;

        return btn;
    }
    private static void CopyToClipboard(string text)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cmd", "/c clip") { RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return;
            proc.StandardInput.Write(text);
            proc.StandardInput.Close();
            proc.WaitForExit(2000);
        }
        catch { }
    }
}
