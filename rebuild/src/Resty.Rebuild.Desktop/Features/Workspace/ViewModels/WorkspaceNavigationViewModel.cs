using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;

namespace Resty.Rebuild.Desktop.Features.Workspace.ViewModels;

public enum WorkspaceNavItemKind
{
    Collection,
    Request,
    History
}

public sealed class WorkspaceNavNode
{
    public required string Header { get; init; }

    public ObservableCollection<WorkspaceNavNode> Children { get; } = [];

    public WorkspaceNavItemKind Kind { get; init; }

    public string? Method { get; init; }

    public string? Url { get; init; }
}

public sealed class WorkspaceNavigationViewModel : ReactiveObject
{
    private string _searchText = string.Empty;
    private WorkspaceNavNode? _selectedNode;

    public WorkspaceNavigationViewModel()
    {
        CollectionNodes =
        [
            new WorkspaceNavNode
            {
                Header = "用户服务集合",
                Kind = WorkspaceNavItemKind.Collection,
                Children =
                {
                    new WorkspaceNavNode { Header = "GET /users", Kind = WorkspaceNavItemKind.Request, Method = "GET", Url = "https://api.example.com/users" },
                    new WorkspaceNavNode { Header = "POST /users", Kind = WorkspaceNavItemKind.Request, Method = "POST", Url = "https://api.example.com/users" }
                }
            },
            new WorkspaceNavNode
            {
                Header = "认证集合",
                Kind = WorkspaceNavItemKind.Collection,
                Children =
                {
                    new WorkspaceNavNode { Header = "POST /login", Kind = WorkspaceNavItemKind.Request, Method = "POST", Url = "https://api.example.com/login" }
                }
            }
        ];

        HistoryNodes =
        [
            new WorkspaceNavNode { Header = "GET /users", Kind = WorkspaceNavItemKind.History, Method = "GET", Url = "https://api.example.com/users" },
            new WorkspaceNavNode { Header = "POST /login", Kind = WorkspaceNavItemKind.History, Method = "POST", Url = "https://api.example.com/login" }
        ];

        RebuildMenu();
    }

    public ObservableCollection<WorkspaceNavNode> CollectionNodes { get; }

    public ObservableCollection<WorkspaceNavNode> HistoryNodes { get; }

    public ObservableCollection<WorkspaceNavNode> MenuRoots { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            RebuildMenu();
        }
    }

    public WorkspaceNavNode? SelectedNode
    {
        get => _selectedNode;
        set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
    }

    private void RebuildMenu()
    {
        var query = SearchText.Trim();

        MenuRoots.Clear();

        var collectionsRoot = new WorkspaceNavNode
        {
            Header = "集合",
            Kind = WorkspaceNavItemKind.Collection
        };

        foreach (var collection in CollectionNodes)
        {
            var cloned = CloneFiltered(collection, query);
            if (cloned is not null)
                collectionsRoot.Children.Add(cloned);
        }

        var historyRoot = new WorkspaceNavNode
        {
            Header = "历史",
            Kind = WorkspaceNavItemKind.History
        };

        foreach (var history in HistoryNodes.Where(x => MatchNode(x, query)))
        {
            historyRoot.Children.Add(new WorkspaceNavNode
            {
                Header = history.Header,
                Kind = history.Kind,
                Method = history.Method,
                Url = history.Url
            });
        }

        MenuRoots.Add(collectionsRoot);
        MenuRoots.Add(historyRoot);
    }

    private static WorkspaceNavNode? CloneFiltered(WorkspaceNavNode source, string query)
    {
        var matchSelf = MatchNode(source, query);

        var clone = new WorkspaceNavNode
        {
            Header = source.Header,
            Kind = source.Kind,
            Method = source.Method,
            Url = source.Url
        };

        foreach (var child in source.Children)
        {
            var filteredChild = CloneFiltered(child, query);
            if (filteredChild is not null)
                clone.Children.Add(filteredChild);
        }

        if (matchSelf || clone.Children.Count > 0)
            return clone;

        return null;
    }

    private static bool MatchNode(WorkspaceNavNode node, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return node.Header.Contains(query, StringComparison.OrdinalIgnoreCase)
               || (node.Method?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
               || (node.Url?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
