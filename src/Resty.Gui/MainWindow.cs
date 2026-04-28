using System.Threading;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Core.Assertions;
using Resty.Core.Environment;
using Resty.Core.Execution;
using Resty.Gui.Infrastructure;
using Resty.Gui.Services;
using Resty.Gui.Views;

namespace Resty.Gui;

/// <summary>
/// Resty 主窗口 — VS Code 式布局，无边框自定义标题栏。
/// G2: 请求编辑器（文本模式）+ 发送 + 响应面板
/// </summary>
public sealed class MainWindow : NativeCustomWindow
{
    // ── 颜色常量（VS Code Dark theme）────────────────────────────
    private static readonly Color BgBase     = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color BgPanel    = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color BgSidebar  = Color.FromRgb(0x25, 0x25, 0x26);
    private static readonly Color BgSurface  = Color.FromRgb(0x37, 0x37, 0x38);
    private static readonly Color Accent     = Color.FromRgb(0x00, 0x7A, 0xCC);
    private static readonly Color TextPri    = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec    = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol  = Color.FromRgb(0x3E, 0x3E, 0x42);

    // ── 状态 ─────────────────────────────────────────────────────
    private readonly WorkspaceService  _workspace = new();
    private readonly SidebarView       _sidebar   = new();
    private readonly ObservableValue<string> _workspaceName = new("Resty");

    // G2 组件
    private readonly RequestEditorView  _editor        = new();
    private readonly ResponsePanelView  _responsePanel = new();
    private readonly HttpRequestExecutor _executor     = new(timeoutMs: 30_000);

    // 主区容器（切换欢迎页 ↔ 编辑器）
    private readonly Border _mainArea;
    private string _currentEnv = string.Empty;
    private Button? _envBtn;

    public MainWindow()
    {
        this.Resizable(1280, 800, minWidth: 800, minHeight: 600)
            .StartCenterScreen();

        // 标题跟工作区名同步
        _workspaceName.Changed += () => this.Title = _workspaceName.Value;
        this.Title = _workspaceName.Value;

        // 背景色
        Background = BgPanel;

        // ── 标题栏左：MenuBar ─────────────────────────────────────
        TitleBarLeft.Add(BuildMenuBar());

        // ── 侧边栏事件 ───────────────────────────────────────────
        _sidebar.RequestSelected += OnRequestSelected;

        // ── 编辑器发送事件 ────────────────────────────────────────
        _editor.SendRequested = req =>
        {
            var sc            = SynchronizationContext.Current;
            var envName       = _currentEnv;
            var workspacePath = _workspace.WorkspacePath;
            _responsePanel.ShowLoading();
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    // 环境变量解析
                    var resolvedReq = req;
                    if (!string.IsNullOrEmpty(envName) && !string.IsNullOrEmpty(workspacePath))
                    {
                        var fakePath = Path.Combine(workspacePath, "dummy.http");
                        var resolver = EnvironmentResolver.Load(fakePath, envName);
                        resolvedReq = resolver.ApplyTo(req);
                    }
                    var result = await _executor.ExecuteAsync(resolvedReq);
                    // 断言评估
                    var assertions = req.Assertions.Count > 0
                        ? AssertionEngine.Evaluate(req.Assertions, result)
                        : null;
                    sc?.Post(_ => _responsePanel.ShowResult(result, assertions), null);
                }
                catch (Exception ex)
                {
                    sc?.Post(_ => _responsePanel.ShowError(ex.Message), null);
                }
            });
        };

        // ── 主区容器（初始显示欢迎页） ────────────────────────────
        _mainArea = new Border
        {
            Background = BgBase,
            Child      = BuildWelcomeView(),
        };

        // 初始布局
        Content = _mainArea;
        Padding = new Thickness(0);
    }

    // ── 欢迎视图 ─────────────────────────────────────────────────
    private UIElement BuildWelcomeView()
    {
        var openBtn = new Button()
            .Content("打开工作区文件夹…", false)
            .Padding(20, 10)
            .FontSize(14)
            .Background(Accent)
            .Foreground(Color.White)
            .OnClick(OpenWorkspace);

        var titleLabel = new TextBlock
        {
            Text      = "Resty",
            FontSize  = 32,
            FontWeight = FontWeight.Bold,
            Foreground = TextPri,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var subtitleLabel = new TextBlock
        {
            Text      = "本地优先的 HTTP API 客户端",
            FontSize  = 14,
            Foreground = TextSec,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var sp = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        sp.Add(titleLabel);
        sp.Add(subtitleLabel);
        sp.Add(openBtn);

        return new Border
        {
            Background = BgBase,
            Child = sp,
        };
    }

    // ── 主布局（工作区加载后）────────────────────────────────────
    private UIElement BuildWorkspaceLayout()
    {
        _sidebar.SetWorkspace(_workspace);

        // 重置编辑器和响应面板状态
        _responsePanel.ShowEmpty();

        // 请求编辑区 + 响应区（上下 SplitPanel）
        var editorAndResponse = new SplitPanel
        {
            Orientation      = Orientation.Vertical,
            FirstLength      = 360,
            SplitterThickness = 4,
            First            = _mainArea,
            Second           = _responsePanel.RootElement,
        };

        // 右侧主区（直接内容，无标签栏）
        var rightArea = new DockPanel();
        rightArea.Add(editorAndResponse);

        // 侧边栏 + 右侧（水平 SplitPanel）
        var sidebarBorder = new Border
        {
            Background = BgSidebar,
            Child      = _sidebar.RootElement,
        };

        var bodyPanel = new SplitPanel
        {
            Orientation       = Orientation.Horizontal,
            FirstLength       = 260,
            SplitterThickness = 4,
            First             = sidebarBorder,
            Second            = rightArea,
        };

        // 状态栏
        var statusbar = BuildStatusBar();

        // 整体 DockPanel
        var root = new DockPanel();
        root.Add(statusbar.DockBottom());
        root.Add(bodyPanel);
        return root;
    }

    // ── 状态栏 ───────────────────────────────────────────────────
    private UIElement BuildStatusBar()
    {
        var settingsBtn = new Button { Height = 22, Padding = new Thickness(8, 0) };
        settingsBtn.Content("⚙ 设置", false).FontSize(12).Background(Color.Transparent).Foreground(TextSec);

        var readyLabel = new TextBlock
        {
            Text      = "◎ 就绪",
            FontSize  = 12,
            Foreground = Color.FromRgb(0x4E, 0xC9, 0xB0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(8, 0),
        };

        _envBtn = new Button { Height = 22, Padding = new Thickness(8, 0) };
        _envBtn.Content(string.IsNullOrEmpty(_currentEnv) ? "无环境" : $"{_currentEnv} ▾", false)
               .FontSize(12).Background(Color.Transparent).Foreground(TextSec)
               .OnClick(CycleEnv);

        var bar = new DockPanel { Height = 22 };
        bar.Add(settingsBtn.DockLeft());
        bar.Add(_envBtn.DockRight());
        bar.Add(readyLabel);
        return new Border { Height = 22, Background = Color.FromRgb(0x25, 0x25, 0x26), Child = bar };
    }
    private void CycleEnv()
    {
        var envs = _workspace.AvailableEnvironments;
        if (envs.Count == 0) return;
        var idx = 0;
        for (var i = 0; i < envs.Count; i++)
            if (envs[i] == _currentEnv) { idx = i; break; }
        _currentEnv = envs[(idx + 1) % envs.Count];
        _envBtn?.Content(_currentEnv + " ▾", false);
    }
    // ── 菜单栏 ───────────────────────────────────────────────────
    private UIElement BuildMenuBar()
    {
        var fileMenu = new Menu()
            .Item("打开工作区…",   OpenWorkspace,       shortcut: new KeyGesture(Key.O, ModifierKeys.Primary))
            .Separator()
            .Item("新建请求文件", () => { },            shortcut: new KeyGesture(Key.N, ModifierKeys.Primary))
            .Separator()
            .Item("保存",         () => { },            shortcut: new KeyGesture(Key.S, ModifierKeys.Primary))
            .Separator()
            .Item("退出",         () => Application.Quit(), shortcut: new KeyGesture(Key.F4, ModifierKeys.Alt));

        var viewMenu = new Menu()
            .Item("切换侧边栏", () => { }, shortcut: new KeyGesture(Key.B, ModifierKeys.Primary));

        var runMenu = new Menu()
            .Item("发送请求",  () => { }, shortcut: new KeyGesture(Key.Enter, ModifierKeys.Primary))
            .Item("取消请求",  () => { });

        var helpMenu = new Menu()
            .Item("HTTP 语法参考…", () => { })
            .Separator()
            .Item("关于 Resty",    () => { });

        var bar = new MenuBar();
        bar.Background = Color.Transparent;
        bar.Add(new MenuItem("\u6587\u4ef6(_F)").Menu(fileMenu));
        bar.Add(new MenuItem("\u89c6\u56fe(_V)").Menu(viewMenu));
        bar.Add(new MenuItem("\u8fd0\u884c(_R)").Menu(runMenu));
        bar.Add(new MenuItem("\u5e2e\u52a9(_H)").Menu(helpMenu));
        return bar;
    }

    // ── 事件处理 ─────────────────────────────────────────────────
    private void OpenWorkspace()
    {
        var folder = FileDialog.SelectFolder(new FolderDialogOptions { Owner = Handle });
        if (folder is null) return;
        LoadWorkspace(folder);
    }

    private void OpenWorkspace(object? _ = null)  // Menu.Item callback 签名兼容
    {
        OpenWorkspace();
    }

    private void LoadWorkspace(string path)
    {
        _workspace.Load(path);
        _workspaceName.Value = _workspace.WorkspaceName;
        _currentEnv = _workspace.AvailableEnvironments.Count > 0
            ? _workspace.AvailableEnvironments[0]
            : string.Empty;
        _mainArea.Child = _editor.RootElement;
        Content = BuildWorkspaceLayout();
    }

    private void OnRequestSelected(HttpFileNode file, RequestNode req)
    {
        var def = _workspace.GetRequestDefinition(file.FilePath, req.Name);
        if (def is null) return;

        _editor.Load(def);
        _mainArea.Child = _editor.RootElement;
        _responsePanel.ShowEmpty();
    }
}
