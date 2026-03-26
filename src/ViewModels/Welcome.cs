using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.ViewModels;

public partial class Welcome : ObservableObject
{
    public MainWindow? Owner { get; set; }

    [RelayCommand]
    public void NewRequest()
    {
        Owner?.NewRequestCommand.Execute(null);
    }
}