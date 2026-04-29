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
    private RequestEditorView? _editor; // 当前激活的编辑器
    private readonly ResponsePanelView  _responsePanel = new();
    private readonly HttpRequestExecutor _executor     = new(timeoutMs: 30_000);

    // 主区容器（切换欢迎页 ↔ 编辑器）
    private readonly Border _mainArea;
    // 多标签支持
    private readonly StackPanel _tabBar;
    private readonly Border     _editorArea;
    private sealed record EditorTab(string Key, HttpFileNode File, RequestNode Request, RequestEditorView Editor, Button Btn);
    private readonly List<EditorTab> _tabs = [];
    private int _activeTabIdx = -1;
    private string _currentEnv = string.Empty;
    private Button? _envBtn;
    private ComboBox? _envCombo;
    private CancellationTokenSource? _currentCts;
    private TextBlock? _statusLabel;

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

        // ── 主区容器（层次：_mainArea > DockPanel > _tabBar + _editorArea）──
        _tabBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        _editorArea = new Border { Background = BgBase, Child = BuildNoRequestView() };

        var tabContainer = new DockPanel();
        tabContainer.Add(new Border { Height = 35, Background = BgPanel, Child = _tabBar }.DockTop());
        tabContainer.Add(_editorArea);

        _mainArea = new Border { Child = tabContainer };

        // 初始布局：不把 _mainArea 直接作为 Content，
        // 避免首次 LoadWorkspace 时 SplitPanel 接管 _mainArea 产生父节点冲突。
        Content = BuildWelcomeView();
        Padding = new Thickness(0);
    }

    // ── 编辑器事件绑定 ─────────────────────────────────────
    private void WireEditorEvents(RequestEditorView editor)
    {
        editor.CancelRequested = () => { try { _currentCts?.Cancel(); } catch (ObjectDisposedException) { _currentCts = null; } };
        editor.SaveRequested = (filePath, def) => _workspace.SaveRequest(filePath, def);
        editor.SendRequested = req =>
        {
            var sc            = SynchronizationContext.Current;
            var envName       = _currentEnv;
            var workspacePath = _workspace.WorkspacePath;
            var capturedEditor = editor;
            _currentCts?.Cancel();
            _currentCts = new CancellationTokenSource();
            var cts = _currentCts;
            _responsePanel.ShowLoading();
            capturedEditor.SetSendingState(true);
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var resolvedReq = req;
                    if (!string.IsNullOrEmpty(envName) && !string.IsNullOrEmpty(workspacePath))
                    {
                        var fakePath = Path.Combine(workspacePath, "dummy.http");
                        var resolver = EnvironmentResolver.Load(fakePath, envName);
                        resolvedReq = resolver.ApplyTo(req);
                    }
                    var result = await _executor.ExecuteAsync(resolvedReq, cts.Token);
                    var assertions = req.Assertions.Count > 0
                        ? AssertionEngine.Evaluate(req.Assertions, result)
                        : null;
                    sc?.Post(_ =>
                    {
                        capturedEditor.SetSendingState(false);
                        _responsePanel.ShowResult(result, assertions);
                        UpdateStatusBar(result.StatusCode, result.ElapsedMs,
                            System.Text.Encoding.UTF8.GetByteCount(result.Body));
                    }, null);
                }
                catch (OperationCanceledException)
                {
                    sc?.Post(_ => { capturedEditor.SetSendingState(false); _responsePanel.ShowEmpty(); }, null);
                }
                catch (Exception ex)
                {
                    sc?.Post(_ => { capturedEditor.SetSendingState(false); _responsePanel.ShowError(ex.Message); }, null);
                }
                finally
                {
                    cts.Dispose();
                    // 避免下次 Cancel() 作用在已销毁的 CTS 上（ObjectDisposedException）
                    if (ReferenceEquals(_currentCts, cts)) _currentCts = null;
                }
            });
        };
    }

    // ── 标签管理 ─────────────────────────────────────
    private void ActivateTab(int idx)
    {
        if (idx < 0 || idx >= _tabs.Count) return;
        // 更新按钮样式
        for (int i = 0; i < _tabs.Count; i++)
        {
            bool active = i == idx;
            _tabs[i].Btn.Background(active ? BgBase : Color.Transparent)
                        .Foreground(active ? TextPri : TextSec);
        }
        _activeTabIdx = idx;
        _editor = _tabs[idx].Editor;
        _editorArea.Child = _editor.RootElement;
    }

    private void CloseTab(int idx)
    {
        if (idx < 0 || idx >= _tabs.Count) return;
        var tab = _tabs[idx];
        _tabBar.Remove(tab.Btn);
        _tabs.RemoveAt(idx);
        if (_tabs.Count == 0)
        {
            _activeTabIdx = -1;
            _editor = null;
            _editorArea.Child = BuildNoRequestView();
        }
        else
        {
            var newIdx = Math.Min(idx, _tabs.Count - 1);
            ActivateTab(newIdx);
        }
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

    /// <summary>工作区已打开但尚未选择请求时，编辑区显示的占位视图。</summary>
    private UIElement BuildNoRequestView() => new Border
    {
        Background = BgBase,
        Child = new TextBlock
        {
            Text      = "← 从左侧边栏选择一个请求",
            FontSize  = 14,
            Foreground = TextSec,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        },
    };

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

        _statusLabel = new TextBlock
        {
            Text      = "◎ 就绪",
            FontSize  = 12,
            Foreground = Color.FromRgb(0x4E, 0xC9, 0xB0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(8, 0),
        };

        _envBtn = new Button { Height = 22, Padding = new Thickness(8, 0) };
        _envBtn.Content(string.IsNullOrEmpty(_currentEnv) ? "无环境" : $"{_currentEnv} ▾", false)
               .FontSize(12).Background(Color.Transparent).Foreground(TextSec);

        _envCombo = new ComboBox { Height = 20, MinWidth = 80 };
        _envCombo.FontSize(11);
        RebuildEnvCombo();
        _envCombo.OnSelectionChanged(o =>
        {
            if (o is string s) { _currentEnv = s; RefreshEditorEnvVars(); }
        });

        var bar = new DockPanel { Height = 22 };
        bar.Add(settingsBtn.DockLeft());
        bar.Add(_envCombo!.DockRight());
        bar.Add(_statusLabel);
        return new Border { Height = 22, Background = Color.FromRgb(0x25, 0x25, 0x26), Child = bar };
    }
    private void UpdateStatusBar(int statusCode, long elapsedMs, long bodyBytes)
    {
        if (_statusLabel is null) return;
        var statusText = statusCode switch
        {
            >= 200 and < 300 => $"● {statusCode}",
            >= 300 and < 400 => $"● {statusCode}",
            >= 400 and < 500 => $"● {statusCode}",
            >= 500           => $"● {statusCode}",
            _                => $"✗ {statusCode}",
        };
        var sizeText = bodyBytes switch
        {
            < 1024        => $"{bodyBytes} B",
            < 1024 * 1024 => $"{bodyBytes / 1024.0:F1} KB",
            _             => $"{bodyBytes / (1024.0 * 1024):F1} MB",
        };
        _statusLabel.Text = $"{statusText}  {elapsedMs} ms  {sizeText}";
        _statusLabel.Foreground = statusCode switch
        {
            >= 200 and < 300 => Color.FromRgb(0x4E, 0xC9, 0xB0),
            >= 300 and < 400 => Color.FromRgb(0x4F, 0xC1, 0xFF),
            >= 400 and < 500 => Color.FromRgb(0xCE, 0x91, 0x78),
            _                => Color.FromRgb(0xF4, 0x47, 0x47),
        };
    }

    private void RebuildEnvCombo()
    {
        if (_envCombo is null) return;
        var envs = _workspace.AvailableEnvironments;
        if (envs.Count == 0)
        {
            _envCombo.Items(["无环境"]).SelectedIndex(0);
            _currentEnv = string.Empty;
        }
        else
        {
            _envCombo.Items(envs.ToArray()).SelectedIndex(0);
            _currentEnv = envs[0];
        }
        RefreshEditorEnvVars();
    }

    private void RefreshEditorEnvVars()
    {
        if (_editor is null || string.IsNullOrEmpty(_editor.CurrentFilePath) || string.IsNullOrEmpty(_currentEnv)) return;
        try
        {
            var resolver = Resty.Core.Environment.EnvironmentResolver.Load(_editor.CurrentFilePath, _currentEnv);
            _editor.SetEnvVars(new Dictionary<string, string>(resolver.Variables));
        }
        catch { }
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
            .Item("保存",         () => _editor?.TriggerSave(), shortcut: new KeyGesture(Key.S, ModifierKeys.Primary))
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
        _workspace.FilesChanged -= OnWorkspaceFilesChanged;
        _workspace.Load(path);
        _workspace.FilesChanged += OnWorkspaceFilesChanged;
        _workspaceName.Value = _workspace.WorkspaceName;
        _currentEnv = _workspace.AvailableEnvironments.Count > 0
            ? _workspace.AvailableEnvironments[0]
            : string.Empty;
        RebuildEnvCombo();
        // 关闭已开启的标签
        while (_tabs.Count > 0) CloseTab(0);
        Content = BuildWorkspaceLayout();
    }

    private void OnWorkspaceFilesChanged()
    {
        // 在后台线程触发 - 需要回到 UI 线程
        var sc = SynchronizationContext.Current;
        if (sc is not null)
            sc.Post(_ => { _workspace.Load(_workspace.WorkspacePath); _sidebar.SetWorkspace(_workspace); }, null);
        else
        {
            _workspace.Load(_workspace.WorkspacePath);
            _sidebar.SetWorkspace(_workspace);
        }
    }

    private void OnRequestSelected(HttpFileNode file, RequestNode req)
    {
        var key = $"{file.FilePath}||{req.Name}";
        // 检查是否已开启
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i].Key == key) { ActivateTab(i); return; }
        }

        var def = _workspace.GetRequestDefinition(file.FilePath, req.Name);
        if (def is null) return;

        // 创建新编辑器
        var editor = new RequestEditorView();
        WireEditorEvents(editor);
        editor.SetFilePath(file.FilePath);

        // 创建标签按钮（含关闭 ✕）
        var title = req.Name.Length > 20 ? req.Name[..20] + "…" : req.Name;
        var newIdx = _tabs.Count;

        var titleBlock = new TextBlock { Text = title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var closeBtn = new Button { Width = 18, Height = 18, Padding = new Thickness(0), IsVisible = false };
        closeBtn.Content("✕", false).FontSize(9).Background(Color.Transparent).Foreground(TextSec);
        closeBtn.Click += () => CloseTab(_tabs.FindIndex(t => t.Key == key));

        var tabContent = new DockPanel { Margin = new Thickness(12, 0, 6, 0) };
        tabContent.Add(closeBtn.DockRight());
        tabContent.Add(new Border { Width = 6 }.DockRight());
        tabContent.Add(titleBlock);

        var tabBtn = new Button { Height = 34, Padding = new Thickness(0) };
        tabBtn.Content(tabContent as Element).Background(Color.Transparent).Foreground(TextSec);
        tabBtn.Click += () => ActivateTab(_tabs.FindIndex(t => t.Key == key));
        tabBtn.MouseEnter += () => closeBtn.IsVisible = true;
        tabBtn.MouseLeave += () => closeBtn.IsVisible = false;

        var tab = new EditorTab(key, file, req, editor, tabBtn);
        _tabs.Add(tab);
        _tabBar.Add(tabBtn);
        ActivateTab(newIdx);   // 先把编辑器放入可视树
        editor.Load(def);      // 再加载数据（控件已在树中，Text/SelectedIndex 赋值生效）
        RefreshEditorEnvVars();
        _responsePanel.ShowEmpty();
    }
}
