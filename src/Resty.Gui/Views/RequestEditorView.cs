using System.Text;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Core.Models;
using Resty.Core.Parsing;

namespace Resty.Gui.Views;

/// <summary>
/// G3 请求编辑区：文本 / 结构化双模式，双向同步。
/// 文本模式：MultiLineTextBox 原始 HTTP。
/// 结构化模式：方法 ComboBox + URL 输入框 + Headers/Body Tab。
/// </summary>
public sealed class RequestEditorView
{
    // ── 颜色 ────────────────────────────────────────────────────
    private static readonly Color BgBase    = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color BgPanel   = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color BgSurface = Color.FromRgb(0x37, 0x37, 0x38);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color Accent    = Color.FromRgb(0x00, 0x7A, 0xCC);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);

    private static readonly string[] Methods = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    // ── 工具栏控件 ───────────────────────────────────────────────
    private readonly ComboBox _methodCombo;
    private readonly TextBox  _urlBox;
    private readonly Button   _sendBtn;
    // P16 cURL 导入行
    private readonly Border   _curlImportRow;
    private readonly TextBox  _curlImportBox;

    // ── 内容区 ───────────────────────────────────────────────────
    private readonly Border            _contentArea;
    private readonly MultiLineTextBox  _textEditor;
    private readonly TabControl        _tabControl;
    // F5 语法提示栏
    private readonly TextBlock _syntaxHintLabel;
    private readonly Border    _textEditorWithHint; // DockPanel with hint + editor
    private bool _isRawTabActive = false; // 当前是否显示原文 Tab
    // F6 URL 变量预览
    private readonly TextBlock _urlPreviewLabel;
    private readonly Border    _urlPreviewRow;
    private Dictionary<string, string> _envVars = new();

    // ── 结构化模式内控件 ─────────────────────────────────────────
    private readonly StackPanel  _paramRows;      // 动态 query param 行
    private readonly StackPanel  _headerRows;     // 动态 header 行
    private readonly MultiLineTextBox _bodyText;  // Body tab 中的文本框
    private readonly ComboBox    _contentTypeCombo;
    private readonly StackPanel  _assertionRows;  // 动态断言行
    // Auth Tab
    private int              _authMode = 0;       // 0=None 1=Basic 2=Bearer
    private readonly Button  _authNoneBtn, _authBasicBtn, _authBearerBtn;
    private readonly TextBox _authTokenBox, _authUsernameBox, _authPasswordBox;
    private readonly Border  _authContent;
    private readonly UIElement _authNoneContent, _authBasicContent, _authBearerContent;
    // ── 根布局 ───────────────────────────────────────────────────
    private readonly DockPanel _root;

    // ── 空状态覆盖层 ─────────────────────────────────────────────
    private readonly UIElement _emptyOverlay;
    private bool _hasLoaded = false;

    // ── 公共接口 ─────────────────────────────────────────────────
    public UIElement RootElement => _root;
    public Action<HttpRequestDefinition>? SendRequested;
    public Action? CancelRequested;
    public Action<string, HttpRequestDefinition>? SaveRequested; // (filePath, def)
    public string? CurrentFilePath { get; private set; }
    public bool IsDirty { get; private set; }
    public event Action<bool>? DirtyChanged; // true = 变脏, false = 已保存

    private readonly TextBlock _dirtyLabel;
    /// <summary>切换发送/取消按钮状态。</summary>
    public void SetSendingState(bool sending)
    {
        if (sending)
            _sendBtn.Content("■ 取消", false).Background(Color.FromRgb(0x6A, 0x1A, 0x1A)).OnClick(OnCancelClicked);
        else
            _sendBtn.Content("▶ 发送", false).Background(Accent).OnClick(OnSendClicked);
    }

    /// <summary>设置当前文件路径（由 MainWindow 在加载请求时调用）。</summary>
    public void SetFilePath(string path)
    {
        CurrentFilePath = path;
        SetDirty(false);
    }

    /// <summary>F6: 更新当前环境变量，用于 URL 预览。</summary>
    public void SetEnvVars(Dictionary<string, string> vars)
    {
        _envVars = vars;
        UpdateUrlPreview(_urlBox.Text ?? string.Empty);
    }

    private void SetDirty(bool dirty)
    {
        IsDirty = dirty;
        _dirtyLabel.Text = dirty ? "●" : string.Empty;
        DirtyChanged?.Invoke(dirty);
    }

    /// <summary>F5: 解析文本内容并更新语法提示栏。</summary>
    private void UpdateSyntaxHint(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) { _syntaxHintLabel.Text = string.Empty; return; }
        try
        {
            var fd = HttpFileParser.ParseContent(text);
            if (fd.Requests.Count == 0) { _syntaxHintLabel.Text = "⚠ 无有效请求"; _syntaxHintLabel.Foreground(Color.FromRgb(0xE0, 0x6C, 0x75)); return; }
            var r = fd.Requests[0];
            var method = r.Method.ToUpperInvariant();
            var methodColor = method switch
            {
                "GET"    => Color.FromRgb(0x61, 0xAF, 0xEF),
                "POST"   => Color.FromRgb(0x98, 0xC3, 0x79),
                "PUT"    => Color.FromRgb(0xE5, 0xC0, 0x7B),
                "DELETE" => Color.FromRgb(0xE0, 0x6C, 0x75),
                "PATCH"  => Color.FromRgb(0xD1, 0x9A, 0x66),
                _        => Color.FromRgb(0xAB, 0xB2, 0xBF),
            };
            var extra = fd.Requests.Count > 1 ? $"  (+{fd.Requests.Count - 1} 个请求)" : string.Empty;
            _syntaxHintLabel.Text = $"{method}  {r.Url}{extra}";
            _syntaxHintLabel.Foreground(methodColor);
        }
        catch
        {
            _syntaxHintLabel.Text = "⚠ 解析错误";
            _syntaxHintLabel.Foreground(Color.FromRgb(0xE0, 0x6C, 0x75));
        }
    }

    /// <summary>F6: URL 变量预览更新。</summary>
    private void UpdateUrlPreview(string url)
    {
        if (!url.Contains("{{")) { _urlPreviewRow.IsVisible = false; return; }
        // 简单替换 {{varName}} 占位符
        var resolved = System.Text.RegularExpressions.Regex.Replace(url, @"\{\{(\w+)\}\}", m =>
        {
            var name = m.Groups[1].Value;
            return _envVars.TryGetValue(name, out var val) ? val : m.Value;
        });
        bool hasUnresolved = resolved.Contains("{{");
        _urlPreviewLabel.Text = "→ " + resolved;
        _urlPreviewLabel.Foreground(hasUnresolved
            ? Color.FromRgb(0xE0, 0x6C, 0x75)  // 红：有未解析的变量
            : Color.FromRgb(0x4E, 0xC9, 0xB0)); // 绿：完全解析
        _urlPreviewRow.IsVisible = true;
    }
    public RequestEditorView()
    {
        _dirtyLabel = new TextBlock
        {
            Text      = string.Empty,
            FontSize  = 16,
            Foreground = Color.FromRgb(0xCC, 0xCC, 0xCC),
            Width     = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // ── 方法 ComboBox ────────────────────────────────────────
        _methodCombo = new ComboBox { Width = 100, Height = 28 };
        _methodCombo.Items(Methods)
                    .SelectedIndex(0)
                    .OnSelectionChanged(_ => UpdateMethodColor());

        // ── URL 输入框 ────────────────────────────────────────────
        _urlBox = new TextBox
        {
            FontSize          = 13,
            Foreground        = TextPri,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _urlBox.Placeholder("https://example.com/api/…");

        // ── 发送按钮 ──────────────────────────────────────────────
        _sendBtn = new Button { Width = 80, Height = 36 };
        _sendBtn.Content("▶ 发送", false)
            .FontSize(13)
            .Background(Accent)
            .Foreground(Color.White)
            .OnClick(OnSendClicked);

        // ── 工具栏（单行：方法 + URL + 发送）────────────
        // cURL 导出按钮
        var curlExportBtn = new Button { Width = 56, Height = 28, Padding = new Thickness(0) };
        curlExportBtn.Content("⬇cURL", false).FontSize(10).Background(Color.Transparent).Foreground(TextSec);
        curlExportBtn.MouseEnter += () => curlExportBtn.Background(BgSurface).Foreground(TextPri);
        curlExportBtn.MouseLeave += () => curlExportBtn.Background(Color.Transparent).Foreground(TextSec);
        curlExportBtn.OnClick(() =>
        {
            var def = GetCurrentDefinition();
            if (def is null) return;
            var curlStr = Resty.Core.Parsing.CurlConverter.Export(def);
            CopyToClipboard(curlStr);
        });
        // cURL 导入按钮
        var curlImportBtn = new Button { Width = 56, Height = 28, Padding = new Thickness(0) };
        curlImportBtn.Content("⬆cURL", false).FontSize(10).Background(Color.Transparent).Foreground(TextSec);
        curlImportBtn.MouseEnter += () => curlImportBtn.Background(BgSurface).Foreground(TextPri);
        curlImportBtn.MouseLeave += () => curlImportBtn.Background(Color.Transparent).Foreground(TextSec);

        var urlRow = new DockPanel { Height = 44 };
        urlRow.Add(new Border { Width = 8 }.DockLeft());
        urlRow.Add(_methodCombo.DockLeft());
        urlRow.Add(new Border { Width = 8 }.DockLeft());
        urlRow.Add(new Border { Width = 8 }.DockRight());
        urlRow.Add(_sendBtn.DockRight());
        urlRow.Add(new Border { Width = 4 }.DockRight());
        urlRow.Add(curlExportBtn.DockRight());
        urlRow.Add(curlImportBtn.DockRight());
        urlRow.Add(_urlBox);

        // ── cURL 导入行（初始隐藏）───────────────────────────────
        _curlImportBox = new TextBox
        {
            Placeholder = "粘贴 curl 命令…",
            FontSize    = 12,
            Foreground  = TextPri,
            Background  = BgBase,
        };
        var curlConfirmBtn = new Button { Width = 52, Height = 28, Padding = new Thickness(0) };
        curlConfirmBtn.Content("导入", false).FontSize(11).Background(Accent).Foreground(Color.White);
        var curlCancelBtn = new Button { Width = 40, Height = 28, Padding = new Thickness(0) };
        curlCancelBtn.Content("✕", false).FontSize(11).Background(Color.Transparent).Foreground(TextSec);
        curlCancelBtn.MouseEnter += () => curlCancelBtn.Background(BgSurface);
        curlCancelBtn.MouseLeave += () => curlCancelBtn.Background(Color.Transparent);

        var curlImportInner = new DockPanel { Height = 36 };
        curlImportInner.Add(new Border { Width = 8 }.DockLeft());
        curlImportInner.Add(new Border { Width = 4 }.DockRight());
        curlImportInner.Add(curlCancelBtn.DockRight());
        curlImportInner.Add(new Border { Width = 4 }.DockRight());
        curlImportInner.Add(curlConfirmBtn.DockRight());
        curlImportInner.Add(_curlImportBox);

        _curlImportRow = new Border
        {
            Background = BgPanel,
            Padding    = new Thickness(0, 4),
            Child      = curlImportInner,
            IsVisible  = false,
        };

        // 事件绑定
        curlImportBtn.OnClick(() =>
        {
            _curlImportRow.IsVisible = !_curlImportRow.IsVisible;
        });
        curlConfirmBtn.OnClick(() =>
        {
            var cmd = _curlImportBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(cmd)) return;
            if (Resty.Core.Parsing.CurlConverter.TryImport(cmd, out var imported))
            {
                Load(imported);
                _curlImportRow.IsVisible = false;
                _curlImportBox.Text = string.Empty;
            }
        });
        curlCancelBtn.OnClick(() => { _curlImportRow.IsVisible = false; _curlImportBox.Text = string.Empty; });

        // ── URL 变量预览行（F6）──────────────────────────────────
        _urlPreviewLabel = new TextBlock { FontSize = 11 };
        _urlPreviewRow = new Border
        {
            Background = BgBase,
            Padding    = new Thickness(12, 2, 12, 2),
            Child      = _urlPreviewLabel,
            IsVisible  = false,
        };

        var toolbarInner = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing     = 0,
        };
        toolbarInner.Add(urlRow);

        var toolbarBorder = new Border
        {
            Height      = 44,
            Background  = BgPanel,
            BorderBrush = BorderCol,
            Child       = toolbarInner,
        };
        _textEditor = new MultiLineTextBox
        {
            FontSize   = 13,
            Foreground = TextPri,
            Background = BgBase,
            Padding    = new Thickness(12, 8, 12, 8),
        };
        _textEditor.Wrap(false);

        // F5 语法提示栏（文本模式下显示解析摘要）
        _syntaxHintLabel = new TextBlock
        {
            FontSize  = 12,
            Foreground = TextSec,
        };
        var syntaxHintBorder = new Border
        {
            Background  = BgPanel,
            BorderBrush = BorderCol,
            Padding     = new Thickness(12, 3, 12, 3),
            Child       = _syntaxHintLabel,
        };
        var textWithHintPanel = new DockPanel();
        textWithHintPanel.Add(syntaxHintBorder.DockTop());
        textWithHintPanel.Add(_textEditor);
        _textEditorWithHint = new Border { Child = textWithHintPanel };

        // ── 结构化面板 ────────────────────────────────────────────
        // Params Tab
        _paramRows = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing     = 2,
            Margin      = new Thickness(8, 8, 8, 0),
        };
        var addParamBtn = new Button { Height = 24 };
        addParamBtn.Content("+ 添加参数", false)
            .FontSize(12).Background(Color.Transparent).Foreground(Accent)
            .OnClick(AddEmptyParamRow);
        var paramsContent = new StackPanel { Orientation = Orientation.Vertical };
        paramsContent.Add(_paramRows);
        paramsContent.Add(addParamBtn);
        var paramsScroll = new ScrollViewer
        {
            VerticalScroll = ScrollMode.Auto,
            Content        = paramsContent,
        };

        // Headers Tab
        _headerRows = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing     = 2,
            Margin      = new Thickness(8, 8, 8, 0),
        };

        var addHeaderBtn = new Button { Height = 24 };
        addHeaderBtn.Content("+ 添加 Header", false)
            .FontSize(12).Background(Color.Transparent).Foreground(Accent)
            .OnClick(AddEmptyHeaderRow);

        var headersContent = new StackPanel { Orientation = Orientation.Vertical };
        headersContent.Add(_headerRows);
        headersContent.Add(addHeaderBtn);

        var headersScroll = new ScrollViewer
        {
            VerticalScroll = ScrollMode.Auto,
            Content        = headersContent,
        };

        // Body Tab
        _contentTypeCombo = new ComboBox { Width = 200, Height = 24, Margin = new Thickness(8, 8, 0, 4) };
        _contentTypeCombo.Items(["application/json", "text/plain", "application/x-www-form-urlencoded", "multipart/form-data"]);
        _contentTypeCombo.SelectedIndex(0);

        _bodyText = new MultiLineTextBox
        {
            FontSize   = 13,
            Foreground = TextPri,
            Background = BgBase,
            Padding    = new Thickness(8, 4, 8, 4),
        };
        _bodyText.Wrap(false);

        var bodyContent = new DockPanel();
        bodyContent.Add(_contentTypeCombo.DockTop());
        bodyContent.Add(_bodyText);

        // Assertions Tab
        _assertionRows = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing     = 2,
            Margin      = new Thickness(8, 8, 8, 0),
        };
        var addAssertBtn = new Button { Height = 24 };
        addAssertBtn.Content("+ 添加断言", false)
            .FontSize(12).Background(Color.Transparent).Foreground(Accent)
            .OnClick(AddEmptyAssertionRow);
        var assertContent = new StackPanel { Orientation = Orientation.Vertical };
        assertContent.Add(_assertionRows);
        assertContent.Add(addAssertBtn);
        var assertScroll = new ScrollViewer
        {
            VerticalScroll = ScrollMode.Auto,
            Content        = assertContent,
        };

        // Auth Tab
        _authTokenBox    = new TextBox { FontSize = 12, Foreground = TextPri, Margin = new Thickness(8, 4, 8, 4) };
        _authTokenBox.Placeholder("{{auth_token}}");
        _authUsernameBox = new TextBox { FontSize = 12, Foreground = TextPri, Margin = new Thickness(8, 4, 8, 4) };
        _authUsernameBox.Placeholder("用户名");
        _authPasswordBox = new TextBox { FontSize = 12, Foreground = TextPri, Margin = new Thickness(8, 4, 8, 4) };
        _authPasswordBox.Placeholder("密码");

        _authNoneContent = new Border
        {
            Background = Color.Transparent,
            Child = new TextBlock
            {
                Text                = "无认证",
                FontSize            = 12,
                Foreground          = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 16),
            },
        };

        var basicForm = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, Margin = new Thickness(0, 8) };
        basicForm.Add(new TextBlock { Text = "用户名", FontSize = 11, Foreground = TextSec, Margin = new Thickness(8, 0) });
        basicForm.Add(_authUsernameBox);
        basicForm.Add(new TextBlock { Text = "密码", FontSize = 11, Foreground = TextSec, Margin = new Thickness(8, 0) });
        basicForm.Add(_authPasswordBox);
        _authBasicContent = basicForm;

        var bearerForm = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, Margin = new Thickness(0, 8) };
        bearerForm.Add(new TextBlock { Text = "Token", FontSize = 11, Foreground = TextSec, Margin = new Thickness(8, 0) });
        bearerForm.Add(_authTokenBox);
        bearerForm.Add(new TextBlock
        {
            Text       = "ⓘ 将自动设置 Authorization: Bearer ... Header",
            FontSize   = 11,
            Foreground = TextSec,
            Margin     = new Thickness(8, 4),
        });
        _authBearerContent = bearerForm;

        _authContent = new Border { Background = BgBase, Child = _authNoneContent };

        _authNoneBtn = new Button { Height = 24, Width = 52 };
        _authNoneBtn.Content("None",   false).FontSize(11).Background(BgSurface).Foreground(TextPri)
            .OnClick(() => SetAuthMode(0));
        _authBasicBtn = new Button { Height = 24, Width = 52 };
        _authBasicBtn.Content("Basic",  false).FontSize(11).Background(Color.Transparent).Foreground(TextSec)
            .OnClick(() => SetAuthMode(1));
        _authBearerBtn = new Button { Height = 24, Width = 68 };
        _authBearerBtn.Content("Bearer", false).FontSize(11).Background(Color.Transparent).Foreground(TextSec)
            .OnClick(() => SetAuthMode(2));

        var authTypeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing     = 4,
            Margin      = new Thickness(8, 8),
        };
        authTypeRow.Add(_authNoneBtn);
        authTypeRow.Add(_authBasicBtn);
        authTypeRow.Add(_authBearerBtn);

        var authPanel = new DockPanel();
        authPanel.Add(authTypeRow.DockTop());
        authPanel.Add(_authContent);

        var tabControl = new TabControl();
        var rawTabItem = new TabItem().Header("Raw", false).Content(_textEditorWithHint);
        tabControl.TabItems(
            new TabItem().Header("Params",     false).Content(paramsScroll),
            new TabItem().Header("Headers",    false).Content(headersScroll),
            new TabItem().Header("Auth",       false).Content(new Border { Background = BgBase, Child = authPanel }),
            new TabItem().Header("Body",       false).Content(new Border { Background = BgBase, Child = bodyContent }),
            new TabItem().Header("Assertions", false).Content(assertScroll),
            rawTabItem
        );
        // 切换到"原文"Tab 时同步结构化→文本；切走时同步文本→结构化
        tabControl.OnSelectionChanged(o =>
        {
            bool nowRaw = ReferenceEquals(o, rawTabItem);
            if (nowRaw && !_isRawTabActive) SyncStructuredToText();
            else if (!nowRaw && _isRawTabActive) SyncTextToStructured();
            _isRawTabActive = nowRaw;
        });
        _tabControl = tabControl;

        // ── 空状态覆盖层 ──────────────────────────────────────────
        _emptyOverlay = new Border
        {
            Background = BgBase,
            Child = new TextBlock
            {
                Text      = "← 从侧边栏点击一个请求开始编辑",
                FontSize  = 13,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };

        // ── 内容区 ────────────────────────────────────────────────
        _contentArea = new Border { Background = BgBase, Child = _emptyOverlay };

        // ── 根布局 ────────────────────────────────────────────────
        _root = new DockPanel();
        _root.Add(toolbarBorder.DockTop());
        _root.Add(_curlImportRow.DockTop());
        _root.Add(_urlPreviewRow.DockTop());
        _root.Add(_contentArea);

        // ── Dirty 追踪 + F5/F6 语法提示 ──────────────────────────
        _urlBox.OnTextChanged(text => { SetDirty(true); UpdateUrlPreview(text); });
        _textEditor.OnTextChanged(text => { SetDirty(true); UpdateSyntaxHint(text); });
        _bodyText.OnTextChanged(_ => SetDirty(true));
        _methodCombo.OnSelectionChanged(_ => SetDirty(true));
        _contentTypeCombo.OnSelectionChanged(_ => SetDirty(true));
    }

    // ── 公共方法 ─────────────────────────────────────────────────

    /// <summary>加载请求到编辑器（双模式均更新）。</summary>
    public void Load(HttpRequestDefinition req)
    {
        // 更新方法
        var methodIdx = Array.IndexOf(Methods, req.Method.ToUpperInvariant());
        _methodCombo.SelectedIndex(methodIdx >= 0 ? methodIdx : 0);

        // 更新 URL
        _urlBox.Text = req.Url;

        // 更新文本编辑器
        _textEditor.Text = BuildRawText(req);

        // 更新结构化表单
        PopulateStructuredForm(req);

        // 清除 dirty 状态
        SetDirty(false);

        // 切换到空状态之外（首次加载）
        if (!_hasLoaded)
        {
            _hasLoaded = true;
            _contentArea.Child = _tabControl;
        }
    }

    /// <summary>触发保存（由 MainWindow 的 Ctrl+S 调用）。</summary>
    public void TriggerSave()
    {
        if (string.IsNullOrEmpty(CurrentFilePath)) return;
        HttpRequestDefinition req;
        // 若当前在"原文"Tab，从文本解析；否则从结构化表单构建
        if (_isRawTabActive)
        {
            var raw = _textEditor.Text;
            if (string.IsNullOrWhiteSpace(raw)) return;
            try
            {
                var fd = HttpFileParser.ParseContent(raw);
                if (fd.Requests.Count == 0) return;
                req = fd.Requests[0];
            }
            catch { return; }
        }
        else
        {
            req = BuildDefinitionFromStructured();
        }
        SaveRequested?.Invoke(CurrentFilePath, req);
        SetDirty(false);
    }

    /// <summary>获取当前编辑器内容快照（用于标签关闭时缓存状态）。</summary>
    public HttpRequestDefinition? GetCurrentDefinition()
    {
        if (!_hasLoaded) return null;
        try
        {
            if (_isRawTabActive)
            {
                var raw = _textEditor.Text;
                if (string.IsNullOrWhiteSpace(raw)) return null;
                var fd = HttpFileParser.ParseContent(raw);
                return fd.Requests.Count > 0 ? fd.Requests[0] : null;
            }
            return BuildDefinitionFromStructured();
        }
        catch { return null; }
    }

    // ── 同步逻辑 ─────────────────────────────────────────────────

    /// <summary>文本模式 → 结构化：解析原始文本，填入结构化表单。</summary>
    private void SyncTextToStructured()
    {
        try
        {
            var fileDef = HttpFileParser.ParseContent(_textEditor.Text);
            if (fileDef.Requests.Count == 0) return;
            var req = fileDef.Requests[0];

            var methodIdx = Array.IndexOf(Methods, req.Method.ToUpperInvariant());
            _methodCombo.SelectedIndex(methodIdx >= 0 ? methodIdx : 0);
            _urlBox.Text = req.Url;
            PopulateStructuredForm(req);
        }
        catch
        {
            // 解析失败时不修改结构化表单
        }
    }

    /// <summary>结构化模式 → 文本：序列化表单到原始文本。</summary>
    private void SyncStructuredToText()
    {
        var req = BuildDefinitionFromStructured();
        _textEditor.Text = BuildRawText(req);
    }

    // ── 结构化表单 ────────────────────────────────────────────────

    private void PopulateStructuredForm(HttpRequestDefinition req)
    {
        // Auth 检测（必须在 headers 填入前执行）
        if (req.Headers.TryGetValue("Authorization", out var authVal))
        {
            if (authVal.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                SetAuthMode(2);
                _authTokenBox.Text = authVal[7..].Trim();
            }
            else if (authVal.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                SetAuthMode(1);
                try
                {
                    var decoded  = Encoding.UTF8.GetString(Convert.FromBase64String(authVal[6..].Trim()));
                    var colonIdx = decoded.IndexOf(':');
                    _authUsernameBox.Text = colonIdx >= 0 ? decoded[..colonIdx] : decoded;
                    _authPasswordBox.Text = colonIdx >= 0 ? decoded[(colonIdx + 1)..] : string.Empty;
                }
                catch { SetAuthMode(0); }
            }
            else
            {
                SetAuthMode(0);
            }
        }
        else
        {
            SetAuthMode(0);
        }

        // 清空并填入 params（从 URL 解析 query string）
        while (_paramRows.Children.Count > 0)
            _paramRows.RemoveAt(0);
        var (baseUrl, queryPairs) = SplitUrlAndParams(req.Url);
        foreach (var (k, v) in queryPairs)
            _paramRows.Add(BuildParamRow(k, v));

        // 清空 header 行
        while (_headerRows.Children.Count > 0)
            _headerRows.RemoveAt(0);

        // 填入 headers（Authorization 由 Auth Tab 管理，若有则跳过）
        foreach (var (k, v) in req.Headers)
        {
            if (_authMode != 0 && string.Equals(k, "Authorization", StringComparison.OrdinalIgnoreCase))
                continue;
            _headerRows.Add(BuildHeaderRow(k, v));
        }

        // 填入 body
        _bodyText.Text = req.Body ?? string.Empty;

        // 设置 Content-Type（如有）
        if (req.Headers.TryGetValue("Content-Type", out var ct))
        {
            var ctIdx = Array.IndexOf(["application/json", "text/plain", "application/x-www-form-urlencoded", "multipart/form-data"], ct.Split(';')[0].Trim());
            if (ctIdx >= 0) _contentTypeCombo.SelectedIndex(ctIdx);
        }

        // 清空并填入断言
        while (_assertionRows.Children.Count > 0)
            _assertionRows.RemoveAt(0);
        foreach (var assertion in req.Assertions)
            _assertionRows.Add(BuildAssertionRow(assertion.RawText));
    }

    private UIElement BuildHeaderRow(string key, string value)
    {
        var keyBox = new TextBox
        {
            Text      = key,
            FontSize  = 12,
            Foreground = TextPri,
            Width     = 180,
        };
        keyBox.Placeholder("Header 名称");

        var valBox = new TextBox
        {
            Text      = value,
            FontSize  = 12,
            Foreground = TextPri,
        };
        valBox.Placeholder("值");

        var delBtn = new Button { Width = 24, Height = 24 };
        delBtn.Content("✕", false).FontSize(11)
              .Background(Color.Transparent).Foreground(TextSec);

        var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
        row.Add(delBtn.DockRight());
        row.Add(new Border { Width = 4 }.DockRight());
        row.Add(keyBox.DockLeft());
        row.Add(new Border { Width = 4 }.DockLeft());
        row.Add(valBox);

        delBtn.Click += () => _headerRows.Remove(row);
        keyBox.OnTextChanged(_ => SetDirty(true));
        valBox.OnTextChanged(_ => SetDirty(true));

        return row;
    }

    private void AddEmptyHeaderRow() => _headerRows.Add(BuildHeaderRow("", ""));

    private UIElement BuildParamRow(string key, string value, bool enabled = true)
    {
        var keyBox = new TextBox { Text = key, FontSize = 12, Foreground = TextPri, Width = 180 };
        keyBox.Placeholder("参数名");
        var valBox = new TextBox { Text = value, FontSize = 12, Foreground = TextPri };
        valBox.Placeholder("値");
        var chk = new CheckBox { Width = 16, Height = 16, Margin = new Thickness(4, 0) };
        chk.OnCheckedChanged(v => { keyBox.Foreground(v ? TextPri : TextSec); valBox.Foreground(v ? TextPri : TextSec); });
        if (enabled) chk.IsChecked(true);
        var delBtn = new Button { Width = 24, Height = 24 };
        delBtn.Content("✕", false).FontSize(11).Background(Color.Transparent).Foreground(TextSec);
        var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
        row.Add(chk.DockLeft());
        row.Add(delBtn.DockRight());
        row.Add(new Border { Width = 4 }.DockRight());
        row.Add(keyBox.DockLeft());
        row.Add(new Border { Width = 4 }.DockLeft());
        row.Add(valBox);
        delBtn.Click += () => _paramRows.Remove(row);
        keyBox.OnTextChanged(_ => SetDirty(true));
        valBox.OnTextChanged(_ => SetDirty(true));
        return row;
    }

    private void AddEmptyParamRow() => _paramRows.Add(BuildParamRow("", ""));

    private UIElement BuildAssertionRow(string text)
    {
        var textBox = new TextBox { Text = text, FontSize = 12, Foreground = TextPri };
        textBox.Placeholder("assert status == 200");
        var delBtn = new Button { Width = 24, Height = 24 };
        delBtn.Content("✕", false).FontSize(11).Background(Color.Transparent).Foreground(TextSec);
        var row = new DockPanel { Margin = new Thickness(8, 1, 8, 1) };
        row.Add(delBtn.DockRight());
        row.Add(new Border { Width = 4 }.DockRight());
        row.Add(textBox);
        delBtn.Click += () => _assertionRows.Remove(row);
        return row;
    }

    private void AddEmptyAssertionRow() => _assertionRows.Add(BuildAssertionRow(""));

    private void SetAuthMode(int mode)
    {
        _authMode = mode;
        _authContent.Child = mode switch
        {
            1 => _authBasicContent,
            2 => _authBearerContent,
            _ => _authNoneContent,
        };
        _authNoneBtn .Background(mode == 0 ? BgSurface : Color.Transparent).Foreground(mode == 0 ? TextPri : TextSec);
        _authBasicBtn.Background(mode == 1 ? BgSurface : Color.Transparent).Foreground(mode == 1 ? TextPri : TextSec);
        _authBearerBtn.Background(mode == 2 ? BgSurface : Color.Transparent).Foreground(mode == 2 ? TextPri : TextSec);
    }

    /// <summary>将 URL 拆分为 base URL 和 query 键值对。</summary>
    private static (string baseUrl, List<(string k, string v)> pairs) SplitUrlAndParams(string url)
    {
        var qIdx = url.IndexOf('?');
        if (qIdx < 0) return (url, []);
        var baseUrl = url[..qIdx];
        var query   = url[(qIdx + 1)..];
        var pairs   = new List<(string, string)>();
        foreach (var seg in query.Split('&'))
        {
            if (string.IsNullOrEmpty(seg)) continue;
            var eqIdx = seg.IndexOf('=');
            if (eqIdx < 0)
                pairs.Add((Uri.UnescapeDataString(seg), string.Empty));
            else
                pairs.Add((Uri.UnescapeDataString(seg[..eqIdx]), Uri.UnescapeDataString(seg[(eqIdx + 1)..])));
        }
        return (baseUrl, pairs);
    }

    private HttpRequestDefinition BuildDefinitionFromStructured()
    {
        var method = Methods[_methodCombo.SelectedIndex >= 0 ? _methodCombo.SelectedIndex : 0];
        var body   = string.IsNullOrWhiteSpace(_bodyText.Text) ? null : _bodyText.Text;

        // 重建 URL：取 URL 框中的 base（去掉已有 query string），再拼接 Params 行
        var (baseUrl, _) = SplitUrlAndParams(_urlBox.Text ?? string.Empty);
        var paramPairs = new List<(string k, string v)>();
        foreach (var child in _paramRows.Children)
        {
            if (child is not DockPanel row) continue;
            // 检查勾选框状态
            bool chkChecked = true;
            string? k = null, v = null;
            foreach (var el in row.Children)
            {
                if (el is CheckBox cb) { chkChecked = cb.IsChecked == true; continue; }
                if (el is TextBox tb) { if (k is null) k = tb.Text; else v = tb.Text; }
            }
            if (chkChecked && !string.IsNullOrWhiteSpace(k))
                paramPairs.Add((k, v ?? string.Empty));
        }
        var url = paramPairs.Count > 0
            ? baseUrl + "?" + string.Join("&", paramPairs.Select(p =>
                Uri.EscapeDataString(p.k) + "=" + Uri.EscapeDataString(p.v)))
            : baseUrl;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Content-Type from combo (only when body exists)
        if (body is not null)
        {
            var ctValues = new[] { "application/json", "text/plain", "application/x-www-form-urlencoded", "multipart/form-data" };
            var ctIdx = _contentTypeCombo.SelectedIndex;
            if (ctIdx >= 0 && ctIdx < ctValues.Length)
                headers["Content-Type"] = ctValues[ctIdx];
        }

        // User-specified headers from rows
        foreach (var child in _headerRows.Children)
        {
            if (child is not DockPanel row) continue;
            bool chkChecked = true;
            string? k = null, v = null;
            foreach (var el in row.Children)
            {
                if (el is CheckBox cb) { chkChecked = cb.IsChecked == true; continue; }
                if (el is TextBox tb)
                {
                    if (k is null) k = tb.Text;
                    else v = tb.Text;
                }
            }
            if (chkChecked && !string.IsNullOrWhiteSpace(k))
                headers[k] = v ?? string.Empty;
        }

        // Assertions from rows
        var assertLines = new List<string>();
        foreach (var child in _assertionRows.Children)
        {
            if (child is not DockPanel row) continue;
            foreach (var el in row.Children)
            {
                if (el is TextBox tb && !string.IsNullOrWhiteSpace(tb.Text))
                { assertLines.Add(tb.Text.Trim()); break; }
            }
        }
        var assertions = AssertionParser.ParseBlock(assertLines);

        // Auth header 注入
        switch (_authMode)
        {
            case 1: // Basic
                var user  = _authUsernameBox.Text ?? string.Empty;
                var pass  = _authPasswordBox.Text ?? string.Empty;
                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
                headers["Authorization"] = $"Basic {creds}";
                break;
            case 2: // Bearer
                var token = _authTokenBox.Text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(token))
                    headers["Authorization"] = $"Bearer {token}";
                break;
        }

        return new HttpRequestDefinition
        {
            Method     = method,
            Url        = url,
            Headers    = headers,
            Body       = body,
            Assertions = assertions,
        };
    }

    // ── 发送 ─────────────────────────────────────────────────────

    private void OnSendClicked()
    {
        HttpRequestDefinition req;
        if (_isRawTabActive)
        {
            var raw = _textEditor.Text;
            if (string.IsNullOrWhiteSpace(raw)) return;
            try
            {
                var fileDef = HttpFileParser.ParseContent(raw);
                if (fileDef.Requests.Count == 0) return;
                req = fileDef.Requests[0];
            }
            catch { return; }
        }
        else
        {
            req = BuildDefinitionFromStructured();
        }
        SendRequested?.Invoke(req);
    }

    private void OnCancelClicked() => CancelRequested?.Invoke();

    // ── 辅助 ─────────────────────────────────────────────────────

    private void UpdateMethodColor()
    {
        // ComboBox 颜色由主题控制，暂无需额外操作
    }

    private static string BuildRawText(HttpRequestDefinition req)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(req.Name))
            sb.Append("### ").AppendLine(req.Name);
        sb.Append(req.Method).Append(' ').AppendLine(req.Url);
        foreach (var (k, v) in req.Headers)
            sb.Append(k).Append(": ").AppendLine(v);
        if (req.Body is not null)
        {
            sb.AppendLine();
            sb.AppendLine(req.Body);
        }
        if (req.Assertions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("> {%");
            foreach (var a in req.Assertions)
                sb.AppendLine(a.RawText);
            sb.AppendLine("%}");
        }
        return sb.ToString();
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cmd", "/c clip")
            {
                RedirectStandardInput = true,
                UseShellExecute       = false,
                CreateNoWindow        = true,
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
