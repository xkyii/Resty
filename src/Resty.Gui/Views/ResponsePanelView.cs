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

        // ── 成功状态面板 ──────────────────────────────────────────
        var successRoot = new DockPanel();
        successRoot.Add(headerBorder.DockTop());
        successRoot.Add(_bodyText);
        _successPanel = new Border { Background = BgBase, Child = successRoot };

        // ── 根容器（初始显示空状态） ──────────────────────────────
        _root = new Border { Background = BgBase, Child = _emptyState };
    }

    /// <summary>显示空状态（未发送）。</summary>
    public void ShowEmpty() => _root.Child = _emptyState;

    /// <summary>显示加载中状态。</summary>
    public void ShowLoading() => _root.Child = _loadingState;

    /// <summary>显示成功响应。</summary>
    public void ShowResult(HttpExecutionResult result)
    {
        // 更新状态码
        var statusText = StatusText(result.StatusCode);
        _statusBadge.Text       = statusText;
        _statusBadge.Foreground = StatusColor(result.StatusCode);

        // 更新耗时 & 大小
        _elapsedLabel.Text = $"{result.ElapsedMs} ms";
        _sizeLabel.Text    = FormatSize(System.Text.Encoding.UTF8.GetByteCount(result.Body));

        // 更新响应体
        _bodyText.Text = string.IsNullOrEmpty(result.Body)
            ? "(空响应体)"
            : result.Body;

        _root.Child = _successPanel;
    }

    /// <summary>显示网络/传输错误。</summary>
    public void ShowError(string error)
    {
        var label = BuildCenteredLabel($"✗ 请求失败\n{error}", Color.FromRgb(0xF4, 0x47, 0x47), 13);
        _root.Child = label;
    }

    // ── 私有辅助 ─────────────────────────────────────────────────

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
