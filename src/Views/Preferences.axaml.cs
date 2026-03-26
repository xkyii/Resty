using Avalonia.Interactivity;
using Kx.Resty.Views;

namespace Kx.Resty.Views.Dialogs;

public partial class Preferences : ChromelessWindow
{
    public Preferences()
    {
        InitializeComponent();
        DataContext = ViewModels.Preferences.Instance;
        CloseOnESC = true;
    }

    private void OnOKClicked(object? sender, RoutedEventArgs e)
    {
        ViewModels.Preferences.Instance.Save();
        Close();
    }
}
