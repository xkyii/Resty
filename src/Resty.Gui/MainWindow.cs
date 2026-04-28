using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Infrastructure;
using Resty.Gui.Services;
using Resty.Gui.Views;

namespace Resty.Gui;

/// <summary>
/// Resty 主窗口 — VS Code 式布局，无边框自定义标题栏。
/// G1: 工作区选择 + 侧边栏文件树 + 主区空壳（标签栏 + 占位内容）
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

    // 主区（标签页内容占位）
    private readonly TextBlock _contentPlaceholder;
    private readonly Border    _mainArea;

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

        // ── 主体内容 ──────────────────────────────────────────────
        _sidebar.RequestSelected += OnRequestSelected;

        _contentPlaceholder = new TextBlock
        {
            Text = "← 从侧边栏选择一个请求，或通过 文件 → 打开工作区 开始",
            FontSize  = 13,
            Foreground = TextSec,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };

        _mainArea = new Border
        {
            Background = BgBase,
            Child      = _contentPlaceholder,
        };

        // 初始显示欢迎界面
        Content = BuildWelcomeView();
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

        // 标签栏（G1 空壳）
        var addTabBtn = new Button { Width = 32, Height = 36 };
        addTabBtn.Content("+", false).FontSize(16).Background(Color.Transparent).Foreground(TextSec);

        var noTabLabel = new TextBlock
        {
            Text      = "无打开的请求",
            FontSize  = 12,
            Foreground = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(12, 0),
        };

        var tabBarPanel = new DockPanel();
        tabBarPanel.Add(addTabBtn.DockRight());
        tabBarPanel.Add(noTabLabel);

        var tabBar = new Border
        {
            Height      = 36,
            Background  = BgPanel,
            BorderBrush = BorderCol,
            Child       = tabBarPanel,
        };

        // 响应区占位
        var responsePlaceholder = new Border
        {
            Background = BgBase,
            Child = new TextBlock
            {
                Text      = "发送请求后响应将显示在此处",
                FontSize  = 12,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };

        // 请求编辑区 + 响应区（上下 SplitPanel）
        var editorAndResponse = new SplitPanel
        {
            Orientation      = Orientation.Vertical,
            FirstLength      = 350,
            SplitterThickness = 4,
            First            = _mainArea,
            Second           = responsePlaceholder,
        };

        // 右侧主区（标签栏 + 内容）
        var rightArea = new DockPanel();
        rightArea.Add(tabBar.DockTop());
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

        var envLabel = new TextBlock
        {
            Text      = "dev ▾",
            FontSize  = 12,
            Foreground = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(0, 0, 8, 0),
        };

        var bar = new DockPanel { Height = 22 };
        bar.Add(settingsBtn.DockLeft());
        bar.Add(envLabel.DockRight());
        bar.Add(readyLabel);
        return new Border { Height = 22, Background = Color.FromRgb(0x25, 0x25, 0x26), Child = bar };
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
        Content = BuildWorkspaceLayout();
    }

    private void OnRequestSelected(HttpFileNode file, RequestNode req)
    {
        // G1 占位：仅更新内容区提示文字
        _contentPlaceholder.Text = $"{req.Method}  {req.Name}\n文件：{file.FileName}\n\n(G2 实现请求编辑器)";
        _mainArea.Child          = _contentPlaceholder;
    }
}
