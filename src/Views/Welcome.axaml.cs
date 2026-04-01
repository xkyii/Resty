using Avalonia.Controls;
using Avalonia.Input;
using Kx.Resty.Models;

namespace Kx.Resty.Views;

public partial class Welcome : UserControl
{
    public Welcome()
    {
        InitializeComponent();
    }

    private void OnRecentEntryPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: WorkspaceEntry entry } && DataContext is ViewModels.MainWindow vm)
            vm.SelectRecentEntryCommand.Execute(entry);
    }

    private void OnManagedEntryPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: WorkspaceEntry entry } && DataContext is ViewModels.MainWindow vm)
            vm.SelectManagedEntryCommand.Execute(entry);
    }

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { DataContext: WorkspaceEntry entry } && DataContext is ViewModels.MainWindow vm)
            vm.OpenEntryCommand.Execute(entry);
    }
}