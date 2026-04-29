using System.Text.Json;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Core.Models;

namespace Resty.Gui.Views;

/// <summary>
/// G2 响应面板：显示 HTTP 响应的状态码、耗时和响应体。
/// 支持四种状态：空、加载中、成功、错误。
/// </summary>
public sealed class ResponsePanelView
{
    // ── 颜色 ────────────────────────────────────────────────────
    private static readonly Color BgBase    = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color BgPanel   = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);

    // ── 控件 ────────────────────────────────────────────────────
    private readonly Border _root;

    // 响应成功区各控件（共用同一 DockPanel 结构，只更新 Text/Foreground）
    private readonly TextBlock         _statusBadge;
    private readonly TextBlock         _elapsedLabel;
    private readonly TextBlock         _sizeLabel;
    private readonly MultiLineTextBox  _bodyText;
    private readonly UIElement         _successPanel;

    // 状态占位控件
    private readonly UIElement _emptyState;
    private readonly UIElement _loadingState;
    private readonly StackPanel _headersPanel;
    private readonly StackPanel _assertionsPanel;

    // Body 子切换
    private bool _bodyShowTree = false;
    private string _lastBodyJson = string.Empty;
    private string _lastBodyRaw  = string.Empty;
    private readonly Button _bodyRawBtn;
    private readonly Button _bodyTreeBtn;
    private readonly Button _bodyCopyBtn;
    private readonly Border _bodyContentArea;  // 切换 raw 与 tree
    private readonly Border _bodyTreeScroll;   // 树视图容器
    private readonly Border _bodyColorScroll;  // JSON 着色原始视图容器

    // ── 公共接口 ─────────────────────────────────────────────────
    /// <summary>根元素，放入父布局。</summary>
    public UIElement RootElement => _root;

    public ResponsePanelView()
    {
        // ── 空状态 ────────────────────────────────────────────────
        _emptyState = BuildCenteredLabel("↑ 点击「▶ 发送」查看响应", TextSec, 13);

        // ── 加载状态 ──────────────────────────────────────────────
        _loadingState = BuildCenteredLabel("⟳ 请求发送中…", TextPri, 13);

        // ── 响应头：状态码 + 耗时 + 大小 ─────────────────────────
        _statusBadge = new TextBlock
        {
            Text      = "200 OK",
            FontSize  = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = StatusColor(200),
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(12, 0, 16, 0),
        };

        _elapsedLabel = new TextBlock
        {
            Text      = "0 ms",
            FontSize  = 12,
            Foreground = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(0, 0, 12, 0),
        };

        _sizeLabel = new TextBlock
        {
            Text      = "0 B",
            FontSize  = 12,
            Foreground = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(0, 0, 12, 0),
        };

        var headerBar = new DockPanel();
        headerBar.Add(_statusBadge.DockLeft());
        headerBar.Add(_elapsedLabel.DockLeft());
        headerBar.Add(_sizeLabel.DockLeft());

        var headerBorder = new Border
        {
            Height      = 36,
            Background  = BgPanel,
            BorderBrush = BorderCol,
            Child       = headerBar,
        };

        // ── 响应体文本框 ──────────────────────────────────────────
        _bodyText = new MultiLineTextBox
        {
            FontSize   = 13,
            Foreground = TextPri,
            Background = BgBase,
            Padding    = new Thickness(12, 8, 12, 8),
        };
        _bodyText.IsReadOnly(true);
        _bodyText.Wrap(true);
        // Body 子切换按钮
        _bodyRawBtn = new Button { Height = 22, Width = 52 };
        _bodyRawBtn.Content("原始", false).FontSize(11)
            .Background(Color.FromRgb(0x37, 0x37, 0x38)).Foreground(TextPri)
            .OnClick(() => SetBodyView(false));
        _bodyTreeBtn = new Button { Height = 22, Width = 60 };
        _bodyTreeBtn.Content("JSON 树", false).FontSize(11)
            .Background(Color.Transparent).Foreground(TextSec)
            .OnClick(() => SetBodyView(true));
        _bodyCopyBtn = new Button { Height = 22, Width = 44 };
        _bodyCopyBtn.Content("复制", false).FontSize(11)
            .Background(Color.Transparent).Foreground(TextSec)
            .OnClick(() => CopyToClipboard(_lastBodyRaw));
        _bodyCopyBtn.MouseEnter += () => _bodyCopyBtn.Background(Color.FromRgb(0x37, 0x37, 0x38)).Foreground(TextPri);
        _bodyCopyBtn.MouseLeave += () => _bodyCopyBtn.Background(Color.Transparent).Foreground(TextSec);

        var bodySubToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing     = 4,
            Margin      = new Thickness(8, 4),
        };
        bodySubToolbar.Add(_bodyRawBtn);
        bodySubToolbar.Add(_bodyTreeBtn);
        bodySubToolbar.Add(_bodyCopyBtn);

        _bodyContentArea = new Border { Background = BgBase, Child = _bodyText };
        _bodyTreeScroll  = new Border { Background = BgBase };
        _bodyColorScroll = new Border { Background = BgBase };

        var bodyTabContent = new DockPanel();
        bodyTabContent.Add(bodySubToolbar.DockTop());
        bodyTabContent.Add(_bodyContentArea);
        // ── 成功状态面板 ──────────────────────────────────────────
        _headersPanel    = new StackPanel { Orientation = Orientation.Vertical };
        _assertionsPanel = new StackPanel { Orientation = Orientation.Vertical };

        var headersScroll    = new ScrollViewer { VerticalScroll = ScrollMode.Auto, Content = _headersPanel };
        var assertionsScroll = new ScrollViewer { VerticalScroll = ScrollMode.Auto, Content = _assertionsPanel };

        var respTabControl = new TabControl();
        respTabControl.TabItems(
            new TabItem().Header("Body",       false).Content(new Border { Background = BgBase, Child = bodyTabContent }),
            new TabItem().Header("Headers",    false).Content(headersScroll),
            new TabItem().Header("Assertions", false).Content(assertionsScroll)
        );

        var successRoot = new DockPanel();
        successRoot.Add(headerBorder.DockTop());
        successRoot.Add(respTabControl);
        _successPanel = new Border { Background = BgBase, Child = successRoot };

        // ── 根容器（初始显示空状态） ──────────────────────────────
        _root = new Border { Background = BgBase, Child = _emptyState };
    }

    /// <summary>显示空状态（未发送）。</summary>
    public void ShowEmpty() => _root.Child = _emptyState;

    /// <summary>显示加载中状态。</summary>
    public void ShowLoading() => _root.Child = _loadingState;

    /// <summary>显示成功响应。</summary>
    public void ShowResult(HttpExecutionResult result, IReadOnlyList<AssertionResult>? assertionResults = null)
    {
        // 更新状态码
        _statusBadge.Text       = StatusText(result.StatusCode);
        _statusBadge.Foreground = StatusColor(result.StatusCode);

        // 更新耗时 & 大小
        _elapsedLabel.Text = $"{result.ElapsedMs} ms";
        _sizeLabel.Text    = FormatSize(System.Text.Encoding.UTF8.GetByteCount(result.Body));

        // 更新响应体（JSON 自动格式化）
        var displayBody = result.Body;
        bool isJson = false;
        if (!string.IsNullOrEmpty(displayBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(displayBody);
                displayBody = JsonSerializer.Serialize(doc.RootElement,
                    new JsonSerializerOptions { WriteIndented = true });
                isJson = true;
            }
            catch { /* 非 JSON，原样显示 */ }
        }
        _lastBodyRaw  = displayBody ?? string.Empty;
        _lastBodyJson = isJson ? displayBody! : string.Empty;
        _bodyText.Text = string.IsNullOrEmpty(displayBody) ? "(空响应体)" : displayBody;

        // 更新 JSON 树按钮状态（非 JSON 时禁用）
        if (isJson)
        {
            _bodyTreeBtn.Foreground(TextSec).Background(Color.Transparent);
            // JSON：原始视图使用着色渲染
            RefreshColorJson();
            _bodyContentArea.Child = _bodyColorScroll;
            _bodyRawBtn.Background(Color.FromRgb(0x37, 0x37, 0x38)).Foreground(TextPri);
        }
        else
        {
            // 强制切回原始视图
            _bodyShowTree = false;
            _bodyContentArea.Child = _bodyText;
            _bodyRawBtn.Background(Color.FromRgb(0x37, 0x37, 0x38)).Foreground(TextPri);
            _bodyTreeBtn.Foreground(Color.FromRgb(0x55, 0x55, 0x55)).Background(Color.Transparent);
        }

        // 若当前在树视图且有新 JSON，刷新树
        if (_bodyShowTree && isJson)
            RefreshJsonTree();

        // 更新响应头面板
        while (_headersPanel.Children.Count > 0) _headersPanel.RemoveAt(0);
        AddHeaderRow(_headersPanel, ":status", StatusColor(result.StatusCode), StatusText(result.StatusCode));
        AddHeaderRow(_headersPanel, ":time",   TextSec, $"{result.ElapsedMs} ms");
        AddHeaderRow(_headersPanel, ":size",   TextSec, FormatSize(System.Text.Encoding.UTF8.GetByteCount(result.Body)));
        if (result.Headers.Count > 0)
            _headersPanel.Add(new Border { Height = 1, Background = BorderCol, Margin = new Thickness(8, 4) });
        foreach (var (k, v) in result.Headers)
            AddHeaderRow(_headersPanel, k, Color.FromRgb(0x9C, 0xDC, 0xFE), v);

        // 更新断言面板
        while (_assertionsPanel.Children.Count > 0) _assertionsPanel.RemoveAt(0);
        if (assertionResults is not null && assertionResults.Count > 0)
        {
            var passCount  = assertionResults.Count(r => r.Passed);
            var allPassed  = passCount == assertionResults.Count;
            var summaryClr = allPassed ? Color.FromRgb(0x4E, 0xC9, 0xB0) : Color.FromRgb(0xF4, 0x47, 0x47);
            _assertionsPanel.Add(new TextBlock
            {
                Text       = allPassed
                    ? $"✓  {passCount} / {assertionResults.Count} 条断言通过"
                    : $"✗  {assertionResults.Count - passCount} / {assertionResults.Count} 条断言失败",
                FontSize   = 12,
                Foreground  = summaryClr,
                FontWeight  = FontWeight.SemiBold,
                Margin     = new Thickness(8, 6, 8, 2),
            });
            _assertionsPanel.Add(new Border { Height = 1, Background = BorderCol, Margin = new Thickness(0, 2, 0, 4) });
            foreach (var ar in assertionResults)
            {
                var icon      = ar.Passed ? "✓" : "✗";
                var iconColor = ar.Passed ? Color.FromRgb(0x4E, 0xC9, 0xB0) : Color.FromRgb(0xF4, 0x47, 0x47);
                var row = new DockPanel { Margin = new Thickness(0, 1) };
                row.Add(new TextBlock
                {
                    Text              = icon,
                    FontSize          = 12,
                    Foreground        = iconColor,
                    Width             = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(8, 2, 0, 2),
                }.DockLeft());
                var actual = ar.ActualValue is not null ? $"  → {ar.ActualValue}" : string.Empty;
                row.Add(new TextBlock
                {
                    Text              = actual,
                    FontSize          = 11,
                    Foreground        = TextSec,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(0, 2, 8, 2),
                }.DockRight());
                row.Add(new TextBlock
                {
                    Text              = ar.Rule?.RawText ?? string.Empty,
                    FontSize          = 12,
                    Foreground        = TextPri,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(4, 2),
                });
                _assertionsPanel.Add(row);
            }
        }
        else
        {
            _assertionsPanel.Add(new TextBlock
            {
                Text                = "此请求无断言规则",
                FontSize            = 12,
                Foreground          = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 16),
            });
        }

        _root.Child = _successPanel;
    }

    /// <summary>显示网络/传输错误。</summary>
    public void ShowError(string error)
    {
        var label = BuildCenteredLabel($"✗ 请求失败\n{error}", Color.FromRgb(0xF4, 0x47, 0x47), 13);
        _root.Child = label;
    }

    // ── Body 子切换 ──────────────────────────────────────────────

    private void SetBodyView(bool showTree)
    {
        if (showTree && string.IsNullOrEmpty(_lastBodyJson)) return; // 非 JSON 时禁止
        _bodyShowTree = showTree;
        if (showTree)
        {
            RefreshJsonTree();
            _bodyContentArea.Child = _bodyTreeScroll;
            _bodyTreeBtn.Background(Color.FromRgb(0x37, 0x37, 0x38)).Foreground(TextPri);
            _bodyRawBtn.Background(Color.Transparent).Foreground(TextSec);
        }
        else
        {
            // 原始视图：JSON 用着色，非 JSON 用 MultiLineTextBox
            if (!string.IsNullOrEmpty(_lastBodyJson))
            {
                RefreshColorJson();
                _bodyContentArea.Child = _bodyColorScroll;
            }
            else
            {
                _bodyContentArea.Child = _bodyText;
            }
            _bodyRawBtn.Background(Color.FromRgb(0x37, 0x37, 0x38)).Foreground(TextPri);
            _bodyTreeBtn.Background(Color.Transparent).Foreground(TextSec);
        }
    }

    private void RefreshColorJson()
    {
        try
        {
            var linesPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(12, 8) };
            RenderColorizedJsonLines(linesPanel, _lastBodyJson);
            var scroll = new ScrollViewer { VerticalScroll = ScrollMode.Auto, HorizontalScroll = ScrollMode.Auto, Content = linesPanel };
            _bodyColorScroll.Child = scroll;
        }
        catch
        {
            _bodyColorScroll.Child = BuildCenteredLabel("JSON 渲染失败", TextSec, 12);
        }
    }

    private static void RenderColorizedJsonLines(StackPanel parent, string json)
    {
        // 逐行着色，使用正则识别 key/value 对
        var keyRe   = new System.Text.RegularExpressions.Regex(@"^(\s*)(""(?:[^""\\]|\\.)*"")(\s*:\s*)(.*)$");
        var strRe   = new System.Text.RegularExpressions.Regex(@"^(""(?:[^""\\]|\\.)*"",?)$");
        var numRe   = new System.Text.RegularExpressions.Regex(@"^(-?\d[\d.eE+\-]*,?)$");
        var boolRe  = new System.Text.RegularExpressions.Regex(@"^(true|false|null),?$");

        static Color KeyColor()  => Color.FromRgb(0x9C, 0xDC, 0xFE);
        static Color StrColor()  => Color.FromRgb(0xCE, 0x91, 0x78);
        static Color NumColor()  => Color.FromRgb(0xB5, 0xCE, 0xA8);
        static Color BoolColor() => Color.FromRgb(0x56, 0x9C, 0xD6);
        static Color PunColor()  => Color.FromRgb(0x85, 0x85, 0x85);
        static Color TextPriCol()=> Color.FromRgb(0xCC, 0xCC, 0xCC);

        void AddSpan(StackPanel row, string text, Color color, bool mono = false) =>
            row.Add(new TextBlock
            {
                Text      = text,
                FontSize  = 12,
                Foreground = color,
                VerticalAlignment = VerticalAlignment.Center,
            });

        foreach (var rawLine in json.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            var km = keyRe.Match(line);
            if (km.Success)
            {
                // indent
                if (km.Groups[1].Length > 0)
                    AddSpan(row, km.Groups[1].Value, PunColor());
                // key (quoted string)
                AddSpan(row, km.Groups[2].Value, KeyColor());
                // colon
                AddSpan(row, km.Groups[3].Value, PunColor());
                // value
                var valPart = km.Groups[4].Value.TrimEnd();
                var trail   = valPart.EndsWith(',') ? "," : string.Empty;
                var val     = trail.Length > 0 ? valPart[..^1] : valPart;
                if (strRe.IsMatch(val) || val.StartsWith('"'))
                    AddSpan(row, val + trail, StrColor());
                else if (numRe.IsMatch(val))
                    AddSpan(row, val + trail, NumColor());
                else if (boolRe.IsMatch(val))
                    AddSpan(row, val + trail, BoolColor());
                else
                    AddSpan(row, valPart, PunColor());
            }
            else
            {
                var trimmed = line.Trim();
                if (strRe.IsMatch(trimmed))
                {
                    if (line.Length - trimmed.Length > 0)
                        AddSpan(row, line[..(line.Length - trimmed.Length)], PunColor());
                    AddSpan(row, trimmed, StrColor());
                }
                else if (numRe.IsMatch(trimmed))
                {
                    if (line.Length - trimmed.Length > 0)
                        AddSpan(row, line[..(line.Length - trimmed.Length)], PunColor());
                    AddSpan(row, trimmed, NumColor());
                }
                else if (boolRe.IsMatch(trimmed))
                {
                    if (line.Length - trimmed.Length > 0)
                        AddSpan(row, line[..(line.Length - trimmed.Length)], PunColor());
                    AddSpan(row, trimmed, BoolColor());
                }
                else
                {
                    // structural or empty
                    AddSpan(row, line, string.IsNullOrWhiteSpace(line) ? TextPriCol() : PunColor());
                }
            }
            parent.Add(row);
        }
    }

    private void RefreshJsonTree()
    {
        try
        {
            using var doc  = JsonDocument.Parse(_lastBodyJson);
            var treePanel  = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4) };
            RenderJsonElement(treePanel, doc.RootElement, null, 0);
            var scroll = new ScrollViewer { VerticalScroll = ScrollMode.Auto, Content = treePanel };
            _bodyTreeScroll.Child = scroll;
        }
        catch
        {
            _bodyTreeScroll.Child = BuildCenteredLabel("JSON 解析失败", TextSec, 12);
        }
    }

    private void RenderJsonElement(StackPanel parent, JsonElement elem, string? key, int depth)
    {
        const double IndentWidth = 16.0;
        switch (elem.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var childPanel = new StackPanel { Orientation = Orientation.Vertical };
                var isExpanded = true;
                foreach (var prop in elem.EnumerateObject())
                    RenderJsonElement(childPanel, prop.Value, prop.Name, depth + 1);
                var childBorder = new Border { Child = childPanel };

                var headerRow = new DockPanel { Margin = new Thickness(depth * IndentWidth, 0, 0, 0) };
                var toggleBtn = new Button { Width = 16, Height = 16 };
                toggleBtn.Content("▼", false).FontSize(9).Background(Color.Transparent).Foreground(TextSec);
                toggleBtn.Click += () =>
                {
                    isExpanded = !isExpanded;
                    childBorder.Child = isExpanded ? (UIElement)childPanel : new Border();
                    toggleBtn.Content(isExpanded ? "▼" : "▶", false);
                };
                headerRow.Add(toggleBtn.DockLeft());
                headerRow.Add(new TextBlock
                {
                    Text              = key is not null ? $"{key}:  {{  " : "{  ",
                    FontSize          = 12,
                    Foreground        = key is not null ? Color.FromRgb(0x9C, 0xDC, 0xFE) : TextSec,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(2, 1),
                });
                parent.Add(headerRow);
                parent.Add(childBorder);
                parent.Add(new TextBlock
                {
                    Text       = "}",
                    FontSize   = 12,
                    Foreground = TextSec,
                    Margin     = new Thickness(depth * IndentWidth + 18, 1),
                });
                break;
            }
            case JsonValueKind.Array:
            {
                var childPanel = new StackPanel { Orientation = Orientation.Vertical };
                var isExpanded = true;
                var idx = 0;
                foreach (var item in elem.EnumerateArray())
                    RenderJsonElement(childPanel, item, $"[{idx++}]", depth + 1);
                var childBorder = new Border { Child = childPanel };

                var headerRow = new DockPanel { Margin = new Thickness(depth * IndentWidth, 0, 0, 0) };
                var toggleBtn = new Button { Width = 16, Height = 16 };
                toggleBtn.Content("▼", false).FontSize(9).Background(Color.Transparent).Foreground(TextSec);
                toggleBtn.Click += () =>
                {
                    isExpanded = !isExpanded;
                    childBorder.Child = isExpanded ? (UIElement)childPanel : new Border();
                    toggleBtn.Content(isExpanded ? "▼" : "▶", false);
                };
                headerRow.Add(toggleBtn.DockLeft());
                headerRow.Add(new TextBlock
                {
                    Text              = key is not null ? $"{key}:  [  ({idx} 项)" : $"[  ({idx} 项)",
                    FontSize          = 12,
                    Foreground        = key is not null ? Color.FromRgb(0x9C, 0xDC, 0xFE) : TextSec,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(2, 1),
                });
                parent.Add(headerRow);
                parent.Add(childBorder);
                parent.Add(new TextBlock
                {
                    Text       = "]",
                    FontSize   = 12,
                    Foreground = TextSec,
                    Margin     = new Thickness(depth * IndentWidth + 18, 1),
                });
                break;
            }
            default:
            {
                var (valText, valColor) = elem.ValueKind switch
                {
                    JsonValueKind.String  => ($"\"{elem.GetString()}\"", Color.FromRgb(0xCE, 0x91, 0x78)),
                    JsonValueKind.Number  => (elem.GetRawText(),         Color.FromRgb(0xB5, 0xCE, 0xA8)),
                    JsonValueKind.True    => ("true",                    Color.FromRgb(0x56, 0x9C, 0xD6)),
                    JsonValueKind.False   => ("false",                   Color.FromRgb(0x56, 0x9C, 0xD6)),
                    JsonValueKind.Null    => ("null",                    TextSec),
                    _                    => (elem.GetRawText(),          TextPri),
                };
                var typeHint = elem.ValueKind switch
                {
                    JsonValueKind.String => "string",
                    JsonValueKind.Number => "number",
                    JsonValueKind.True or JsonValueKind.False => "bool",
                    JsonValueKind.Null   => "null",
                    _                   => string.Empty,
                };
                var leafRow = new DockPanel { Margin = new Thickness(depth * IndentWidth + 18, 0, 0, 0) };
                leafRow.Add(new TextBlock
                {
                    Text              = typeHint,
                    FontSize          = 11,
                    Foreground        = TextSec,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width             = 48,
                    Margin            = new Thickness(0, 1, 4, 1),
                }.DockRight());
                if (key is not null)
                    leafRow.Add(new TextBlock
                    {
                        Text              = key + ": ",
                        FontSize          = 12,
                        Foreground        = Color.FromRgb(0x9C, 0xDC, 0xFE),
                        VerticalAlignment = VerticalAlignment.Center,
                        Width             = 140,
                        Margin            = new Thickness(2, 1),
                    }.DockLeft());
                leafRow.Add(new TextBlock
                {
                    Text              = valText,
                    FontSize          = 12,
                    Foreground        = valColor,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(2, 1),
                });
                parent.Add(leafRow);
                break;
            }
        }
    }

    // ── 私有辅助 ─────────────────────────────────────────────────
    private static void AddHeaderRow(StackPanel panel, string key, Color keyColor, string value)
    {
        var row = new DockPanel();
        row.Add(new TextBlock
        {
            Text              = key,
            FontSize          = 12,
            Foreground        = keyColor,
            Width             = 200,
            Margin            = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center,
        }.DockLeft());
        row.Add(new TextBlock
        {
            Text              = value,
            FontSize          = 12,
            Foreground        = Color.FromRgb(0xCC, 0xCC, 0xCC),
            Margin            = new Thickness(0, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
        });

        // 用 Button 包装实现点击复制 + 悬停高亮
        var btn = new Button { Height = 26, Margin = new Thickness(0, 1) };
        btn.Content(row).Background(Color.Transparent);
        btn.MouseEnter += () => btn.Background(Color.FromRgb(0x2A, 0x2D, 0x2E));
        btn.MouseLeave += () => btn.Background(Color.Transparent);
        var capturedValue = value;
        btn.OnClick(() => CopyToClipboard(capturedValue));
        panel.Add(btn);
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
    private static UIElement BuildCenteredLabel(string text, Color color, double fontSize) =>
        new Border
        {
            Child = new TextBlock
            {
                Text      = text,
                FontSize  = fontSize,
                Foreground = color,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };

    private static string StatusText(int code) => code switch
    {
        200 => "200 OK",
        201 => "201 Created",
        204 => "204 No Content",
        301 => "301 Moved",
        302 => "302 Found",
        400 => "400 Bad Request",
        401 => "401 Unauthorized",
        403 => "403 Forbidden",
        404 => "404 Not Found",
        405 => "405 Method Not Allowed",
        422 => "422 Unprocessable",
        429 => "429 Too Many Requests",
        500 => "500 Internal Server Error",
        502 => "502 Bad Gateway",
        503 => "503 Service Unavailable",
        0   => "连接失败",
        _   => code.ToString(),
    };

    private static Color StatusColor(int code) => code switch
    {
        >= 200 and < 300 => Color.FromRgb(0x4E, 0xC9, 0xB0),  // 绿
        >= 300 and < 400 => Color.FromRgb(0x4F, 0xC1, 0xFF),  // 蓝
        >= 400 and < 500 => Color.FromRgb(0xCE, 0x91, 0x78),  // 橙
        >= 500           => Color.FromRgb(0xF4, 0x47, 0x47),  // 红
        _                => Color.FromRgb(0xF4, 0x47, 0x47),  // 错误红
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024        => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _             => $"{bytes / (1024.0 * 1024):F1} MB",
    };
}
