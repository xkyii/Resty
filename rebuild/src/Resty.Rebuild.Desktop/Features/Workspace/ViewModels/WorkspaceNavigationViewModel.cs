using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
        CollectionNodes = [];
        HistoryNodes = [];

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

    /// <summary>
    /// 从磁盘路径加载工作区。若路径有效则扫描 .http 文件作为集合；
    /// 传 null 或空字符串表示清空（无工作区状态）。
    /// </summary>
    public void LoadWorkspace(string? workspacePath)
    {
        CollectionNodes.Clear();
        HistoryNodes.Clear();
        SelectedNode = null;

        if (!string.IsNullOrWhiteSpace(workspacePath) && Directory.Exists(workspacePath))
        {
            LoadFromDirectory(workspacePath);
        }

        RebuildMenu();
        this.RaisePropertyChanged(nameof(HasCollections));
    }

    private static readonly string[] SkippedFolders = [".git", "bin", "obj", "node_modules", ".vs"];

    private void LoadFromDirectory(string rootPath)
    {
        try
        {
            var httpFiles = FindHttpFiles(rootPath);
            foreach (var filePath in httpFiles.OrderBy(f => f))
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var relPath = Path.GetRelativePath(rootPath, filePath);
                var collectionNode = new WorkspaceNavNode
                {
                    Header = fileName,
                    Kind = WorkspaceNavItemKind.Collection
                };
                // P3 将在此处解析 ### block；目前只显示集合节点
                CollectionNodes.Add(collectionNode);
            }
        }
        catch { /* 权限或 IO 异常：静默跳过，外层已做校验 */ }
    }

    private static IEnumerable<string> FindHttpFiles(string directory)
    {
        var result = new List<string>();
        try
        {
            result.AddRange(Directory.GetFiles(directory, "*.http", SearchOption.TopDirectoryOnly));
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var dirName = Path.GetFileName(subDir);
                if (SkippedFolders.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                    continue;
                result.AddRange(FindHttpFiles(subDir));
            }
        }
        catch { /* 权限异常跳过该子目录 */ }
        return result;
    }

    public void AddHistoryEntry(string method, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var header = $"{method.ToUpperInvariant()} {ExtractPath(url)}";

        var existed = HistoryNodes.FirstOrDefault(x =>
            string.Equals(x.Method, method, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase));

        if (existed is not null)
            HistoryNodes.Remove(existed);

        HistoryNodes.Insert(0, new WorkspaceNavNode
        {
            Header = header,
            Kind = WorkspaceNavItemKind.History,
            Method = method.ToUpperInvariant(),
            Url = url
        });

        RebuildMenu();
    }

    private static string ExtractPath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        return string.IsNullOrWhiteSpace(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
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
