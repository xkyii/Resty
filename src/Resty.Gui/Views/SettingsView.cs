using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Resty.Gui.Services;

namespace Resty.Gui.Views;

/// <summary>
/// P12 设置面板：超时、代理、JSON 自动格式化。
/// 显示在侧边栏区域（Activity Bar ⚙ 切换）。
/// </summary>
public sealed class SettingsView
{
    // ── 颜色 ─────────────────────────────────────────────────────
    private static readonly Color BgSidebar = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color BgInput   = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color BgHover   = Color.FromRgb(0x2A, 0x2D, 0x2E);
    private static readonly Color Accent    = Color.FromRgb(0x00, 0x7A, 0xCC);
    private static readonly Color TextPri   = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private static readonly Color TextSec   = Color.FromRgb(0x85, 0x85, 0x85);
    private static readonly Color BorderCol = Color.FromRgb(0x3E, 0x3E, 0x42);
    private static readonly Color GreenOn   = Color.FromRgb(0x4E, 0xC9, 0xB0);

    // ── 控件 ─────────────────────────────────────────────────────
    private readonly TextBox _timeoutBox;
    private readonly TextBox _proxyBox;
    private readonly Button  _jsonFormatToggle;
    private bool _jsonAutoFormat;

    public UIElement RootElement { get; }

    // ── 外部通知：设置更改后回调 ──────────────────────────────────
    public event Action<AppSettings>? SettingsChanged;

    public SettingsView()
    {
        var s = SettingsService.Current;
        _jsonAutoFormat = s.JsonAutoFormat;

        // ── 超时输入 ────────────────────────────────────────────
        _timeoutBox = new TextBox
        {
            Text        = s.TimeoutSeconds.ToString(),
            FontSize    = 12,
            Background  = BgInput,
            Foreground  = TextPri,
            Margin      = new Thickness(0, 4, 0, 0),
        };

        // ── 代理输入 ────────────────────────────────────────────
        _proxyBox = new TextBox
        {
            Text        = s.ProxyUrl,
            FontSize    = 12,
            Placeholder = "http://127.0.0.1:8080",
            Background  = BgInput,
            Foreground  = TextPri,
            Margin      = new Thickness(0, 4, 0, 0),
        };

        // ── JSON 自动格式化开关 ──────────────────────────────────
        _jsonFormatToggle = new Button { Height = 24, Width = 80, Padding = new Thickness(0) };
        UpdateToggleStyle();
        _jsonFormatToggle.OnClick(() =>
        {
            _jsonAutoFormat = !_jsonAutoFormat;
            UpdateToggleStyle();
        });
        _jsonFormatToggle.MouseEnter += () => _jsonFormatToggle.Background(
            _jsonAutoFormat ? Color.FromRgb(0x3A, 0xB0, 0x9A) : BgHover);
        _jsonFormatToggle.MouseLeave += UpdateToggleStyle;

        // ── 保存按钮 ────────────────────────────────────────────
        var saveBtn = new Button { Height = 32, Padding = new Thickness(16, 0) };
        saveBtn.Content("保存", false).FontSize(13).Background(Accent).Foreground(Color.White);
        saveBtn.MouseEnter += () => saveBtn.Background(Color.FromRgb(0x0E, 0x90, 0xE0));
        saveBtn.MouseLeave += () => saveBtn.Background(Accent);
        saveBtn.OnClick(SaveSettings);

        // ── 重置为默认 ──────────────────────────────────────────
        var resetBtn = new Button { Height = 26, Padding = new Thickness(12, 0) };
        resetBtn.Content("重置为默认", false).FontSize(11).Background(Color.Transparent).Foreground(TextSec);
        resetBtn.MouseEnter += () => resetBtn.Background(BgHover).Foreground(TextPri);
        resetBtn.MouseLeave += () => resetBtn.Background(Color.Transparent).Foreground(TextSec);
        resetBtn.OnClick(ResetDefaults);

        // ── 布局 ────────────────────────────────────────────────
        var form = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0, Margin = new Thickness(16, 12) };
        form.Add(MakeLabel("请求超时（秒）"));
        form.Add(_timeoutBox);
        form.Add(new Border { Height = 16 }); // spacer
        form.Add(MakeLabel("HTTP 代理"));
        form.Add(_proxyBox);
        form.Add(new Border { Height = 16 });
        form.Add(MakeLabel("JSON 自动格式化"));
        form.Add(new Border { Height = 6 });
        form.Add(_jsonFormatToggle);
        form.Add(new Border { Height = 24 });
        form.Add(new Border { Height = 1, Background = BorderCol, Margin = new Thickness(-16, 0) });
        form.Add(new Border { Height = 16 });

        var btnRow = new DockPanel();
        btnRow.Add(resetBtn.DockRight());
        btnRow.Add(saveBtn.DockLeft());
        form.Add(btnRow);

        var headerLabel = new TextBlock
        {
            Text              = "设置",
            FontSize          = 11,
            FontWeight        = FontWeight.SemiBold,
            Foreground        = TextSec,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(12, 0, 0, 0),
        };
        var headerRow = new Border { Height = 30, Background = BgSidebar, Child = headerLabel };

        var scroll = new ScrollViewer { VerticalScroll = ScrollMode.Auto, Content = form };
        var root = new DockPanel();
        root.Add(headerRow.DockTop());
        root.Add(new Border { Height = 1, Background = BorderCol }.DockTop());
        root.Add(scroll);

        RootElement = new Border { Background = BgSidebar, Child = root };
    }

    // ── 私有方法 ─────────────────────────────────────────────────
    private void UpdateToggleStyle()
    {
        _jsonFormatToggle
            .Content(_jsonAutoFormat ? "● 开启" : "○ 关闭", false)
            .FontSize(12)
            .Background(_jsonAutoFormat ? GreenOn : Color.FromRgb(0x50, 0x50, 0x50))
            .Foreground(_jsonAutoFormat ? Color.FromRgb(0x1E, 0x1E, 0x1E) : TextSec);
    }

    private void SaveSettings()
    {
        var timeout = 30;
        int.TryParse(_timeoutBox.Text?.Trim(), out timeout);
        if (timeout <= 0 || timeout > 300) timeout = 30;

        var settings = new AppSettings
        {
            TimeoutSeconds = timeout,
            ProxyUrl       = _proxyBox.Text?.Trim() ?? string.Empty,
            JsonAutoFormat = _jsonAutoFormat,
        };
        SettingsService.Save(settings);
        SettingsChanged?.Invoke(settings);
    }

    private void ResetDefaults()
    {
        var defaults = new AppSettings();
        _timeoutBox.Text = defaults.TimeoutSeconds.ToString();
        _proxyBox.Text   = defaults.ProxyUrl;
        _jsonAutoFormat  = defaults.JsonAutoFormat;
        UpdateToggleStyle();
    }

    private static TextBlock MakeLabel(string text) => new TextBlock
    {
        Text       = text,
        FontSize   = 11,
        Foreground = TextSec,
    };
}
