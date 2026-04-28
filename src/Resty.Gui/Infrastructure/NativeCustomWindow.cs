using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;

namespace Resty.Gui.Infrastructure;

/// <summary>
/// 无边框自定义标题栏窗口，基于 DWM 帧扩展（Win11）。
/// 圆角、阴影和 resize 由 OS 处理。
/// 参考: MewUI Gallery/NativeCustomWindow.cs (MIT License)
/// </summary>
public class NativeCustomWindow : Window
{
    private const double DefaultTitleBarHeight = 28;
    private const double ButtonWidth = 46;

    private readonly Border _contentArea;
    private readonly Border _chromeBorder;
    private readonly TextBlock _titleText;
    private readonly StackPanel _controlButtons;
    protected readonly StackPanel _leftArea;
    protected readonly StackPanel _rightArea;
    private readonly Button _minimizeBtn;
    private readonly Button _maximizeBtn;

    // ── Chrome button styles ──────────────────────────────────────
    private static readonly Style ChromeButtonStyle = new(typeof(Button))
    {
        Transitions = [Transition.Create(Control.BackgroundProperty)],
        Setters =
        [
            Setter.Create(Control.BackgroundProperty,  t => t.Palette.ButtonFace.WithAlpha(0)),
            Setter.Create(Control.BorderThicknessProperty, 0.0),
            Setter.Create(Control.CornerRadiusProperty, 0.0),
            Setter.Create(Control.PaddingProperty, new Thickness(0)),
        ],
        Triggers =
        [
            new StateTrigger
            {
                Match = VisualStateFlags.Hot,
                Setters = [Setter.Create(Control.BackgroundProperty, t => t.Palette.ButtonFace)],
            },
            new StateTrigger
            {
                Match = VisualStateFlags.Pressed,
                Setters = [Setter.Create(Control.BackgroundProperty, t => t.Palette.ButtonPressedBackground)],
            },
        ],
    };

    private static readonly Style CloseButtonStyle = new(typeof(Button))
    {
        Transitions = [Transition.Create(Control.BackgroundProperty)],
        Setters =
        [
            Setter.Create(Control.BackgroundProperty,  Color.FromRgb(232, 17, 35).WithAlpha(0)),
            Setter.Create(Control.BorderThicknessProperty, 0.0),
            Setter.Create(Control.CornerRadiusProperty, 0.0),
            Setter.Create(Control.PaddingProperty, new Thickness(0)),
        ],
        Triggers =
        [
            new StateTrigger
            {
                Match = VisualStateFlags.Hot,
                Setters =
                [
                    Setter.Create(Control.BackgroundProperty, Color.FromRgb(232, 17, 35)),
                    Setter.Create(Control.ForegroundProperty, Color.White),
                ],
            },
            new StateTrigger
            {
                Match = VisualStateFlags.Pressed,
                Setters =
                [
                    Setter.Create(Control.BackgroundProperty, Color.FromRgb(200, 12, 28)),
                    Setter.Create(Control.ForegroundProperty, Color.White),
                ],
            },
        ],
    };

    // ── Constructor ───────────────────────────────────────────────
    public NativeCustomWindow()
    {
        ExtendClientAreaTitleBarHeight = DefaultTitleBarHeight;
        base.Padding = new Thickness(0);
        StyleSheet = new StyleSheet();
        StyleSheet.Define("chrome", ChromeButtonStyle);
        StyleSheet.Define("close", CloseButtonStyle);

        // Title text (centered, non-interactive)
        _titleText = new TextBlock
        {
            IsHitTestVisible = false,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            Margin = new Thickness(8, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _titleText.SetBinding(TextBlock.TextProperty, this, TitleProperty);

        // Chrome buttons
        _minimizeBtn = MakeChromeButton("─");
        _minimizeBtn.Click += () => Minimize();
        _minimizeBtn.SetBinding(UIElement.IsVisibleProperty, this, CanMinimizeProperty);

        var maxGlyph = new GlyphElement().Kind(GlyphKind.WindowMaximize).GlyphSize(4);
        _maximizeBtn = MakeChromeButton(maxGlyph);
        _maximizeBtn.Click += () =>
        {
            if (WindowState == WindowState.Maximized) Restore();
            else Maximize();
        };
        _maximizeBtn.SetBinding(UIElement.IsVisibleProperty, this, CanMaximizeProperty);

        var closeBtn = MakeChromeButton("✕", isClose: true);
        closeBtn.Click += () => Close();
        closeBtn.SetBinding(UIElement.IsVisibleProperty, this, CanCloseProperty);

        _controlButtons = new StackPanel { Orientation = Orientation.Horizontal };
        _controlButtons.Add(_minimizeBtn);
        _controlButtons.Add(_maximizeBtn);
        _controlButtons.Add(closeBtn);

        // Left / right title bar areas
        _leftArea  = new StackPanel { Orientation = Orientation.Horizontal };
        _rightArea = new StackPanel { Orientation = Orientation.Horizontal };

        // Title bar panel
        var titleBarContent = new DockPanel().Children(
            new Border().DockRight().Child(_controlButtons),
            new Border().DockRight().Child(_rightArea),
            new Border().DockLeft().Child(_leftArea),
            _titleText
        );

        var titleBar = new Border
        {
            MinHeight = DefaultTitleBarHeight,
            Child = titleBarContent,
        };
        titleBar.SetBinding(BackgroundProperty, this, BackgroundProperty);

        // Double-click to maximize/restore
        titleBar.MouseDoubleClick += e =>
        {
            if (e.Button == MouseButton.Left && CanMaximize)
            {
                if (e.GetPosition(titleBar) is Point p &&
                    (_leftArea.Bounds.Contains(p) || _rightArea.Bounds.Contains(p)))
                {
                    e.Handled = true;
                    return;
                }
                if (WindowState == WindowState.Maximized) Restore();
                else Maximize();
                e.Handled = true;
            }
        };

        // Content area
        _contentArea = new Border { Padding = new Thickness(0) };

        _chromeBorder = new Border
        {
            BorderThickness = 0,
            Child = new DockPanel().Children(
                titleBar.DockTop(),
                _contentArea
            )
        };
        _chromeBorder.SetBinding(Border.BorderBrushProperty, this, BorderBrushProperty);

        base.Content = _chromeBorder;

        ClientSizeChanged += _ =>
        {
            OnWindowStateVisualUpdate();
            UpdateChromeButtonVisibility();
        };

        Activated   += UpdateChromeAppearance;
        Deactivated += UpdateChromeAppearance;
        Loaded      += OnLoaded;
    }

    private void OnLoaded()
    {
        if (BorderBrush.A > 0
            && !ChromeCapabilities.HasFlag(WindowChromeCapabilities.NativeBorderColor)
            && !ChromeCapabilities.HasFlag(WindowChromeCapabilities.NativeWindowBorder))
        {
            _chromeBorder.BorderThickness = 1;
        }
        UpdateChromeButtonVisibility();
    }

    // ── Public API ────────────────────────────────────────────────
    /// <summary>左侧标题栏区域（菜单栏）。</summary>
    public StackPanel TitleBarLeft  => _leftArea;
    /// <summary>右侧标题栏区域（额外操作）。</summary>
    public StackPanel TitleBarRight => _rightArea;

    public new UIElement? Content
    {
        get => _contentArea.Child;
        set => _contentArea.Child = value;
    }

    public new Thickness Padding
    {
        get => _contentArea.Padding;
        set => _contentArea.Padding = value;
    }

    // ── Internals ─────────────────────────────────────────────────
    private void UpdateChromeAppearance()
    {
        var p = Theme.Palette;
        BorderBrush    = IsActive ? p.Accent : p.ControlBorder;
        _titleText.Foreground = IsActive ? p.WindowText : p.DisabledText;
    }

    private void UpdateChromeButtonVisibility()
    {
        bool hasExtend = ChromeCapabilities.HasFlag(WindowChromeCapabilities.ExtendClientArea);
        _controlButtons.IsVisible = !HasNativeChromeButtons;
        _titleText.IsVisible      = hasExtend || !HasNativeChromeButtons;
        _titleBar_SetPadding();
    }

    private void _titleBar_SetPadding()
    {
        // Keep the title bar panel in sync with native inset (e.g. macOS traffic lights)
        var inset = NativeChromeButtonInset;
        _leftArea.Margin  = new Thickness(inset.Left, 0, 0, 0);
        _rightArea.Margin = new Thickness(0, 0, inset.Right, 0);
    }

    private void OnWindowStateVisualUpdate()
    {
        bool maximized = WindowState == WindowState.Maximized;
        if (_maximizeBtn.Content is GlyphElement glyph)
            glyph.Kind = maximized ? GlyphKind.WindowRestore : GlyphKind.WindowMaximize;
    }

    private Button MakeChromeButton(string text, bool isClose = false) =>
        new Button
        {
            Content     = new Label { Text = text, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            MinWidth    = ButtonWidth,
            MinHeight   = DefaultTitleBarHeight,
            StyleName   = isClose ? "close" : "chrome",
        };

    private Button MakeChromeButton(Element content, bool isClose = false) =>
        new Button
        {
            Content   = content,
            MinWidth  = ButtonWidth,
            MinHeight = DefaultTitleBarHeight,
            StyleName = isClose ? "close" : "chrome",
        };
}
