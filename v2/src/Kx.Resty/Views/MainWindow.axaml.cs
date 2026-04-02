using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kx.Resty.Features.Shell.ViewModels;
using Ursa.Controls;

namespace Kx.Resty.Views;

public partial class MainWindow : UrsaWindow
{
    public MainWindow() => InitializeComponent();

    private void DirectoryMenuDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm
            && vm.IsDirectoryManagerMode
            && vm.DirectoryManager.HasSelection)
        {
            vm.DirectoryManager.OpenSelectedInWorkspace();
        }
    }

    private async void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!vm.IsDirectoryManagerMode) return;
        if (!StorageProvider.CanPickFolder) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择文件夹",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(path))
            await vm.DirectoryManager.OpenPathAsync(path);
    }
}
