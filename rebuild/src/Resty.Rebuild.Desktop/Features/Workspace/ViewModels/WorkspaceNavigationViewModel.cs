using System;
using System.Collections.Generic;
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
    private bool _isCollectionsMode = true;

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

        ShowCollectionsCommand = ReactiveCommand.Create(() => { IsCollectionsMode = true; });
        ShowHistoryCommand = ReactiveCommand.Create(() => { IsCollectionsMode = false; });

        RebuildMenu();
    }

    public ObservableCollection<WorkspaceNavNode> CollectionNodes { get; }

    public ObservableCollection<WorkspaceNavNode> HistoryNodes { get; }

    public ObservableCollection<WorkspaceNavNode> CollectionMenuRoots { get; } = [];

    public ObservableCollection<WorkspaceNavNode> HistoryMenuRoots { get; } = [];

    public ObservableCollection<WorkspaceNavNode> ActiveMenuRoots { get; } = [];

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ShowCollectionsCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ShowHistoryCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            RebuildMenu();
        }
    }

    public bool IsCollectionsMode
    {
        get => _isCollectionsMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isCollectionsMode, value);
            this.RaisePropertyChanged(nameof(IsHistoryMode));
            RefreshActiveMenuRoots();
        }
    }

    public bool IsHistoryMode => !IsCollectionsMode;

    public WorkspaceNavNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            this.RaisePropertyChanged(nameof(IsCollectionNodeSelected));
            this.RaisePropertyChanged(nameof(SelectedCollectionHasRequests));
        }
    }

    public bool HasCollections => CollectionNodes.Count > 0;

    public bool IsCollectionNodeSelected => SelectedNode?.Kind == WorkspaceNavItemKind.Collection;

    public bool SelectedCollectionHasRequests =>
        SelectedNode?.Kind != WorkspaceNavItemKind.Collection || SelectedNode.Children.Count > 0;

    public void LoadWorkspace(string? workspaceName)
    {
        CollectionNodes.Clear();
        HistoryNodes.Clear();
        SelectedNode = null;

        if (string.IsNullOrWhiteSpace(workspaceName) || workspaceName == "未打开工作区")
        {
            RebuildMenu();
            this.RaisePropertyChanged(nameof(HasCollections));
            return;
        }

        if (workspaceName == "sandbox")
        {
            RebuildMenu();
            this.RaisePropertyChanged(nameof(HasCollections));
            return;
        }

        if (workspaceName == "backend-service")
        {
            CollectionNodes.Add(new WorkspaceNavNode
            {
                Header = "后端集合",
                Kind = WorkspaceNavItemKind.Collection
            });

            HistoryNodes.Add(new WorkspaceNavNode
            {
                Header = "GET /health",
                Kind = WorkspaceNavItemKind.History,
                Method = "GET",
                Url = "https://api.example.com/health"
            });

            RebuildMenu();
            this.RaisePropertyChanged(nameof(HasCollections));
            return;
        }

        CollectionNodes.Add(new WorkspaceNavNode
        {
            Header = "用户服务集合",
            Kind = WorkspaceNavItemKind.Collection,
            Children =
            {
                new WorkspaceNavNode { Header = "GET /users", Kind = WorkspaceNavItemKind.Request, Method = "GET", Url = "https://api.example.com/users" },
                new WorkspaceNavNode { Header = "POST /users", Kind = WorkspaceNavItemKind.Request, Method = "POST", Url = "https://api.example.com/users" }
            }
        });

        CollectionNodes.Add(new WorkspaceNavNode
        {
            Header = "认证集合",
            Kind = WorkspaceNavItemKind.Collection,
            Children =
            {
                new WorkspaceNavNode { Header = "POST /login", Kind = WorkspaceNavItemKind.Request, Method = "POST", Url = "https://api.example.com/login" }
            }
        });

        HistoryNodes.Add(new WorkspaceNavNode { Header = "GET /users", Kind = WorkspaceNavItemKind.History, Method = "GET", Url = "https://api.example.com/users" });
        HistoryNodes.Add(new WorkspaceNavNode { Header = "POST /login", Kind = WorkspaceNavItemKind.History, Method = "POST", Url = "https://api.example.com/login" });

        RebuildMenu();
        this.RaisePropertyChanged(nameof(HasCollections));
    }

    private void RebuildMenu()
    {
        var query = SearchText.Trim();

        CollectionMenuRoots.Clear();
        foreach (var node in CollectionNodes)
        {
            var filtered = CloneFiltered(node, query);
            if (filtered is not null)
                CollectionMenuRoots.Add(filtered);
        }

        HistoryMenuRoots.Clear();
        foreach (var node in HistoryNodes.Where(n => MatchNode(n, query)))
        {
            HistoryMenuRoots.Add(new WorkspaceNavNode
            {
                Header = node.Header,
                Kind = node.Kind,
                Method = node.Method,
                Url = node.Url
            });
        }

        RefreshActiveMenuRoots();
    }

    private void RefreshActiveMenuRoots()
    {
        ActiveMenuRoots.Clear();
        IEnumerable<WorkspaceNavNode> source = IsCollectionsMode ? CollectionMenuRoots : HistoryMenuRoots;
        foreach (var node in source)
            ActiveMenuRoots.Add(node);
    }

    private static WorkspaceNavNode? CloneFiltered(WorkspaceNavNode source, string query)
    {
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

        if (MatchNode(source, query) || clone.Children.Count > 0)
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
