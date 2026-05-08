using System.Diagnostics;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Services;

namespace Resty.Gui.Views;

/// <summary>
/// 工作区管理面板：左侧为搜索/最近/收藏，右侧为工作区详情与操作。
/// </summary>
public sealed class WorkspacePanelView
{
    private static readonly Color TextPri = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color Accent = Color.FromRgb(0x4F, 0xC1, 0xFF);
    private static readonly Color BgPanel = Color.FromRgb(0x25, 0x25, 0x26);
    private static readonly Color BgHover = Color.FromRgb(0x2A, 0x2D, 0x2E);
    private static readonly Color BgActive = Color.FromRgb(0x04, 0x39, 0x5E);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);

    public UIElement RootElement => _root;
    public UIElement DetailElement => _detailRoot;

    /// <summary>左侧选中某个工作区时触发，参数为目录路径。</summary>
    public event Action<string>? WorkspaceSelected;

    /// <summary>请求打开某个工作区时触发，参数为目录路径。</summary>
    public event Action<string>? WorkspaceOpenRequested;

    private string _currentPath = string.Empty;
    private string _query = string.Empty;
    private string? _selectedPath;

    private readonly TextBox _searchBox;
    private readonly StackPanel _recentContainer;
    private readonly StackPanel _favoriteContainer;
    private readonly DockPanel _root;
    private readonly Border _detailRoot;
    private readonly Border _detailBody;

    private sealed record WorkspaceEntry(string Path, string Name, DateTime? LastAccessedAt, bool IsFavorite);

    public WorkspacePanelView()
    {
        var header = new TextBlock
        {
            Text = "工作区管理",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextSec,
            Margin = new Thickness(12, 10, 0, 6),
        };

        _searchBox = new TextBox
        {
            Placeholder = "搜索名称或路径…",
            FontSize = 12,
            Margin = new Thickness(8, 0, 8, 8),
        };
        _searchBox.TextChanged += _ =>
        {
            _query = _searchBox.Text?.Trim() ?? string.Empty;
            Refresh();
        };

        _recentContainer = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        _favoriteContainer = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };

        var listPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };
        listPanel.Add(new TextBlock
        {
            Text = "最近工作区",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextSec,
            Margin = new Thickness(12, 4, 0, 4),
        });
        listPanel.Add(_recentContainer);

        listPanel.Add(new TextBlock
        {
            Text = "收藏工作区",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextSec,
            Margin = new Thickness(12, 10, 0, 4),
        });
        listPanel.Add(_favoriteContainer);

        var scroll = new ScrollViewer { Content = listPanel };

        _root = new DockPanel();
        _root.Add(new Border { Height = 1, Background = BorderCol }.DockTop());
        _root.Add(header.DockTop());
        _root.Add(_searchBox.DockTop());
        _root.Add(scroll);

        _detailBody = new Border
        {
            Child = BuildEmptyDetail("从左侧选择一个工作区以查看详情"),
            Padding = new Thickness(16),
        };
        _detailRoot = new Border
        {
            Background = BgPanel,
            Child = _detailBody,
        };

        Refresh();
    }

    /// <summary>设置当前打开的工作区路径（用于高亮显示）。</summary>
    public void SetCurrentPath(string path)
    {
        _currentPath = path;
        if (!string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(_selectedPath))
            _selectedPath = path;
        Refresh();
        UpdateDetail();
    }

    public void Refresh()
    {
        _recentContainer.Clear();
        _favoriteContainer.Clear();

        var recents = RecentWorkspacesService.LoadEntries();
        var favorites = FavoriteWorkspacesService.Load();
        var favoriteSet = new HashSet<string>(favorites, StringComparer.OrdinalIgnoreCase);

        var recentLimit = SettingsService.Current.RecentWorkspaceDisplayCount;
        var filteredRecents = recents
            .Where(x => MatchQuery(Path.GetFileName(x.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), x.Path))
            .Take(recentLimit)
            .Select(x => new WorkspaceEntry(
                x.Path,
                Path.GetFileName(x.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? x.Path,
                x.LastAccessedAt,
                favoriteSet.Contains(x.Path)))
            .ToList();

        var filteredFavorites = favorites
            .Where(path => MatchQuery(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), path))
            .Select(path => new WorkspaceEntry(
                path,
                Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? path,
                recents.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase))?.LastAccessedAt,
                true))
            .ToList();

        if (filteredRecents.Count == 0)
        {
            _recentContainer.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_query) ? "暂无最近工作区" : "无匹配项",
                FontSize = 12,
                Foreground = TextSec,
                Margin = new Thickness(16, 12),
            });
        }
        else
        {
            foreach (var item in filteredRecents)
                _recentContainer.Add(BuildWorkspaceRow(item));
        }

        if (filteredFavorites.Count == 0)
        {
            _favoriteContainer.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_query) ? "暂无收藏工作区" : "无匹配项",
                FontSize = 12,
                Foreground = TextSec,
                Margin = new Thickness(16, 12),
            });
        }
        else
        {
            foreach (var item in filteredFavorites)
                _favoriteContainer.Add(BuildWorkspaceRow(item));
        }

        if (!string.IsNullOrWhiteSpace(_selectedPath) &&
            !filteredRecents.Any(x => string.Equals(x.Path, _selectedPath, StringComparison.OrdinalIgnoreCase)) &&
            !filteredFavorites.Any(x => string.Equals(x.Path, _selectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedPath = null;
        }

        UpdateDetail();
    }

    private UIElement BuildWorkspaceRow(WorkspaceEntry item)
    {
        var isCurrent = string.Equals(item.Path, _currentPath, StringComparison.OrdinalIgnoreCase);
        var isSelected = string.Equals(item.Path, _selectedPath, StringComparison.OrdinalIgnoreCase);

        var nameLabel = new TextBlock
        {
            Text = item.IsFavorite ? $"★ {item.Name}" : item.Name,
            FontSize = 13,
            Foreground = isCurrent ? Accent : TextPri,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var pathLabel = new TextBlock
        {
            Text = item.Path,
            FontSize = 10,
            Foreground = TextSec,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var sp = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            Margin = new Thickness(12, 6, 8, 6),
        };
        sp.Add(nameLabel);
        sp.Add(pathLabel);

        var container = new Border
        {
            Background = isSelected ? BgActive : Color.Transparent,
            Child = sp,
        };

        var btn = new Button { Height = 50, Padding = new Thickness(0) };
        btn.Content(container as Element).Background(Color.Transparent);

        btn.MouseEnter += () =>
        {
            if (!string.Equals(item.Path, _selectedPath, StringComparison.OrdinalIgnoreCase))
                container.Background = BgHover;
        };
        btn.MouseLeave += () =>
        {
            if (!string.Equals(item.Path, _selectedPath, StringComparison.OrdinalIgnoreCase))
                container.Background = Color.Transparent;
        };

        var capturedPath = item.Path;
        btn.Click += () =>
        {
            _selectedPath = capturedPath;
            WorkspaceSelected?.Invoke(capturedPath);
            Refresh();
        };

        btn.ContextMenu(new ContextMenu()
            .Item("打开工作区", () => WorkspaceOpenRequested?.Invoke(capturedPath))
            .Item(item.IsFavorite ? "取消收藏" : "加入收藏", () =>
            {
                if (item.IsFavorite) FavoriteWorkspacesService.Remove(capturedPath);
                else FavoriteWorkspacesService.Add(capturedPath);
                Refresh();
            })
            .Item("从最近移除", () =>
            {
                RecentWorkspacesService.Remove(capturedPath);
                if (string.Equals(_selectedPath, capturedPath, StringComparison.OrdinalIgnoreCase))
                    _selectedPath = null;
                Refresh();
            }));

        return btn;
    }

    private void UpdateDetail()
    {
        if (string.IsNullOrWhiteSpace(_selectedPath))
        {
            _detailBody.Child = BuildEmptyDetail("从左侧选择一个工作区以查看详情");
            return;
        }

        var path = _selectedPath!;
        var favorites = FavoriteWorkspacesService.Load();
        var isFavorite = favorites.Any(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
        var metrics = WorkspaceMetricsService.Collect(path);
        var recents = RecentWorkspacesService.LoadEntries();
        var recentEntry = recents.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
        var isCurrent = string.Equals(path, _currentPath, StringComparison.OrdinalIgnoreCase);

        var detailPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
        };

        detailPanel.Add(new TextBlock
        {
            Text = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? path,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = TextPri,
        });
        detailPanel.Add(new TextBlock
        {
            Text = isCurrent ? "当前已打开工作区" : "未打开",
            FontSize = 11,
            Foreground = isCurrent ? Accent : TextSec,
        });
        detailPanel.Add(MakeField("目录路径", path));
        detailPanel.Add(MakeField("HTTP 文件数", metrics.Exists ? metrics.HttpFileCount.ToString() : "-"));
        detailPanel.Add(MakeField("请求数", metrics.Exists ? metrics.RequestCount.ToString() : "-"));
        detailPanel.Add(MakeField("收藏状态", isFavorite ? "已收藏" : "未收藏"));
        detailPanel.Add(MakeField("最近访问", recentEntry is null ? "-" : recentEntry.LastAccessedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));

        if (!metrics.Exists)
        {
            detailPanel.Add(new TextBlock
            {
                Text = "目录不存在，请检查路径。",
                FontSize = 12,
                Foreground = Color.FromRgb(0xE0, 0x6C, 0x75),
                Margin = new Thickness(0, 6, 0, 0),
            });
        }
        else if (!string.IsNullOrWhiteSpace(metrics.Error))
        {
            detailPanel.Add(new TextBlock
            {
                Text = $"统计失败：{metrics.Error}",
                FontSize = 12,
                Foreground = Color.FromRgb(0xE5, 0xC0, 0x7B),
                Margin = new Thickness(0, 6, 0, 0),
            });
        }

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var openBtn = new Button { Height = 30, Padding = new Thickness(14, 0) };
        openBtn.Content(isCurrent ? "重新加载" : "打开工作区", false)
            .Background(Accent)
            .Foreground(Color.White)
            .FontSize(12);
        openBtn.OnClick(() => WorkspaceOpenRequested?.Invoke(path));

        var favoriteBtn = new Button { Height = 30, Padding = new Thickness(14, 0) };
        favoriteBtn.Content(isFavorite ? "取消收藏" : "收藏", false)
            .Background(Color.Transparent)
            .Foreground(TextPri)
            .FontSize(12);
        favoriteBtn.MouseEnter += () => favoriteBtn.Background(BgHover);
        favoriteBtn.MouseLeave += () => favoriteBtn.Background(Color.Transparent);
        favoriteBtn.OnClick(() =>
        {
            if (isFavorite) FavoriteWorkspacesService.Remove(path);
            else FavoriteWorkspacesService.Add(path);
            Refresh();
        });

        var removeRecentBtn = new Button { Height = 30, Padding = new Thickness(14, 0) };
        removeRecentBtn.Content("从最近移除", false)
            .Background(Color.Transparent)
            .Foreground(TextSec)
            .FontSize(12);
        removeRecentBtn.MouseEnter += () => removeRecentBtn.Background(BgHover);
        removeRecentBtn.MouseLeave += () => removeRecentBtn.Background(Color.Transparent);
        removeRecentBtn.OnClick(() =>
        {
            RecentWorkspacesService.Remove(path);
            if (string.Equals(_selectedPath, path, StringComparison.OrdinalIgnoreCase))
                _selectedPath = null;
            Refresh();
        });

        var openFolderBtn = new Button { Height = 30, Padding = new Thickness(14, 0) };
        openFolderBtn.Content("打开目录", false)
            .Background(Color.Transparent)
            .Foreground(TextSec)
            .FontSize(12);
        openFolderBtn.MouseEnter += () => openFolderBtn.Background(BgHover);
        openFolderBtn.MouseLeave += () => openFolderBtn.Background(Color.Transparent);
        openFolderBtn.OnClick(() => OpenFolder(path));

        btnRow.Add(openBtn);
        btnRow.Add(favoriteBtn);
        btnRow.Add(removeRecentBtn);
        btnRow.Add(openFolderBtn);

        detailPanel.Add(btnRow);
        _detailBody.Child = new ScrollViewer { Content = detailPanel };
    }

    private static UIElement MakeField(string label, string value)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2,
            Margin = new Thickness(0, 2, 0, 0),
        };
        row.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = TextSec,
        });
        row.Add(new TextBlock
        {
            Text = value,
            FontSize = 13,
            Foreground = TextPri,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        return row;
    }

    private static UIElement BuildEmptyDetail(string text)
        => new Border
        {
            Child = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = TextSec,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

    private bool MatchQuery(string? name, string path)
    {
        if (string.IsNullOrWhiteSpace(_query)) return true;
        var q = _query;
        return (!string.IsNullOrEmpty(name) && name.Contains(q, StringComparison.OrdinalIgnoreCase))
            || path.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
