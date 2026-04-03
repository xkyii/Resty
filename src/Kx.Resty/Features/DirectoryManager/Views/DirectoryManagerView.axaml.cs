using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Kx.Resty.Features.DirectoryManager.ViewModels;

namespace Kx.Resty.Features.DirectoryManager.Views;

public partial class DirectoryManagerView : UserControl
{
    public DirectoryManagerView() => InitializeComponent();

    private void NavMenuDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is DirectoryManagerViewModel vm && vm.HasSelection)
            vm.OpenSelectedInWorkspace();
    }

    private async void OpenFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DirectoryManagerViewModel vm) return;
        if (!TopLevel.GetTopLevel(this)!.StorageProvider.CanPickFolder) return;

        var folders = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择文件夹",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;
        var path = folders[0].Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(path))
            await vm.OpenPathAsync(path);
    }
}
