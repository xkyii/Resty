using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.Models;

public partial class CollectionTreeNode : ObservableObject
{
    public string          Name        { get; init; } = string.Empty;
    /// <summary>true = directory node; false = .http file node.</summary>
    public bool            IsDirectory { get; init; }
    /// <summary>Non-null when <see cref="IsDirectory"/> is false.</summary>
    public HttpCollection? Collection  { get; init; }

    /// <summary>Sub-folders / sibling .http files (populated when IsDirectory).</summary>
    public ObservableCollection<CollectionTreeNode> Children { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronAngle))]
    private bool _isExpanded;

    public double ChevronAngle => IsExpanded ? 0.0 : -90.0;

    [RelayCommand]
    public void ToggleExpanded() => IsExpanded = !IsExpanded;
}
