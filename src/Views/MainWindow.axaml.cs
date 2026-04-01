using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using System.Windows.Input;

namespace Kx.Resty.Views;

public partial class MainWindow : ChromelessWindow
{
    public static ICommand QuitCommand { get; } =
        new App.SimpleCommand(_ =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown(0);
        });

    public static ICommand OpenAboutCommand { get; } =
        new App.SimpleCommand(_ =>
        {
            var dialog = new ChromelessWindow
            {
                Title = App.Text("Menu.About"),
                Width = 420,
                Height = 220,
                CanResize = false,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = App.Text("App.Name"),
                            FontSize = 20,
                            FontWeight = Avalonia.Media.FontWeight.SemiBold
                        },
                        new TextBlock { Text = App.Text("Welcome.Subtitle"), Opacity = 0.8 },
                        new TextBlock { Text = "Copyright (c) 2026 Kx." , Opacity = 0.6}
                    }
                }
            };

            App.ShowDialog(dialog);
        });

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

    public void ToggleDirectoryManagerMode(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainWindow vm)
            vm.ToggleDirectoryManagerModeCommand.Execute(null);

        e.Handled = true;
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