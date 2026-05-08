using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Infrastructure;
using Resty.Gui.Services;

namespace Resty.Gui.Views;

/// <summary>
/// P12 设置窗口：独立 NativeCustomWindow，包含超时、代理、JSON 自动格式化。
/// 固定尺寸，仅有关闭按钮，表单采用左标签-右控件的两列布局。
/// </summary>
public sealed class SettingsWindow : NativeCustomWindow
{
    // ── 颜色 ─────────────────────────────────────────────────────
    private static readonly Color BgPanel   = Color.FromRgb(0x25, 0x25, 0x26);
    private static readonly Color BgInput   = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color BgHover   = Color.FromRgb(0x2A, 0x2D, 0x2E);
    private static readonly Color Accent    = Color.FromRgb(0x00, 0x7A, 0xCC);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);

    // ── 控件引用 ─────────────────────────────────────────────────
    private readonly TextBox      _timeoutBox;
    private readonly TextBox      _proxyBox;
    private readonly TextBox      _recentDisplayCountBox;
    private readonly ToggleSwitch _jsonAutoFormatSwitch;
    private readonly Button       _saveBtn;
    private readonly Button       _resetBtn;
    private readonly Button       _closeBtn;

    // ── 事件 ─────────────────────────────────────────────────────
    public event Action<AppSettings>? SettingsChanged;

    public SettingsWindow()
    {
        // 窗口基础配置：固定尺寸，相对于屏幕居中
        this.Resizable(460, 280, minWidth: 440, minHeight: 260)
            .StartCenterScreen()
            .Title("设置");

        // 隐藏最小化/最大化按钮（NativeCustomWindow 内部通过 CanMinimize/CanMaximize 控制）
        this.SetValue(Window.CanMinimizeProperty, false);
        this.SetValue(Window.CanMaximizeProperty, false);

        // 初始化控件
        var s = SettingsService.Current;

        _timeoutBox = new TextBox
        {
            Text        = s.TimeoutSeconds.ToString(),
            FontSize    = 12,
            Background  = BgInput,
            Foreground  = TextPri,
            Height      = 28,
            Margin      = new Thickness(0),
            MaxLength   = 4,
        };

        _proxyBox = new TextBox
        {
            Text        = s.ProxyUrl,
            FontSize    = 12,
            Placeholder = "http://127.0.0.1:8080",
            Background  = BgInput,
            Foreground  = TextPri,
            Height      = 28,
            Margin      = new Thickness(0),
        };

        _jsonAutoFormatSwitch = new ToggleSwitch
        {
            IsChecked   = s.JsonAutoFormat,
            Height      = 24,
            Width       = 44,
            Margin      = new Thickness(0),
        };

        _recentDisplayCountBox = new TextBox
        {
            Text        = s.RecentWorkspaceDisplayCount.ToString(),
            FontSize    = 12,
            Background  = BgInput,
            Foreground  = TextPri,
            Height      = 28,
            Margin      = new Thickness(0),
            MaxLength   = 2,
        };

        // ── 按钮（统一样式：高度 28）──────────
        _saveBtn = new Button { Height = 28, Padding = new Thickness(20, 0) };
        _saveBtn.Content("保存", false).FontSize(12).Background(Accent).Foreground(Color.White);
        _saveBtn.MouseEnter += () => _saveBtn.Background(Color.FromRgb(0x0E, 0x90, 0xE0));
        _saveBtn.MouseLeave += () => _saveBtn.Background(Accent);
        _saveBtn.OnClick(() =>
        {
            SaveSettings();
            Close();
        });

        _resetBtn = new Button { Height = 28, Padding = new Thickness(20, 0) };
        _resetBtn.Content("重置为默认", false).FontSize(12).Background(Color.Transparent).Foreground(TextSec);
        _resetBtn.MouseEnter += () => _resetBtn.Background(BgHover);
        _resetBtn.MouseLeave += () => _resetBtn.Background(Color.Transparent);
        _resetBtn.OnClick(ResetDefaults);

        _closeBtn = new Button { Height = 28, Padding = new Thickness(20, 0) };
        _closeBtn.Content("关闭", false).FontSize(12).Background(Color.Transparent).Foreground(TextSec);
        _closeBtn.MouseEnter += () => _closeBtn.Background(BgHover);
        _closeBtn.MouseLeave += () => _closeBtn.Background(Color.Transparent);
        _closeBtn.OnClick(Close);

        // 设置 Content
        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        // ── 表单行（左标签 + 右控件）──────────────────────────────
        var timeoutRow = new DockPanel { Height = 36 };
        timeoutRow.Add(MakeLabel("请求超时（秒）").DockLeft());
        timeoutRow.Add(_timeoutBox);

        var proxyRow = new DockPanel { Height = 36 };
        proxyRow.Add(MakeLabel("HTTP 代理").DockLeft());
        proxyRow.Add(_proxyBox);

        var jsonRow = new DockPanel { Height = 36 };
        jsonRow.Add(MakeLabel("JSON 自动格式化").DockLeft());
        jsonRow.Add(_jsonAutoFormatSwitch);

        var recentCountRow = new DockPanel { Height = 36 };
        recentCountRow.Add(MakeLabel("最近显示数量").DockLeft());
        recentCountRow.Add(_recentDisplayCountBox);

        // ── 按钮行（靠右对齐）────────────────────────────────────
        var btnRow = new DockPanel { Height = 40, Margin = new Thickness(0, 8, 0, 0) };
        btnRow.Add(_closeBtn.DockRight());
        btnRow.Add(_resetBtn.DockRight());
        btnRow.Add(_saveBtn.DockRight()); // 三个按钮靠右

        // ── 主表单 ───────────────────────────────────────────────
        var form = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing     = 0,
            Margin      = new Thickness(24, 20, 24, 20),
        };
        form.Add(timeoutRow);
        form.Add(proxyRow);
        form.Add(jsonRow);
        form.Add(recentCountRow);
        form.Add(btnRow);

        // ── 容器 ───────────────────────────────────────────────
        var content = new Border
        {
            Background     = BgPanel,
            BorderBrush    = BorderCol,
            BorderThickness = 1,
            CornerRadius   = 8,
            Child          = form,
        };

        return new Border
        {
            Background = BgPanel,
            Child      = content,
            Padding    = new Thickness(8),
        };
    }

    // ── 私有辅助方法 ─────────────────────────────────────────────

    private static TextBlock MakeLabel(string text) => new()
    {
        Text       = text,
        FontSize   = 12,
        Foreground = TextSec,
        VerticalAlignment = VerticalAlignment.Center,
        MinWidth   = 130,
    };

    private void UpdateSwitch()
    {
        // ToggleSwitch 会自动反映 _jsonAutoFormatSwitch.IsChecked 状态
    }

    private void SaveSettings()
    {
        var timeout = 30;
        int.TryParse(_timeoutBox.Text?.Trim(), out timeout);
        if (timeout <= 0 || timeout > 300) timeout = 30;

        var recentCount = 5;
        int.TryParse(_recentDisplayCountBox.Text?.Trim(), out recentCount);
        if (recentCount < 3 || recentCount > 20) recentCount = 5;

        var settings = new AppSettings
        {
            TimeoutSeconds = timeout,
            ProxyUrl       = _proxyBox.Text?.Trim() ?? string.Empty,
            JsonAutoFormat = _jsonAutoFormatSwitch.IsChecked,
            RecentWorkspaceDisplayCount = recentCount,
        };
        SettingsService.Save(settings);
        SettingsChanged?.Invoke(settings);
    }

    private void ResetDefaults()
    {
        var defaults = new AppSettings();
        _timeoutBox.Text       = defaults.TimeoutSeconds.ToString();
        _proxyBox.Text         = defaults.ProxyUrl;
        _jsonAutoFormatSwitch.IsChecked = defaults.JsonAutoFormat;
        _recentDisplayCountBox.Text = defaults.RecentWorkspaceDisplayCount.ToString();
    }
}
