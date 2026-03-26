using Avalonia.Interactivity;
using Kx.Resty.Views;

namespace Kx.Resty.Views.Dialogs;

public partial class Preferences : ChromelessWindow
{
    public Preferences()
    {
        InitializeComponent();
        CloseOnESC = true;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Dialogs.Preferences vm)
            vm.Save();
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();
}
