using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Kx.Resty.Views
{
    public class ChromelessWindow : Window
    {
        public static readonly StyledProperty<double> LeftCaptionButtonWidthProperty =
            AvaloniaProperty.Register<ChromelessWindow, double>(nameof(LeftCaptionButtonWidth), 0.0);

        public double LeftCaptionButtonWidth
        {
            get => GetValue(LeftCaptionButtonWidthProperty);
            set => SetValue(LeftCaptionButtonWidthProperty, value);
        }

        public bool HasLeftCaptionButton => LeftCaptionButtonWidth > 0;

        public double CaptionHeight { get; } = 38;

        public bool CloseOnESC { get; set; } = false;

        protected override Type StyleKeyOverride => typeof(Window);

        public ChromelessWindow()
        {
            Focusable = true;

            if (OperatingSystem.IsMacOS())
            {
                // On macOS the native traffic light buttons are on the left
                LeftCaptionButtonWidth = 72;
            }
        }

        public void BeginMoveWindow(object? _, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 1)
                BeginMoveDrag(e);
            e.Handled = true;
        }

        public void MaximizeOrRestoreWindow(object? _, TappedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (Classes.Contains("custom_window_frame") && CanResize)
            {
                string[] borderNames =
                [
                    "PART_BorderTopLeft", "PART_BorderTop", "PART_BorderTopRight",
                    "PART_BorderLeft", "PART_BorderRight",
                    "PART_BorderBottomLeft", "PART_BorderBottom", "PART_BorderBottomRight",
                ];

                foreach (var name in borderNames)
                {
                    var border = e.NameScope.Find<Border>(name);
                    if (border != null)
                    {
                        border.PointerPressed -= OnWindowBorderPointerPressed;
                        border.PointerPressed += OnWindowBorderPointerPressed;
                    }
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty && OperatingSystem.IsWindows())
            {
                if (WindowState == WindowState.Maximized)
                {
                    BorderThickness = new Thickness(0);
                    Padding = new Thickness(8, 6, 8, 8);
                }
                else
                {
                    BorderThickness = new Thickness(1);
                    Padding = new Thickness(0);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.Handled && e is { Key: Key.Escape, KeyModifiers: KeyModifiers.None } && CloseOnESC)
            {
                Close();
                e.Handled = true;
            }
        }

        private void OnWindowBorderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is WindowEdge edge)
                BeginResizeDrag(edge, e);
        }
    }
}
