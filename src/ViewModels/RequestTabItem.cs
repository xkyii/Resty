using CommunityToolkit.Mvvm.ComponentModel;

namespace Kx.Resty.ViewModels;

public partial class RequestTabItem : ObservableObject
{
    [ObservableProperty] private string      _title    = "New Request";
    [ObservableProperty] private RequestTab? _content;
    [ObservableProperty] private bool        _isActive;
}
