using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Kx.Resty.Views;

public partial class MainWindow : ChromelessWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void MinimizeWindow(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    public void MaximizeWindow(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    public void CloseWindow(object? sender, RoutedEventArgs e) =>
        Close();

    public void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsOverInteractiveControl(e.Source, sender as Visual))
            return;
        BeginMoveWindow(sender, e);
    }

    public void TitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsOverInteractiveControl(e.Source, sender as Visual))
            return;
        MaximizeOrRestoreWindow(sender, e);
    }

    private static bool IsOverInteractiveControl(object? source, Visual? root)
    {
        var v = source as Visual;
        if (v is null)
            return false;

        while (v != null && !ReferenceEquals(v, root))
        {
            if (v is Button)
                return true;
            v = v.GetVisualParent();
        }
        return false;
    }
}