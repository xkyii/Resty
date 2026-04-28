using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Services;

namespace Resty.Gui.Views;

/// <summary>
/// 集合侧边栏视图：显示工作区内的 .http 文件树和请求列表。
/// 使用工厂方法 Build() 创建 UIElement，而非继承控件（MewUI 控件均为 sealed）。
/// </summary>
public sealed class SidebarView
{
    // 方法标签颜色（前景色）
    private static readonly Dictionary<string, Color> MethodColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET"]     = Color.FromRgb(0x61, 0xAF, 0xEF),  // 蓝
        ["POST"]    = Color.FromRgb(0x98, 0xC3, 0x79),  // 绿
        ["PUT"]     = Color.FromRgb(0xE5, 0xC0, 0x7B),  // 橙
        ["PATCH"]   = Color.FromRgb(0xD1, 0x9A, 0x66),  // 橙褐
        ["DELETE"]  = Color.FromRgb(0xE0, 0x6C, 0x75),  // 红
        ["HEAD"]    = Color.FromRgb(0xAB, 0xB2, 0xBF),  // 灰
        ["OPTIONS"] = Color.FromRgb(0xAB, 0xB2, 0xBF),  // 灰
    };

    private static Color MethodColor(string method) =>
        MethodColors.TryGetValue(method, out var c) ? c : Color.FromRgb(0xAB, 0xB2, 0xBF);

    private readonly TextBox _searchBox;
    private readonly StackPanel _treeContainer;
    private readonly DockPanel _root;
    private WorkspaceService? _workspace;

    /// <summary>返回可嵌入父布局的 UIElement。</summary>
    public UIElement RootElement => _root;

    public event Action<HttpFileNode, RequestNode>? RequestSelected;

    public SidebarView()
    {
        // ── 顶部工具栏 ─────────────────────────────────────────────
        var toolbar = new DockPanel
        {
            Height   = 36,
            Padding  = new Thickness(8, 0),
        };

        _searchBox = new TextBox
        {
            Placeholder     = "搜索请求…",
            FontSize        = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _searchBox.TextChanged += _ => Refresh();

        toolbar.Add(_searchBox);

        // ── 树形区域 ──────────────────────────────────────────────
        _treeContainer = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };

        var scroll = new ScrollViewer
        {
            Content = _treeContainer,
        };

        _root = new DockPanel();
        _root.Add(toolbar.DockTop());
        _root.Add(scroll);
    }

    /// <summary>加载工作区数据并刷新树。</summary>
    public void SetWorkspace(WorkspaceService workspace)
    {
        _workspace = workspace;
        Refresh();
    }

    // ── 内部刷新 ──────────────────────────────────────────────────
    private void Refresh()
    {
        _treeContainer.Clear();
        if (_workspace is null) return;

        var filter = _searchBox.Text?.Trim() ?? string.Empty;

        foreach (var file in _workspace.Files)
        {
            var matchingRequests = string.IsNullOrEmpty(filter)
                ? file.Requests
                : file.Requests.Where(r => r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!matchingRequests.Any() && !string.IsNullOrEmpty(filter)) continue;

            _treeContainer.Add(BuildFileNode(file, matchingRequests));
        }

        if (_treeContainer.Children.Count == 0 && !string.IsNullOrEmpty(filter))
        {
            _treeContainer.Add(new TextBlock
            {
                Text = "无匹配请求",
                FontSize = 12,
                Foreground = Color.FromRgb(0x85, 0x85, 0x85),
                Margin = new Thickness(16, 8),
            });
        }
    }

    private Element BuildFileNode(HttpFileNode file, IEnumerable<RequestNode> requests)
    {
        var expanded = new ObservableValue<bool>(true);

        // ── 文件行 ───────────────────────────────────────────────
        var chevron = new TextBlock
        {
            FontSize  = 10,
            Foreground = Color.FromRgb(0x85, 0x85, 0x85),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0),
        };
        chevron.BindText(expanded, v => v ? "▾" : "▸");

        var fileLabel = new TextBlock
        {
            Text      = file.FileName,
            FontSize  = 13,
            Foreground = Color.FromRgb(0xCC, 0xCC, 0xCC),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };

        var fileRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height   = 28,
            Spacing  = 0,
            Padding  = new Thickness(8, 0),
            Cursor   = CursorType.Hand,
        };
        fileRow.Add(chevron);
        fileRow.Add(fileLabel);
        fileRow.MouseDown += _ => expanded.Value = !expanded.Value;

        // ── 子请求列表 ───────────────────────────────────────────
        var childPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };
        childPanel.BindIsVisible(expanded);

        foreach (var req in requests)
        {
            childPanel.Add(BuildRequestNode(file, req));
        }

        // ── 组合 ─────────────────────────────────────────────────
        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
        };
        container.Add(fileRow);
        container.Add(childPanel);
        return container;
    }

    private Element BuildRequestNode(HttpFileNode file, RequestNode req)
    {
        // 方法标签
        var badge = new Border
        {
            Padding       = new Thickness(4, 1),
            CornerRadius  = 3,
            Background    = MethodColor(req.Method).WithAlpha((byte)46),
            Child = new TextBlock
            {
                Text       = req.Method,
                FontSize   = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = MethodColor(req.Method),
            },
        };

        var nameLabel = new TextBlock
        {
            Text      = req.Name,
            FontSize  = 12,
            Foreground = Color.FromRgb(0xCC, 0xCC, 0xCC),
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(6, 0, 0, 0),
            TextTrimming  = TextTrimming.CharacterEllipsis,
        };

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height  = 26,
            Spacing = 0,
            Padding = new Thickness(24, 0, 8, 0),
            Cursor  = CursorType.Hand,
        };
        row.Add(badge);
        row.Add(nameLabel);

        row.MouseDown += _ => RequestSelected?.Invoke(file, req);
        return row;
    }
}
