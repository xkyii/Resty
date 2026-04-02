using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Resty.Rebuild.Desktop.ViewModels;

namespace Resty.Rebuild.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void TitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleMaximizeWindow(sender, new RoutedEventArgs());
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ToggleMaximizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DirectoryMenuDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (!vm.IsDirectoryManagerMode)
            return;

        if (!vm.DirectoryManager.HasSelection)
            return;

        vm.DirectoryManager.OpenSelectedInWorkspace();
    }
}