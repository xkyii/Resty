using CommunityToolkit.Mvvm.ComponentModel;

namespace Kx.Resty.Models;

public partial class HttpCollection : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;
    public string Name     { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronAngle))]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isDirty;

    public double ChevronAngle => IsExpanded ? 0.0 : -90.0;

    public List<InPlaceVariable>  Variables { get; } = [];
    public List<HttpRequestEntry> Requests  { get; } = [];
}
