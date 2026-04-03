using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kx.Resty.Features.Shell.ViewModels;

namespace Kx.Resty.Views;

public partial class TitleBarRightContent : UserControl
{
    public TitleBarRightContent() => InitializeComponent();

    private async void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanPickFolder: true } storageProvider) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
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
