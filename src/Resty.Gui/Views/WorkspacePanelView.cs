using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Services;

namespace Resty.Gui.Views;

/// <summary>
/// 工作区管理面板（P9）— 显示最近工作区列表，点击切换到对应工作区。
/// </summary>
public sealed class WorkspacePanelView
{
    private static readonly Color TextPri = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color Accent  = Color.FromRgb(0x4F, 0xC1, 0xFF);
    private static readonly Color BgHover = Color.FromRgb(0x2A, 0x2D, 0x2E);

    public UIElement RootElement => _root;

    /// <summary>用户点击某个最近工作区时触发，参数为目录路径。</summary>
    public event Action<string>? WorkspaceSelected;

    private string _currentPath = string.Empty;
    private readonly StackPanel _listContainer;
    private readonly DockPanel _root;

    public WorkspacePanelView()
    {
        var header = new TextBlock
        {
            Text       = "最近工作区",
            FontSize   = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextSec,
            Margin     = new Thickness(12, 10, 0, 6),
        };

        _listContainer = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };

        var scroll = new ScrollViewer { Content = _listContainer };

        _root = new DockPanel();
        _root.Add(header.DockTop());
        _root.Add(scroll);

        Refresh();
    }

    /// <summary>设置当前打开的工作区路径（用于高亮显示）。</summary>
    public void SetCurrentPath(string path)
    {
        _currentPath = path;
        Refresh();
    }

    public void Refresh()
    {
        _listContainer.Clear();
        var recents = RecentWorkspacesService.Load();

        if (recents.Count == 0)
        {
            _listContainer.Add(new TextBlock
            {
                Text       = "暂无最近工作区",
                FontSize   = 12,
                Foreground = TextSec,
                Margin     = new Thickness(16, 12),
            });
            return;
        }

        foreach (var path in recents)
        {
            var isCurrent = string.Equals(path, _currentPath, StringComparison.OrdinalIgnoreCase);
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar,
                                                       Path.AltDirectorySeparatorChar)) ?? path;

            var nameLabel = new TextBlock
            {
                Text         = name,
                FontSize     = 13,
                Foreground   = isCurrent ? Accent : TextPri,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var pathLabel = new TextBlock
            {
                Text         = path,
                FontSize     = 10,
                Foreground   = TextSec,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin       = new Thickness(0, 2, 0, 0),
            };

            var sp = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing     = 0,
                Margin      = new Thickness(12, 6, 8, 6),
            };
            sp.Add(nameLabel);
            sp.Add(pathLabel);

            var btn = new Button { Height = 50, Padding = new Thickness(0) };
            btn.Content(sp as Element).Background(Color.Transparent);

            var capturedPath = path;
            btn.Click      += () => WorkspaceSelected?.Invoke(capturedPath);
            btn.MouseEnter += () => btn.Background(BgHover);
            btn.MouseLeave += () => btn.Background(Color.Transparent);

            _listContainer.Add(btn);
        }
    }
}
