using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kx.Resty.Models;

public partial class EnvironmentVariable : ObservableObject
{
    [ObservableProperty] private string _name  = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
}

public partial class EnvironmentSet : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveIndicator))]
    private bool _isActive;

    /// <summary>Bullet shown in the sidebar next to the environment name.</summary>
    public string ActiveIndicator => IsActive ? "●" : "○";

    public ObservableCollection<EnvironmentVariable> Variables { get; } = [];
}
