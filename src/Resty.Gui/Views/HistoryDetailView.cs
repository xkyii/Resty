using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Models;
using Resty.Gui.Services;

namespace Resty.Gui.Views;

/// <summary>
/// 历史记录详情视图，显示在右侧主区域。
/// 使用标签页分区展示：信息 / 请求 / 响应 / 断言 / Raw。
/// </summary>
public sealed class HistoryDetailView
{
    // ── 颜色 ─────────────────────────────────────────────────────
    private static readonly Color BgBase    = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color BgPanel   = Color.FromRgb(0x25, 0x25, 0x26);
    private static readonly Color BgCode    = Color.FromRgb(0x1A, 0x1A, 0x1C);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);
    private static readonly Color AccentBlue= Color.FromRgb(0x00, 0x7A, 0xCC);

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

    // ── 空状态 ────────────────────────────────────────────────────
    public UIElement RootElement { get; } = new Border
    {
        Background = BgBase,
        Child = new TextBlock
        {
            Text                = "选择一条历史记录查看详情",
            FontSize            = 13,
            Foreground          = TextSec,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        },
    };

    /// <summary>点击"在编辑器打开"时触发：(filePath, requestName)</summary>
    public event Action<string, string>? OpenRequested;

    // ── 主构建方法 ────────────────────────────────────────────────
    public UIElement BuildView(HistoryRecord record)
    {
        var s = record.Summary;

        // ── 标签页 ────────────────────────────────────────────
        var rawText    = HlogSerializer.Serialize(record);
        var tabControl = new TabControl();
        tabControl.TabItems(
            new TabItem().Header("信息", false).Content(BuildInfoTab(s)),
            new TabItem().Header("请求", false).Content(BuildHttpTab(record.RequestSection)),
            new TabItem().Header("响应", false).Content(BuildHttpTab(record.ResponseSection)),
            new TabItem().Header("断言", false).Content(BuildAssertionsTab(record.AssertionsSection)),
            new TabItem().Header("Raw",  false).Content(BuildTextTab(rawText))
        );

        var root = new DockPanel();
        root.Add(tabControl);
        return new Border { Background = BgBase, Child = root };
    }

    // ── Tab: 信息 ────────────────────────────────────────────────
    private static UIElement BuildInfoTab(HistorySummary s)
    {
        var grid = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10, Margin = new Thickness(24, 20) };

        void Row(string label, UIElement value)
        {
            var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = TextSec,
                Width = 64, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Add(lbl); row.Add(value);
            grid.Add(row);
        }
        UIElement Txt(string text, Color? color = null, bool bold = false) =>
            new TextBlock { Text = text, FontSize = 12, Foreground = color ?? TextPri,
                FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
                VerticalAlignment = VerticalAlignment.Center };

        var statusColor = s.StatusCode switch
        {
            >= 200 and < 300 => Color.FromRgb(0x4E, 0xC9, 0xB0),
            >= 300 and < 400 => Color.FromRgb(0x4F, 0xC1, 0xFF),
            >= 400 and < 500 => Color.FromRgb(0xCE, 0x91, 0x78),
            >= 500           => Color.FromRgb(0xF4, 0x47, 0x47),
            _                => TextSec,
        };
        var statusText = s.Error is not null ? $"Error: {s.Error}"
                       : s.StatusCode > 0   ? s.StatusCode.ToString() : "—";

        var methodBadge = new Border
        {
            Padding = new Thickness(8, 3), CornerRadius = 3,
            Background = MethodColor(s.Method).WithAlpha(46),
            Child = new TextBlock { Text = s.Method, FontSize = 11,
                FontWeight = FontWeight.SemiBold, Foreground = MethodColor(s.Method) },
        };

        Row("请求名", Txt(s.RequestName, bold: true));
        Row("方法",   methodBadge);
        Row("URL",    new TextBlock { Text = s.Url, FontSize = 12, Foreground = TextPri });
        Row("状态",   Txt(statusText, statusColor, bold: true));
        Row("耗时",   Txt($"{s.ElapsedMs} ms"));
        Row("时间",   Txt(s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")));
        if (!string.IsNullOrEmpty(s.FilePath))
        {
            Row("来源", Txt(Path.GetFileName(s.FilePath)));
            Row("路径", new TextBlock { Text = s.FilePath, FontSize = 11, Foreground = TextSec });
        }

        var scroll = new ScrollViewer { VerticalScroll = ScrollMode.Auto, Content = grid };
        return new Border { Background = BgBase, Child = scroll };
    }

    // ── Tab: 请求 / 响应 ─────────────────────────────────────────
    private static UIElement BuildHttpTab(string rawSection)
    {
        if (string.IsNullOrWhiteSpace(rawSection))
            return EmptyPane("（无内容）");

        // 按第一个空行切分 start-line+headers / body
        var sep = rawSection.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (sep < 0) sep = rawSection.IndexOf("\n\n", StringComparison.Ordinal);

        string headersPart, bodyPart;
        if (sep >= 0)
        {
            headersPart = rawSection[..sep].TrimEnd();
            var skip    = rawSection[sep] == '\r' ? 4 : 2;
            bodyPart    = rawSection[(sep + skip)..].TrimEnd();
        }
        else
        {
            headersPart = rawSection.TrimEnd();
            bodyPart    = string.Empty;
        }

        var headersBox  = MakeReadonlyBox(headersPart, BgPanel);
        var headersPane = new Border { Background = BgPanel, Child = headersBox };

        if (string.IsNullOrEmpty(bodyPart))
        {
            var root0 = new DockPanel();
            root0.Add(headersPane);
            return new Border { Background = BgCode, Child = root0 };
        }

        var bodyBox  = MakeReadonlyBox(bodyPart, BgCode);
        var bodyPane = new Border { Background = BgCode, Child = bodyBox };

        var root = new DockPanel();
        root.Add(headersPane.DockTop());
        root.Add(new Border { Height = 1, Background = BorderCol }.DockTop());
        root.Add(bodyPane);
        return new Border { Background = BgCode, Child = root };
    }

    // ── Tab: 断言 ────────────────────────────────────────────────
    private static UIElement BuildAssertionsTab(string? assertionsSection)
    {
        if (string.IsNullOrWhiteSpace(assertionsSection))
            return EmptyPane("该请求无断言");

        var list = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6,
                                    Margin = new Thickness(20, 16) };
        foreach (var rawLine in assertionsSection.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ');
            if (string.IsNullOrEmpty(line)) continue;
            var passed = line.StartsWith("[PASS]", StringComparison.OrdinalIgnoreCase);
            var failed = line.StartsWith("[FAIL]", StringComparison.OrdinalIgnoreCase);
            var fg     = passed ? Color.FromRgb(0x4E, 0xC9, 0xB0)
                       : failed ? Color.FromRgb(0xF4, 0x47, 0x47) : TextSec;
            var icon   = new TextBlock { Text = passed ? "✓" : failed ? "✗" : "·",
                FontSize = 13, Foreground = fg, Width = 20 };
            var text   = new TextBlock { Text = line.Length > 6 ? line[6..].Trim() : line,
                FontSize = 12, Foreground = failed ? fg : TextPri };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Add(icon); row.Add(text);
            list.Add(row);
        }

        var scroll  = new ScrollViewer { VerticalScroll = ScrollMode.Auto, Content = list };
        return new Border { Background = BgBase, Child = scroll };
    }

    // ── Tab: Raw（纯文本，可选择）────────────────────────────────
    private static UIElement BuildTextTab(string text)
    {
        var box = MakeReadonlyBox(text, BgCode);
        return new Border { Background = BgCode, Child = box };
    }

    // ── 通用工厂 ─────────────────────────────────────────────────
    private static MultiLineTextBox MakeReadonlyBox(string text, Color bg)
    {
        var box = new MultiLineTextBox
        {
            FontSize        = 12,
            Foreground      = Color.FromRgb(0xD4, 0xD4, 0xD4),
            Background      = bg,
            Padding         = new Thickness(14, 8),
            BorderBrush     = Color.Transparent,
            BorderThickness = 0,
        };
        box.IsReadOnly(true);
        box.Wrap(false);
        box.Text = text;
        return box;
    }

    private static Button MakeButton(string label, Color bg, Color fg, Action onClick)
    {
        var btn = new Button { Height = 28, Padding = new Thickness(12, 0) };
        btn.Content(label, false).FontSize(11).Background(bg).Foreground(fg);
        var hover = bg == AccentBlue ? Color.FromRgb(0x00, 0x6B, 0xB3) : Color.FromRgb(0x3C, 0x3C, 0x3F);
        btn.MouseEnter += () => btn.Background(hover);
        btn.MouseLeave += () => btn.Background(bg);
        btn.OnClick(onClick);
        return btn;
    }

    private static UIElement EmptyPane(string msg) =>
        new Border
        {
            Background = BgBase,
            Child = new TextBlock
            {
                Text = msg, FontSize = 12, Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };

}
