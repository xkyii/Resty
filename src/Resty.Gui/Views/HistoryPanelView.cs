using System.Text.Json;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace Resty.Gui.Views;

/// <summary>
/// P11 请求历史面板：显示最近发送的 HTTP 请求记录。
/// </summary>
public sealed class HistoryPanelView
{
    // ── 颜色 ─────────────────────────────────────────────────────
    private static readonly Color BgSidebar = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color BgHover   = Color.FromRgb(0x2A, 0x2D, 0x2E);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);
    private static readonly Color Accent    = Color.FromRgb(0x00, 0x7A, 0xCC);

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
    private readonly List<HistoryEntry> _entries = [];
    private string _historyFile = string.Empty;
    private const int MaxEntries = 200;

    public UIElement RootElement { get; }

    // ── 构造 ─────────────────────────────────────────────────────
    public HistoryPanelView()
    {
        _listPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };

        var clearBtn = new Button { Height = 26, Padding = new Thickness(10, 0) };
        clearBtn.Content("清除", false).FontSize(11)
            .Background(Color.Transparent).Foreground(TextSec);
        clearBtn.MouseEnter += () => clearBtn.Background(BgHover).Foreground(TextPri);
        clearBtn.MouseLeave += () => clearBtn.Background(Color.Transparent).Foreground(TextSec);
        clearBtn.OnClick(ClearAll);

        var headerLabel = new TextBlock
        {
            Text              = "请求历史",
            FontSize          = 11,
            FontWeight        = FontWeight.SemiBold,
            Foreground        = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(12, 0, 0, 0),
        };
        var headerRow = new DockPanel { Height = 30 };
        headerRow.Add(clearBtn.DockRight());
        headerRow.Add(headerLabel);

        var scroll = new ScrollViewer { VerticalScroll = ScrollMode.Auto, Content = _listPanel };

        var root = new DockPanel();
        root.Add(new Border { Height = 30, Background = BgSidebar, Child = headerRow }.DockTop());
        root.Add(new Border { Height = 1, Background = BorderCol }.DockTop());
        root.Add(scroll);

        RootElement = new Border { Background = BgSidebar, Child = root };
    }

    // ── 公开方法 ─────────────────────────────────────────────────
    public void SetWorkspacePath(string workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath)) return;
        var dir = Path.Combine(workspacePath, ".resty");
        Directory.CreateDirectory(dir);
        _historyFile = Path.Combine(dir, "history.json");
        _entries.Clear();
        LoadFromFile();
        Rebuild();
    }

    public void AddEntry(HistoryEntry entry)
    {
        _entries.Insert(0, entry);
        if (_entries.Count > MaxEntries)
            _entries.RemoveAt(_entries.Count - 1);
        SaveToFile();
        Rebuild();
    }

    // ── 私有 ─────────────────────────────────────────────────────
    private void ClearAll()
    {
        _entries.Clear();
        SaveToFile();
        Rebuild();
    }

    private void Rebuild()
    {
        _listPanel.Clear();
        if (_entries.Count == 0)
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
        foreach (var e in _entries)
            _listPanel.Add(BuildEntryRow(e));
    }

    private UIElement BuildEntryRow(HistoryEntry entry)
    {
        var methodBadge = new Border
        {
            Width   = 48,
            Height  = 18,
            Padding = new Thickness(2, 1),
            Child   = new TextBlock
            {
                Text                = entry.Method.ToUpperInvariant(),
                FontSize            = 10,
                FontWeight          = FontWeight.SemiBold,
                Foreground          = MethodColor(entry.Method),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };

        var statusColor = entry.StatusCode switch
        {
            >= 200 and < 300 => Color.FromRgb(0x4E, 0xC9, 0xB0),
            >= 300 and < 400 => Color.FromRgb(0x4F, 0xC1, 0xFF),
            >= 400 and < 500 => Color.FromRgb(0xCE, 0x91, 0x78),
            >= 500           => Color.FromRgb(0xF4, 0x47, 0x47),
            _                => TextSec,
        };
        var statusLabel = new TextBlock
        {
            Text              = entry.StatusCode > 0 ? entry.StatusCode.ToString() : "Err",
            FontSize          = 11,
            Foreground        = statusColor,
            VerticalAlignment = VerticalAlignment.Center,
            Width             = 32,
            Margin            = new Thickness(0, 0, 4, 0),
        };

        var timeStr = FormatRelativeTime(entry.Timestamp);
        var timeLabel = new TextBlock
        {
            Text              = timeStr,
            FontSize          = 10,
            Foreground        = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
        };

        var urlLabel = new TextBlock
        {
            Text              = TruncateUrl(entry.Url, 28),
            FontSize          = 11,
            Foreground        = TextPri,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis,
        };
        var nameLabel = new TextBlock
        {
            Text              = entry.RequestName,
            FontSize          = 10,
            Foreground        = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis,
        };
        var infoStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 1 };
        infoStack.Add(urlLabel);
        infoStack.Add(nameLabel);

        var row = new DockPanel { Margin = new Thickness(0) };
        row.Add(new Border { Width = 6 }.DockLeft()); // indent
        row.Add(methodBadge.DockLeft());
        row.Add(new Border { Width = 6 }.DockLeft());
        row.Add(timeLabel.DockRight());
        row.Add(statusLabel.DockRight());
        row.Add(infoStack);

        var rowBorder = new Border { Height = 48, Padding = new Thickness(0, 2), Child = row };
        rowBorder.MouseEnter += () => rowBorder.Background = BgHover;
        rowBorder.MouseLeave += () => rowBorder.Background = Color.Transparent;

        var separator = new Border { Height = 1, Background = Color.FromRgb(0x30, 0x30, 0x33) };
        var container = new StackPanel { Orientation = Orientation.Vertical };
        container.Add(rowBorder);
        container.Add(separator);
        return container;
    }

    private static string TruncateUrl(string url, int max)
    {
        // 去掉协议前缀以节省空间
        var s = url;
        if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) s = s[8..];
        else if (s.StartsWith("http://",  StringComparison.OrdinalIgnoreCase)) s = s[7..];
        return s.Length > max ? s[..max] + "…" : s;
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

    private void LoadFromFile()
    {
        if (string.IsNullOrEmpty(_historyFile) || !File.Exists(_historyFile)) return;
        try
        {
            var json = File.ReadAllText(_historyFile);
            var list = JsonSerializer.Deserialize<List<HistoryEntry>>(json);
            if (list is not null) _entries.AddRange(list);
        }
        catch { }
    }

    private void SaveToFile()
    {
        if (string.IsNullOrEmpty(_historyFile)) return;
        try
        {
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyFile, json);
        }
        catch { }
    }
}

/// <summary>单条历史记录。</summary>
public sealed record HistoryEntry(
    string RequestName,
    string Method,
    string Url,
    int StatusCode,
    long ElapsedMs,
    DateTime Timestamp);
