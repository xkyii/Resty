using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

    private async void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        // 仅在目录管理模式下响应
        if (!vm.IsDirectoryManagerMode)
            return;

        if (!StorageProvider.CanPickFolder)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择文件夹",
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return;

        var path = folders[0].Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        // 将选中路径交给 DirectoryManager 处理（会校验、加入最近并触发打开）
        await vm.DirectoryManager.OpenPathAsync(path);
    }
}