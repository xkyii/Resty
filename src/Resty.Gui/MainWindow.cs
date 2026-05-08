using System.Threading;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Core.Assertions;
using Resty.Core.Environment;
using Resty.Core.Execution;
using Resty.Gui.Infrastructure;
using Resty.Gui.Models;
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
    private readonly WorkspaceService   _workspace      = new();
    private readonly SidebarView         _sidebar        = new();
    private readonly WorkspacePanelView  _workspacePanel = new();
    private readonly EnvManagerView      _envManagerView = new();
    private readonly ObservableValue<string> _workspaceName = new("Resty");


    // G2 组件
    private RequestEditorView? _editor; // 当前激活的编辑器
    private readonly HttpRequestExecutor _executor = new(timeoutMs: 30_000);

    // 主区容器（切换欢迎页 ↔ 编辑器）
    private readonly Border _mainArea;
    // 多标签支持
    private readonly TabControl   _editorTabControl = new();
    private readonly List<Button> _editorTabCloseBtns = [];
    private readonly Border     _responseArea;  // P5: 每个 Tab 独立响应面板
    private sealed record EditorTab(string Key, HttpFileNode File, RequestNode Request, RequestEditorView Editor, TextBlock TitleBlock, ResponsePanelView ResponsePanel, TabItem TabItem);
    private readonly List<EditorTab> _tabs = [];
    private int _activeTabIdx = -1;
    private string _currentEnv = string.Empty;
    private Border     _sidebarPanelBorder  = new();  // Activity Bar 切换侧边栏内容区
    private Border     _rightContentBorder  = new();  // 右侧主区（切换编辑器 ↔ 环境管理）
    private UIElement  _editorAndResponse   = null!;  // 请求编辑器 + 响应面板组合
    private SplitPanel _bodyPanel           = new();  // 侧边栏 / 主区分隔面板（动态调宽）
    private int        _activePanelIdx      = 0;      // 当前活跃的 Activity Bar 面板
    private UIElement[] _panelRightDefaults = null!;  // 各面板右侧默认内容
    private readonly UIElement?[] _panelRightCache = new UIElement?[4]; // 各面板右侧记忆
    private CancellationTokenSource? _currentCts;
    // P15: Tab 状态缓存（key = "filePath||reqName"）
    private readonly Dictionary<string, Resty.Core.Models.HttpRequestDefinition> _tabStateCache = new();
    // P11: 请求历史面板 + 服务
    private readonly HistoryPanelView  _historyPanel  = new();
    private readonly HistoryDetailView _historyDetail = new();
    private readonly HistoryService    _historyService = new();
    // P12: 设置窗口（独立 NativeCustomWindow）
    private SettingsWindow? _settingsWindow;
    // Lab: 实验面板
    private readonly LabView _labView = new();

    public MainWindow()
    {
        this.Resizable(1280, 800, minWidth: 800, minHeight: 600)
            .StartCenterScreen();

        // 标题跟工作区名同步
        _workspaceName.Changed += () => this.Title = _workspaceName.Value;
        this.Title = _workspaceName.Value;

        // 窗口图标（标题栏 + 任务栏）
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "resty.ico");
        if (File.Exists(icoPath))
            Icon = IconSource.FromFile(icoPath);

        // 背景色
        Background = BgPanel;

        // ── 标题栏左：图标 + MenuBar ──────────────────────────────
        var icoImgPath = Path.Combine(AppContext.BaseDirectory, "Assets", "resty.ico");
        if (File.Exists(icoImgPath))
        {
            var appIcon = new Image
            {
                Source = ImageSource.FromFile(icoImgPath),
                Width  = 18,
                Height = 18,
                Margin = new Thickness(8, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            TitleBarLeft.Add(appIcon);
        }
        TitleBarLeft.Add(BuildMenuBar());
        // 窗口标题居中显示：由 this.Title 控制（随侧边栏/面板变化）

        // ── 侧边栏事件 ───────────────────────────────────────────
        _sidebar.RequestSelected += OnRequestSelected;
        _sidebar.NewFileCreated  += OnNewFileCreated;

        // ── 主区容器（层次：_mainArea > DockPanel > _tabBar + _editorArea）──
        _responseArea = new Border { Background = BgBase, Child = new ResponsePanelView().RootElement };

        _editorTabControl.OnSelectionChanged(selected =>
        {
            var idx = _tabs.FindIndex(t => ReferenceEquals(t.TabItem, selected));
            if (idx >= 0)
            {
                _activeTabIdx = idx;
                var tab = _tabs[idx];
                _editor = tab.Editor;
                _responseArea.Child = tab.ResponsePanel.RootElement;
                _sidebar.SetActiveRequest(tab.File, tab.Request);
            }
        });

        _mainArea = new Border { Child = _editorTabControl };

        // P4: Ctrl+W 关闭当前 Tab
        this.KeyDown += e =>
        {
            if (e.Key == Key.W && e.Modifiers == ModifierKeys.Primary)
                CloseTab(_activeTabIdx);
        };

        // 初始布局：不把 _mainArea 直接作为 Content，
        // 避免首次 LoadWorkspace 时 SplitPanel 接管 _mainArea 产生父节点冲突。
        Content = BuildWelcomeView();
        Padding = new Thickness(0);
    }

    // ── 编辑器事件绑定 ─────────────────────────────────────
    private void WireEditorEvents(RequestEditorView editor, ResponsePanelView responsePanel, string requestName = "", string filePath = "")
    {
        editor.CancelRequested = () => { try { _currentCts?.Cancel(); } catch (ObjectDisposedException) { _currentCts = null; } };
        editor.SaveRequested = (fp, def) => _workspace.SaveRequest(fp, def);
        editor.SendRequested = req =>
        {
            var sc               = SynchronizationContext.Current;
            var envName          = _currentEnv;
            var workspacePath    = _workspace.WorkspacePath;
            var capturedEditor   = editor;
            var capturedPanel    = responsePanel;
            var capturedName     = requestName;
            var capturedFilePath = filePath;
            var capturedHistory  = _historyPanel;
            var capturedService  = _historyService;
            _currentCts?.Cancel();
            _currentCts = new CancellationTokenSource();
            var cts = _currentCts;
            capturedPanel.ShowLoading();
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
                    var result     = await _executor.ExecuteAsync(resolvedReq, cts.Token);
                    var assertions = req.Assertions.Count > 0
                        ? AssertionEngine.Evaluate(req.Assertions, result)
                        : null;

                    // 构建完整 .hlog 记录
                    var ts = DateTime.Now;
                    var id = HistoryService.NewId(ts, capturedName);
                    var statusText = result.StatusCode > 0
                        ? GetStatusText(result.StatusCode) : string.Empty;

                    var record = new HistoryRecord(
                        new HistorySummary(id, capturedName, resolvedReq.Method,
                            resolvedReq.Url, result.StatusCode, result.ElapsedMs,
                            ts, capturedFilePath, result.Error),
                        HlogSerializer.BuildRequestSection(
                            resolvedReq.Method, resolvedReq.Url,
                            resolvedReq.Headers, resolvedReq.Body),
                        HlogSerializer.BuildResponseSection(
                            result.StatusCode, statusText,
                            result.Headers, result.Body, result.Error),
                        HlogSerializer.BuildAssertionsSection(assertions));

                    sc?.Post(_ =>
                    {
                        capturedEditor.SetSendingState(false);
                        capturedPanel.ShowResult(result, assertions);
                        capturedService.AddRecord(record);
                        capturedHistory.PrependSummary(record.Summary);
                    }, null);
                }
                catch (OperationCanceledException)
                {
                    sc?.Post(_ => { capturedEditor.SetSendingState(false); capturedPanel.ShowEmpty(); }, null);
                }
                catch (Exception ex)
                {
                    sc?.Post(_ => { capturedEditor.SetSendingState(false); capturedPanel.ShowError(ex.Message); }, null);
                }
                finally
                {
                    cts.Dispose();
                    if (ReferenceEquals(_currentCts, cts)) _currentCts = null;
                }
            });
        };
    }

    private static string GetStatusText(int code) => code switch
    {
        200 => "OK", 201 => "Created", 204 => "No Content",
        301 => "Moved Permanently", 302 => "Found", 304 => "Not Modified",
        400 => "Bad Request", 401 => "Unauthorized", 403 => "Forbidden",
        404 => "Not Found", 405 => "Method Not Allowed", 409 => "Conflict",
        422 => "Unprocessable Entity", 429 => "Too Many Requests",
        500 => "Internal Server Error", 502 => "Bad Gateway",
        503 => "Service Unavailable", 504 => "Gateway Timeout",
        _ => string.Empty,
    };

    // ── 标签管理 ─────────────────────────────────────
    private void ActivateTab(int idx)
    {
        if (idx < 0 || idx >= _tabs.Count) return;
        _activeTabIdx = idx;
        var tab = _tabs[idx];
        _editor = tab.Editor;
        _responseArea.Child = tab.ResponsePanel.RootElement;
        _editorTabControl.SelectedIndex(idx);
        this.Title = tab.Request.Name;
        // 同步左侧树中的激活请求
        _sidebar.SetActiveRequest(tab.File, tab.Request);
    }

    private void CloseTab(int idx)
    {
        if (idx < 0 || idx >= _tabs.Count) return;
        var tab = _tabs[idx];
        // P15: 关闭前缓存编辑器当前状态
        var snapshot = tab.Editor.GetCurrentDefinition();
        if (snapshot is not null)
            _tabStateCache[tab.Key] = snapshot;
        _editorTabCloseBtns.RemoveAt(idx);
        _tabs.RemoveAt(idx);
        _editorTabControl.RemoveTabAt(idx);
        if (_tabs.Count == 0)
        {
            _activeTabIdx = -1;
            _editor = null;
            _responseArea.Child = new ResponsePanelView().RootElement;
            this.Title = _workspaceName.Value;
        }
        else
        {
            var newIdx = Math.Min(idx, _tabs.Count - 1);
            ActivateTab(newIdx);
        }
        BindEditorTabHeaders();
    }

    private void BindEditorTabHeaders()
    {
        var i = 0;
        VisualTree.Visit(_editorTabControl, el =>
        {
            if (el.GetType().Name == "TabHeaderButton" && el is UIElement thb)
            {
                if (i >= _editorTabCloseBtns.Count) return;
                var btn = _editorTabCloseBtns[i++];
                thb.MouseEnter += () => btn.Foreground(TextSec);
                thb.MouseLeave += () => btn.Foreground(Color.Transparent);
            }
        });
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
        _envManagerView.SetWorkspace(_workspace);

        // 请求编辑区 + 响应区（上下 SplitPanel）
        _editorAndResponse = new SplitPanel
        {
            Orientation       = Orientation.Vertical,
            FirstLength       = 360,
            SplitterThickness = 4,
            First             = _mainArea,
            Second            = _responseArea,
        };

        // P9: 工作区面板更新当前路径
        _workspacePanel.SetCurrentPath(_workspace.WorkspacePath);
        _workspacePanel.Refresh();

        // 右侧主区（Border 方便切换内容）
        _rightContentBorder = new Border { Child = _editorAndResponse };
        // 各面板右侧默认内容（无记忆时的初始值）
        _panelRightDefaults = new UIElement[]
        {
            _editorAndResponse,         // 0: 工作区
            _historyDetail.RootElement, // 1: 请求历史
            _editorAndResponse,         // 2: 最近工作区
            _editorAndResponse,         // 3: 实验室
        };
        // 订阅 LabView 的试验打开事件：当左侧点击某个试验时，将具体试验内容显示到右侧主区
        _labView.ExperimentRequested -= ui => _rightContentBorder.Child = ui;
        _labView.ExperimentRequested += ui => _rightContentBorder.Child = ui;
        var rightArea = new DockPanel();
        rightArea.Add(_rightContentBorder);

        // 侧边栏内容区（Activity Bar 切换）
        _sidebarPanelBorder = new Border
        {
            Background = BgSidebar,
            Child      = _sidebar.RootElement,
        };

        _bodyPanel = new SplitPanel
        {
            Orientation       = Orientation.Horizontal,
            FirstLength       = 260,
            SplitterThickness = 4,
            First             = new DockPanel().Children(
                                    BuildActivityBar().DockLeft(),
                                    _sidebarPanelBorder),
            Second            = rightArea,
        };

        // 整体 DockPanel
        var root = new DockPanel();
        root.Add(_bodyPanel);
        return root;
    }

    // ── Activity Bar ────────────────────────────────────────────
    private UIElement BuildActivityBar()
    {
        var bgBar = Color.FromRgb(0x33, 0x33, 0x33);

        Border collectionLine = null!, historyLine = null!, workspaceLine = null!, labLine = null!;
        Button collectionBtn  = null!, historyBtn  = null!, workspaceBtn  = null!, labBtn = null!;

        collectionLine = new Border { Width = 2, Background = Accent };
        historyLine    = new Border { Width = 2, Background = Color.Transparent };
        workspaceLine  = new Border { Width = 2, Background = Color.Transparent };
        labLine        = new Border { Width = 2, Background = Color.Transparent };

        void SetActive(int idx)
        {
            // 保存当前面板的右侧内容
            _panelRightCache[_activePanelIdx] = _rightContentBorder.Child;
            _activePanelIdx = idx;

            // 恢复目标面板的右侧内容（有记忆则用记忆，否则用默认值）
            _rightContentBorder.Child = _panelRightCache[idx] ?? _panelRightDefaults[idx];

            // 侧边栏宽度：历史面板稍宽
            _bodyPanel.FirstLength = idx == 1 ? 320 : 260;

            // 左侧竖线选中态
            collectionLine.Background = idx == 0 ? Accent : Color.Transparent;
            historyLine.Background    = idx == 1 ? Accent : Color.Transparent;
            workspaceLine.Background  = idx == 2 ? Accent : Color.Transparent;
            labLine.Background        = idx == 3 ? Accent : Color.Transparent;

            collectionBtn.Foreground(idx == 0 ? Color.White : TextSec);
            historyBtn.Foreground(idx == 1 ? Color.White : TextSec);
            workspaceBtn.Foreground(idx == 2 ? Color.White : TextSec);
            labBtn.Foreground(idx == 3 ? Color.White : TextSec);

            _sidebarPanelBorder.Child = idx switch
            {
                0 => _sidebar.RootElement,
                1 => _historyPanel.RootElement,
                2 => _workspacePanel.RootElement,
                3 => _labView.RootElement,
                _ => _sidebar.RootElement,
            };
            // 更新窗口标题（居中显示）
            this.Title = idx switch
            {
                0 => string.IsNullOrEmpty(_workspace.WorkspaceName) ? "工作区" : _workspace.WorkspaceName,
                1 => "请求历史",
                2 => "最近工作区",
                3 => "实验室",
                _ => "Resty",
            };
        }

        collectionBtn = MakeActivityBtn("☰", collectionLine, () => SetActive(0), "工作区");
        historyBtn    = MakeActivityBtn("⧗", historyLine,    () => SetActive(1), "请求历史");
        workspaceBtn  = MakeActivityBtn("⊞", workspaceLine,  () => SetActive(2), "最近工作区");
        labBtn        = MakeActivityBtn("⚗", labLine,        () => SetActive(3), "实验室");

        var settingsBtn = MakeSettingsBtn();

        var topPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        topPanel.Add(collectionBtn);
        topPanel.Add(historyBtn);
        topPanel.Add(workspaceBtn);
        topPanel.Add(labBtn);

        var barDock = new DockPanel();
        barDock.Add(settingsBtn.DockBottom());
        barDock.Add(topPanel);

        return new Border
        {
            Width      = 40,
            Background = bgBar,
            Child      = barDock,
        };
    }

    private Button MakeSettingsBtn()
    {
        var lbl = new TextBlock
        {
            Text                = "⚙",
            FontSize            = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        var btn = new Button
        {
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
            BorderThickness = 0,
        };
        btn.Content(lbl as Element).Background(Color.Transparent).Foreground(TextSec);
        try { btn.BorderBrush = Color.Transparent; } catch { }
        btn.Click      += () =>
        {
            if (_settingsWindow is null)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += () => _settingsWindow = null;
                _settingsWindow.Show();
            }
            else
            {
                // 窗口已存在：前置并激活
                if (_settingsWindow.WindowState == WindowState.Minimized)
                    _settingsWindow.Restore();
                _settingsWindow.Activate();
                _settingsWindow.Focus();
            }
        };
        btn.MouseEnter += () => btn.Background(Color.FromRgb(0x45, 0x45, 0x45));
        btn.MouseLeave += () => btn.Background(Color.Transparent);
        btn.ToolTip("设置");
        return btn;
    }

    private static Button MakeActivityBtn(string icon, Border indicator, Action onClick, string tooltip)
    {
        var lbl = new TextBlock
        {
            Text              = icon,
            FontSize          = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new DockPanel();
        row.Add(indicator.DockLeft());
        row.Add(lbl);

        var btn = new Button
        {
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
            BorderThickness = 0,
        };
        btn.Content(row as Element).Background(Color.Transparent).Foreground(Color.FromRgb(0x85, 0x85, 0x85));
        // 隐藏默认边框以尽量避免系统焦点样式干扰
        try { btn.BorderBrush = Color.Transparent; } catch { }
        btn.Click      += onClick;
        btn.MouseEnter += () => btn.Background(Color.FromRgb(0x45, 0x45, 0x45));
        btn.MouseLeave += () => btn.Background(Color.Transparent);
        btn.ToolTip(tooltip);
        return btn;
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

    private void OnHistoryEntrySelected(HistorySummary summary)
    {
        var record = _historyService.LoadRecord(summary.Id);
        _rightContentBorder.Child = record is not null
            ? _historyDetail.BuildView(record)
            : _historyDetail.RootElement;
    }

    private void OnHistoryClearRequested()
    {
        _historyService.Clear();
        _historyPanel.ClearList();
        _panelRightCache[1] = null;
        _rightContentBorder.Child = _historyDetail.RootElement;
    }

    // 历史记录"在编辑器打开"
    private void OnHistoryOpenRequested(string filePath, string requestName)
    {
        if (_workspace is null) return;
        var file = _workspace.Files.FirstOrDefault(f => f.FilePath == filePath);
        var req  = file?.Requests.FirstOrDefault(r => r.Name == requestName);
        if (file is not null && req is not null)
            OnRequestSelected(file, req);
    }

    // 环境模式切换（来自 SidebarView.EnvModeChanged）
    private void OnEnvModeChanged(bool isEnvMode)
    {
        _rightContentBorder.Child = isEnvMode ? _envManagerView.RootElement : _editorAndResponse;
    }

    // 环境选中（来自 SidebarView.EnvActivated）
    private void OnEnvSelected(string envName)
    {
        _currentEnv = envName;
        _envManagerView.SelectEnv(envName);
        RefreshEditorEnvVars();
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

        var helpMenu = new Menu()
            .Item("HTTP 语法参考…", () => { })
            .Separator()
            .Item("关于 Resty",    () => { });

        var bar = new MenuBar();
        bar.Background = Color.Transparent;
        bar.Add(new MenuItem("\u6587\u4ef6(_F)").Menu(fileMenu));
        bar.Add(new MenuItem("\u89c6\u56fe(_V)").Menu(viewMenu));
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
        this.Title = _workspaceName.Value;
        _currentEnv = _workspace.AvailableEnvironments.Count > 0
            ? _workspace.AvailableEnvironments[0]
            : string.Empty;

        // P9: 记录最近工作区
        RecentWorkspacesService.Add(path);

        // P11: 初始化历史面板
        _historyService.SetWorkspacePath(path);
        _historyPanel.SetSummaries(_historyService.Summaries);
        _historyPanel.EntrySelected  -= OnHistoryEntrySelected;
        _historyPanel.EntrySelected  += OnHistoryEntrySelected;
        _historyPanel.ClearRequested -= OnHistoryClearRequested;
        _historyPanel.ClearRequested += OnHistoryClearRequested;
        _historyDetail.OpenRequested -= OnHistoryOpenRequested;
        _historyDetail.OpenRequested += OnHistoryOpenRequested;

        // 订阅 SidebarView 事件
        _sidebar.EnvModeChanged -= OnEnvModeChanged;
        _sidebar.EnvModeChanged += OnEnvModeChanged;
        _sidebar.EnvActivated   -= OnEnvSelected;
        _sidebar.EnvActivated   += OnEnvSelected;

        // 订阅 WorkspacePanelView.WorkspaceSelected 事件
        _workspacePanel.WorkspaceSelected -= LoadWorkspace;
        _workspacePanel.WorkspaceSelected += LoadWorkspace;

        // 关闭已开启的标签
        while (_tabs.Count > 0) CloseTab(0);
        Content = BuildWorkspaceLayout();

        // 初始化环境
        if (!string.IsNullOrEmpty(_currentEnv))
        {
            _sidebar.SetActiveEnv(_currentEnv);
            _envManagerView.SelectEnv(_currentEnv);
        }
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

        // 创建新编辑器 + 独立响应面板（P5）
        var responsePanel = new ResponsePanelView();
        var editor = new RequestEditorView();
        WireEditorEvents(editor, responsePanel, req.Name, file.FilePath);
        editor.SetFilePath(file.FilePath);

        // 创建标签按钮（含关闭 ✕ 和 dirty 标记）
        var title = req.Name.Length > 20 ? req.Name[..20] + "…" : req.Name;
        var newIdx = _tabs.Count;

        var dirtyDot  = new TextBlock { Text = string.Empty, FontSize = 10, Foreground = Color.FromRgb(0xE0, 0x6C, 0x75), VerticalAlignment = VerticalAlignment.Center, Width = 12, TextAlignment = TextAlignment.Center };
        var titleBlock = new TextBlock { Text = title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        Button  closeBtn = null!;
        TabItem tabItem  = null!;

        // P3: 订阅 dirty 变化，更新 tab 上的 ● 标记
        editor.DirtyChanged += dirty => dirtyDot.Text = dirty ? "●" : string.Empty;

        new TabItem().Ref(out tabItem)
            .Header(
                new StackPanel()
                    .Horizontal()
                    .CenterVertical()
                    .Spacing(4)
                    .Children(
                        dirtyDot,
                        titleBlock,
                        new Button()
                            .Ref(out closeBtn)
                            .Content(new GlyphElement { Kind = GlyphKind.Cross, GlyphSize = 3.5, IsHitTestVisible = false })
                            .MinHeight(0)
                            .Size(16, 16)
                            .Padding(new Thickness(0))
                            .CenterVertical()
                            .BorderThickness(0)
                            .Background(Color.Transparent)
                            .Foreground(Color.Transparent)
                            .OnClick(() => CloseTab(_tabs.FindIndex(t => t.Key == key)))
                    ))
            .Content(editor.RootElement as Element);

        var tab = new EditorTab(key, file, req, editor, titleBlock, responsePanel, tabItem);
        _tabs.Add(tab);
        _editorTabCloseBtns.Add(closeBtn);
        _editorTabControl.AddTab(tabItem);
        BindEditorTabHeaders();
        ActivateTab(newIdx);   // 先把编辑器放入可视树
        // P15: 优先从缓存恢复，否则从文件加载
        if (_tabStateCache.TryGetValue(key, out var cached))
            editor.Load(cached);
        else
            editor.Load(def);
        RefreshEditorEnvVars();
    }

    private void OnNewFileCreated(string filePath)
    {
        // 找到新文件的第一个请求并自动打开
        var fileNode = _workspace.Files.FirstOrDefault(f =>
            string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (fileNode is null || fileNode.Requests.Count == 0) return;
        OnRequestSelected(fileNode, fileNode.Requests[0]);
    }
}
