using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.ViewModels;

public partial class CollectionNode : ObservableObject
{
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsFolder { get; init; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
                OnPropertyChanged(nameof(ChevronAngle));
        }
    }

    /// <summary>0° = collapsed (chevron pointing right → down), 90° = expanded.</summary>
    public double ChevronAngle => IsExpanded ? 0.0 : -90.0;

    public string? Method { get; init; }

    public ObservableCollection<CollectionNode> Children { get; } = [];

    [RelayCommand]
    public void ToggleExpanded() => IsExpanded = !IsExpanded;

    private string _name = string.Empty;
    private bool _isExpanded = false;
}

public partial class CollectionPanel : ObservableObject
{
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public ObservableCollection<CollectionNode> Collections { get; } = new(
    [
        new CollectionNode
        {
            Name = "Sample Collection",
            IsFolder = true,
            IsExpanded = true,
            Children =
            {
                new CollectionNode { Name = "Get Users",    IsFolder = false, Method = "GET"  },
                new CollectionNode { Name = "Create User",  IsFolder = false, Method = "POST" },
                new CollectionNode { Name = "Delete User",  IsFolder = false, Method = "DELETE" },
            }
        },
        new CollectionNode
        {
            Name = "Auth",
            IsFolder = true,
            IsExpanded = false,
            Children =
            {
                new CollectionNode { Name = "Login",   IsFolder = false, Method = "POST" },
                new CollectionNode { Name = "Refresh", IsFolder = false, Method = "POST" },
            }
        }
    ]);

    public ObservableCollection<CollectionNode> Environments { get; } = new(
    [
        new CollectionNode { Name = "Development", IsFolder = false },
        new CollectionNode { Name = "Production",  IsFolder = false },
    ]);

    public ObservableCollection<CollectionNode> History { get; } = new(
    [
        new CollectionNode { Name = "GET /api/users",   IsFolder = false, Method = "GET"  },
        new CollectionNode { Name = "POST /api/login",  IsFolder = false, Method = "POST" },
    ]);

    private string _searchText = string.Empty;
}