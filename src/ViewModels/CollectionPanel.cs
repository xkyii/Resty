using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kx.Resty.Models;

namespace Kx.Resty.ViewModels;

public partial class CollectionPanel : ObservableObject
{
    [ObservableProperty] private string          _searchText        = string.Empty;
    [ObservableProperty] private EnvironmentSet? _activeEnvironment;
    [ObservableProperty] private HttpCollection? _selectedCollection;

    public ObservableCollection<CollectionTreeNode> RootNodes    { get; } = [];
    public ObservableCollection<EnvironmentSet>     Environments { get; } = [];

    /// <summary>
    /// Delegate set by <see cref="WorkspaceTab"/> to handle request-open events
    /// triggered from the sidebar.
    /// </summary>
    public Action<HttpRequestEntry, HttpCollection>? OnRequestOpen { get; set; }

    /// <summary>Called from CollectionNodeView code-behind when a request row is tapped.</summary>
    public void OpenRequest(HttpRequestEntry entry, HttpCollection collection)
    {
        SelectedCollection = collection;
        OnRequestOpen?.Invoke(entry, collection);
    }

    [RelayCommand]
    public void SelectEnvironment(EnvironmentSet env)
    {
        foreach (var e in Environments)
            e.IsActive = e == env;
        ActiveEnvironment = env;
    }
}
