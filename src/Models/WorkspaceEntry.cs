using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kx.Resty.Models;

public partial class WorkspaceEntry : ObservableObject
{
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private DateTime _lastOpenedAt;

    [JsonIgnore]
    public bool IsMissing => !string.IsNullOrEmpty(Path) && !Directory.Exists(Path);

    [JsonIgnore]
    public bool IsValid => string.IsNullOrEmpty(Path) || Directory.Exists(Path);

    [property: JsonIgnore]
    [ObservableProperty]
    private bool _isSelected;

    partial void OnPathChanged(string value)
    {
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(IsValid));
    }
}
