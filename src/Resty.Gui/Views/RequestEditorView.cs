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

    // ── 模式 ─────────────────────────────────────────────────────
    private bool _isStructuredMode = false;

    // ── 工具栏控件 ───────────────────────────────────────────────
    private readonly ComboBox _methodCombo;
    private readonly TextBox  _urlBox;
    private readonly Button   _sendBtn;
    private readonly Button   _modeTextBtn;
    private readonly Button   _modeStructBtn;

    // ── 内容区 ───────────────────────────────────────────────────
    private readonly Border            _contentArea;
    private readonly MultiLineTextBox  _textEditor;
    private readonly UIElement         _structuredPanel;

    // ── 结构化模式内控件 ─────────────────────────────────────────
    private readonly StackPanel  _paramRows;      // 动态 query param 行
    private readonly StackPanel  _headerRows;     // 动态 header 行
    private readonly MultiLineTextBox _bodyText;  // Body tab 中的文本框
    private readonly ComboBox    _contentTypeCombo;
    private readonly StackPanel  _assertionRows;  // 动态断言行

    // ── 根布局 ───────────────────────────────────────────────────
    private readonly DockPanel _root;

    // ── 空状态覆盖层 ─────────────────────────────────────────────
    private readonly UIElement _emptyOverlay;
    private bool _hasLoaded = false;

    // ── 公共接口 ─────────────────────────────────────────────────
    public UIElement RootElement => _root;
    public Action<HttpRequestDefinition>? SendRequested;

    public RequestEditorView()
    {
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

        // ── 模式切换按钮 ──────────────────────────────────────────
        _modeTextBtn = new Button { Height = 24, Width = 60 };
        _modeTextBtn.Content("文本", false).FontSize(12)
            .Background(BgSurface).Foreground(TextPri)
            .OnClick(SwitchToTextMode);

        _modeStructBtn = new Button { Height = 24, Width = 64 };
        _modeStructBtn.Content("结构化", false).FontSize(12)
            .Background(Color.Transparent).Foreground(TextSec)
            .OnClick(SwitchToStructuredMode);

        // ── 工具栏 ────────────────────────────────────────────────
        var modeRow = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            Spacing           = 4,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
        };
        modeRow.Add(_modeTextBtn);
        modeRow.Add(_modeStructBtn);

        var urlRow = new DockPanel();
        urlRow.Add(new Border { Width = 8 }.DockLeft());
        urlRow.Add(_methodCombo.DockLeft());
        urlRow.Add(new Border { Width = 8 }.DockLeft());
        urlRow.Add(new Border { Width = 8 }.DockRight());
        urlRow.Add(_sendBtn.DockRight());
        urlRow.Add(_urlBox);

        var toolbarInner = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing     = 4,
        };
        toolbarInner.Add(urlRow);
        toolbarInner.Add(modeRow);

        var toolbarBorder = new Border
        {
            Height      = 56,
            Background  = BgPanel,
            BorderBrush = BorderCol,
            Child       = toolbarInner,
        };

        // ── 文本编辑器 ────────────────────────────────────────────
        _textEditor = new MultiLineTextBox
        {
            FontSize   = 13,
            Foreground = TextPri,
            Background = BgBase,
            Padding    = new Thickness(12, 8, 12, 8),
        };
        _textEditor.Wrap(false);

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

        var tabControl = new TabControl();
        tabControl.TabItems(
            new TabItem().Header("Params", false).Content(paramsScroll),
            new TabItem().Header("Headers", false).Content(headersScroll),
            new TabItem().Header("Body", false).Content(new Border { Background = BgBase, Child = bodyContent }),
            new TabItem().Header("Assertions", false).Content(assertScroll)
        );

        _structuredPanel = tabControl;

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
        _root.Add(_contentArea);
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

        // 切换到空状态之外（首次加载）
        if (!_hasLoaded)
        {
            _hasLoaded = true;
            _contentArea.Child = _isStructuredMode ? _structuredPanel : _textEditor;
        }
    }

    // ── 模式切换 ─────────────────────────────────────────────────

    private void SwitchToTextMode()
    {
        if (!_isStructuredMode) return;
        SyncStructuredToText();
        _isStructuredMode = false;
        _modeTextBtn.Background(BgSurface).Foreground(TextPri);
        _modeStructBtn.Background(Color.Transparent).Foreground(TextSec);
        if (_hasLoaded) _contentArea.Child = _textEditor;
    }

    private void SwitchToStructuredMode()
    {
        if (_isStructuredMode) return;
        SyncTextToStructured();
        _isStructuredMode = true;
        _modeStructBtn.Background(BgSurface).Foreground(TextPri);
        _modeTextBtn.Background(Color.Transparent).Foreground(TextSec);
        if (_hasLoaded) _contentArea.Child = _structuredPanel;
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
        // 清空并填入 params（从 URL 解析 query string）
        while (_paramRows.Children.Count > 0)
            _paramRows.RemoveAt(0);
        var (baseUrl, queryPairs) = SplitUrlAndParams(req.Url);
        foreach (var (k, v) in queryPairs)
            _paramRows.Add(BuildParamRow(k, v));

        // 清空 header 行
        while (_headerRows.Children.Count > 0)
            _headerRows.RemoveAt(0);

        // 填入 headers
        foreach (var (k, v) in req.Headers)
            _headerRows.Add(BuildHeaderRow(k, v));

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

        return row;
    }

    private void AddEmptyHeaderRow() => _headerRows.Add(BuildHeaderRow("", ""));

    private UIElement BuildParamRow(string key, string value)
    {
        var keyBox = new TextBox { Text = key, FontSize = 12, Foreground = TextPri, Width = 180 };
        keyBox.Placeholder("参数名");
        var valBox = new TextBox { Text = value, FontSize = 12, Foreground = TextPri };
        valBox.Placeholder("值");
        var delBtn = new Button { Width = 24, Height = 24 };
        delBtn.Content("✕", false).FontSize(11).Background(Color.Transparent).Foreground(TextSec);
        var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
        row.Add(delBtn.DockRight());
        row.Add(new Border { Width = 4 }.DockRight());
        row.Add(keyBox.DockLeft());
        row.Add(new Border { Width = 4 }.DockLeft());
        row.Add(valBox);
        delBtn.Click += () => _paramRows.Remove(row);
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
            string? k = null, v = null;
            foreach (var el in row.Children)
            {
                if (el is TextBox tb) { if (k is null) k = tb.Text; else v = tb.Text; }
            }
            if (!string.IsNullOrWhiteSpace(k))
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
            string? k = null, v = null;
            foreach (var el in row.Children)
            {
                if (el is TextBox tb)
                {
                    if (k is null) k = tb.Text;
                    else v = tb.Text;
                }
            }
            if (!string.IsNullOrWhiteSpace(k))
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
        if (_isStructuredMode)
        {
            req = BuildDefinitionFromStructured();
        }
        else
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
        SendRequested?.Invoke(req);
    }

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
}
