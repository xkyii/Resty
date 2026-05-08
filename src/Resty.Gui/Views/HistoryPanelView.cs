using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Models;

namespace Resty.Gui.Views;

/// <summary>
/// 请求历史面板：左侧导航栏中的紧凑列表。
/// 数据由 HistoryService 提供，点击条目时触发 EntrySelected 事件。
/// </summary>
public sealed class HistoryPanelView
{
    // ── 颜色 ─────────────────────────────────────────────────────
    private static readonly Color BgSidebar = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color BgHover   = Color.FromRgb(0x2A, 0x2D, 0x2E);
    private static readonly Color BgActive  = Color.FromRgb(0x04, 0x39, 0x5E);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);

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

    // ── 字段 ─────────────────────────────────────────────────────
    private readonly StackPanel _listPanel;
    private readonly List<HistorySummary> _summaries = [];
    private HistorySummary? _selected;

    public UIElement RootElement { get; }

    /// <summary>用户点击某条历史记录时触发。</summary>
    public event Action<HistorySummary>? EntrySelected;
    /// <summary>用户点击"清除"按钮时触发，由 MainWindow 调用 HistoryService.Clear()。</summary>
    public event Action? ClearRequested;

    // ── 构造 ─────────────────────────────────────────────────────
    public HistoryPanelView()
    {
        _listPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };

        var menuIcon = new TextBlock { Text = "···", FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center, Foreground = TextSec };
        var menuBtn = new Button { Width = 32, Height = 26, Padding = new Thickness(0), Margin = new Thickness(0, 0, 6, 0) };
        menuBtn.Content(menuIcon as Element).Background(Color.Transparent);
        menuBtn.MouseEnter += () => menuIcon.Foreground = TextPri;
        menuBtn.MouseLeave += () => menuIcon.Foreground = TextSec;
        menuBtn.ToolTip("更多操作");
        menuBtn.Click += () =>
        {
            var menu = new ContextMenu();
            menu.Item("清除", () => ClearRequested?.Invoke());
            ViewHelpers.PopupMenu(menuBtn, menu);
        };

        var headerLabel = new TextBlock
        {
            Text              = "请求历史",
            FontSize          = 11,
            FontWeight        = FontWeight.SemiBold,
            Foreground        = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(12, 0, 0, 0),
        };
        var headerRow = new DockPanel { Height = 33 };
        headerRow.Add(menuBtn.DockRight());
        headerRow.Add(headerLabel);

        var scroll = new ScrollViewer { VerticalScroll = ScrollMode.Auto, Content = _listPanel };

        var root = new DockPanel();
        root.Add(new Border { Height = 33, Background = BgSidebar, Child = headerRow }.DockTop());
        root.Add(new Border { Height = 1, Background = BorderCol }.DockTop());
        root.Add(scroll);

        RootElement = new Border { Background = BgSidebar, Child = root };
    }

    // ── 公开方法 ─────────────────────────────────────────────────
    /// <summary>加载全部摘要（初始化或重载时调用）。</summary>
    public void SetSummaries(IReadOnlyList<HistorySummary> summaries)
    {
        _summaries.Clear();
        _summaries.AddRange(summaries);
        _selected = null;
        Rebuild();
    }

    /// <summary>在列表头部插入新摘要（每次发送请求后调用）。</summary>
    public void PrependSummary(HistorySummary summary)
    {
        _summaries.Insert(0, summary);
        _selected = summary;   // 自动选中最新条目
        Rebuild();
    }

    /// <summary>清空列表显示。</summary>
    public void ClearList()
    {
        _summaries.Clear();
        _selected = null;
        Rebuild();
    }

    // ── 私有 ─────────────────────────────────────────────────────
    private void Rebuild()
    {
        _listPanel.Clear();
        if (_summaries.Count == 0)
        {
            _listPanel.Add(new TextBlock
            {
                Text                = "尚无请求记录",
                FontSize            = 12,
                Foreground          = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 24),
            });
            return;
        }
        foreach (var s in _summaries)
            _listPanel.Add(BuildEntryRow(s));
    }

    private UIElement BuildEntryRow(HistorySummary summary)
    {
        var isSelected = IsSelected(summary);

        var methodBadge = new Border
        {
            Width   = 48,
            Height  = 18,
            Padding = new Thickness(2, 1),
            Child   = new TextBlock
            {
                Text                = summary.Method.ToUpperInvariant(),
                FontSize            = 10,
                FontWeight          = FontWeight.SemiBold,
                Foreground          = MethodColor(summary.Method),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };

        var statusColor = summary.StatusCode switch
        {
            >= 200 and < 300 => Color.FromRgb(0x4E, 0xC9, 0xB0),
            >= 300 and < 400 => Color.FromRgb(0x4F, 0xC1, 0xFF),
            >= 400 and < 500 => Color.FromRgb(0xCE, 0x91, 0x78),
            >= 500           => Color.FromRgb(0xF4, 0x47, 0x47),
            _                => TextSec,
        };
        var statusLabel = new TextBlock
        {
            Text              = summary.Error is not null ? "Err"
                              : summary.StatusCode > 0   ? summary.StatusCode.ToString()
                              : "…",
            FontSize          = 11,
            Foreground        = statusColor,
            VerticalAlignment = VerticalAlignment.Center,
            Width             = 32,
            Margin            = new Thickness(0, 0, 4, 0),
        };
        var timeLabel = new TextBlock
        {
            Text              = FormatRelativeTime(summary.Timestamp),
            FontSize          = 10,
            Foreground        = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
        };
        var urlLabel = new TextBlock
        {
            Text              = TruncateUrl(summary.Url),
            FontSize          = 11,
            Foreground        = TextPri,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis,
        };
        var nameLabel = new TextBlock
        {
            Text              = summary.RequestName,
            FontSize          = 10,
            Foreground        = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis,
        };
        var infoStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 1 };
        infoStack.Add(urlLabel);
        infoStack.Add(nameLabel);

        var row = new DockPanel();
        row.Add(new Border { Width = 6 }.DockLeft());
        row.Add(methodBadge.DockLeft());
        row.Add(new Border { Width = 6 }.DockLeft());
        row.Add(timeLabel.DockRight());
        row.Add(statusLabel.DockRight());
        row.Add(infoStack);

        var rowBtn = new Button { Height = 48, Padding = new Thickness(0) };
        rowBtn.Content(row as Element)
              .Background(isSelected ? BgActive : Color.Transparent)
              .Padding(0, 2);
        rowBtn.MouseEnter += () => { if (!IsSelected(summary)) rowBtn.Background(BgHover); };
        rowBtn.MouseLeave += () => { if (!IsSelected(summary)) rowBtn.Background(Color.Transparent); };
        rowBtn.Click += () =>
        {
            _selected = summary;
            Rebuild();
            EntrySelected?.Invoke(summary);
        };

        var separator = new Border { Height = 1, Background = Color.FromRgb(0x30, 0x30, 0x33) };
        var container = new StackPanel { Orientation = Orientation.Vertical };
        container.Add(rowBtn);
        container.Add(separator);
        return container;
    }

    private bool IsSelected(HistorySummary s) =>
        _selected?.Id == s.Id;

    private static string TruncateUrl(string url, int max = 28)
    {
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = url[8..];
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) url = url[7..];
        return url.Length > max ? url[..max] + "…" : url;
    }

    private static string FormatRelativeTime(DateTime ts)
    {
        var diff = DateTime.Now - ts;
        return diff.TotalSeconds < 60  ? "刚刚"
             : diff.TotalMinutes < 60  ? $"{(int)diff.TotalMinutes}m 前"
             : diff.TotalHours < 24    ? $"{(int)diff.TotalHours}h 前"
             : diff.TotalDays < 7      ? $"{(int)diff.TotalDays}d 前"
             : ts.ToString("MM-dd");
    }
}
