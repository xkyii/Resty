using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using ReactiveUI;
using Kx.Resty.ViewModels;
using Kx.Resty.Features.Workspace.Models;
using Kx.Resty.Features.Workspace.Services;

namespace Kx.Resty.Features.Workspace.ViewModels;

public enum WorkspaceNavItemKind { Collection, Request, History }

public sealed class WorkspaceNavNode
{
    public required string Header { get; init; }
    public ObservableCollection<WorkspaceNavNode> Children { get; } = [];
    public WorkspaceNavItemKind Kind { get; init; }
    public string? Method { get; init; }
    public string? Url { get; init; }
    public string? FilePath { get; init; }
    public string? RelativePath { get; init; }
    public int SegmentIndex { get; init; } = -1;
    public string? RequestId { get; init; }
    public string? HeadersText { get; init; }
    public string? BodyText { get; init; }
    public bool NoLog { get; init; }
}

public sealed class WorkspaceNavigationViewModel : ReactiveObject
{
    private const int MaxHistoryItems = 300;

    private string _searchText = string.Empty;
    private WorkspaceNavNode? _selectedNode;
    private bool _isCollectionsMode = true;
    private string? _workspaceRootPath;

    private readonly Dictionary<string, ParsedHttpCollection> _collectionsByFile
        = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] SkippedFolders = [".git", "bin", "obj", "node_modules", ".vs"];

    public WorkspaceNavigationViewModel()
    {
        CollectionNodes = [];
        HistoryNodes = [];
        CollectionMenuRoots = [];
        HistoryMenuRoots = [];
        ActiveMenuRoots = [];

        ShowCollectionsCommand = new SimpleCommand(() => { IsCollectionsMode = true; });
        ShowHistoryCommand = new SimpleCommand(() => { IsCollectionsMode = false; });

        RebuildMenu();
    }

    public ObservableCollection<WorkspaceNavNode> CollectionNodes { get; }
    public ObservableCollection<WorkspaceNavNode> HistoryNodes { get; }
    public ObservableCollection<WorkspaceNavNode> CollectionMenuRoots { get; }
    public ObservableCollection<WorkspaceNavNode> HistoryMenuRoots { get; }
    public ObservableCollection<WorkspaceNavNode> ActiveMenuRoots { get; }

    public ICommand ShowCollectionsCommand { get; }
    public ICommand ShowHistoryCommand { get; }

    public string? WorkspaceRootPath => _workspaceRootPath;

    public string SearchText
    {
        get => _searchText;
        set { this.RaiseAndSetIfChanged(ref _searchText, value); RebuildMenu(); }
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

    public void LoadWorkspace(string? workspacePath)
    {
        _workspaceRootPath = workspacePath;
        CollectionNodes.Clear();
        HistoryNodes.Clear();
        _collectionsByFile.Clear();
        SelectedNode = null;

        if (!string.IsNullOrWhiteSpace(workspacePath) && Directory.Exists(workspacePath))
        {
            LoadCollectionsFromDirectory(workspacePath);
            LoadHistoryFromDisk();
        }

        RebuildMenu();
        this.RaisePropertyChanged(nameof(HasCollections));
    }

    public IReadOnlyDictionary<string, string> GetFileVariables(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return new Dictionary<string, string>();
        return _collectionsByFile.TryGetValue(filePath, out var c) ? c.FileVariables : new Dictionary<string, string>();
    }

    public void AddHistoryEntry(string method, string url, bool persist = true)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var normalizedMethod = string.IsNullOrWhiteSpace(method) ? "GET" : method.ToUpperInvariant();
        var header = $"{normalizedMethod} {ExtractPath(url)}";

        var existed = HistoryNodes.FirstOrDefault(x =>
            string.Equals(x.Method, normalizedMethod, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase));

        if (existed is not null)
            HistoryNodes.Remove(existed);

        HistoryNodes.Insert(0, new WorkspaceNavNode
        {
            Header = header,
            Kind = WorkspaceNavItemKind.History,
            Method = normalizedMethod,
            Url = url
        });

        while (HistoryNodes.Count > MaxHistoryItems)
            HistoryNodes.RemoveAt(HistoryNodes.Count - 1);

        RebuildMenu();
        if (persist) SaveHistoryToDisk();
    }

    public void SaveRequestChanges(WorkspaceNavNode requestNode, string name, string method, string url, string headersText, string bodyText)
    {
        if (requestNode.Kind != WorkspaceNavItemKind.Request
            || string.IsNullOrWhiteSpace(requestNode.FilePath)
            || requestNode.SegmentIndex < 0)
            return;

        var req = new ParsedHttpRequest
        {
            Id = requestNode.RequestId ?? $"{requestNode.FilePath}::{requestNode.SegmentIndex}",
            Name = string.IsNullOrWhiteSpace(name) ? $"{method} {ExtractPath(url)}" : name,
            Method = method,
            Url = url,
            HeadersText = headersText,
            BodyText = bodyText,
            SegmentIndex = requestNode.SegmentIndex,
            NoLog = requestNode.NoLog
        };

        if (HttpFileParser.TrySaveRequestBlock(requestNode.FilePath, requestNode.SegmentIndex, req))
            LoadWorkspace(_workspaceRootPath);
    }

    private void LoadCollectionsFromDirectory(string rootPath)
    {
        try
        {
            foreach (var filePath in FindHttpFiles(rootPath).OrderBy(f => f))
            {
                var parsed = HttpFileParser.ParseCollection(rootPath, filePath);
                _collectionsByFile[filePath] = parsed;

                var collectionNode = new WorkspaceNavNode
                {
                    Header = parsed.Name,
                    Kind = WorkspaceNavItemKind.Collection,
                    FilePath = parsed.FilePath,
                    RelativePath = parsed.RelativePath
                };

                foreach (var request in parsed.Requests)
                {
                    collectionNode.Children.Add(new WorkspaceNavNode
                    {
                        Header = request.Name,
                        Kind = WorkspaceNavItemKind.Request,
                        Method = request.Method,
                        Url = request.Url,
                        FilePath = parsed.FilePath,
                        RelativePath = parsed.RelativePath,
                        SegmentIndex = request.SegmentIndex,
                        RequestId = request.Id,
                        HeadersText = request.HeadersText,
                        BodyText = request.BodyText,
                        NoLog = request.NoLog
                    });
                }

                CollectionNodes.Add(collectionNode);
            }
        }
        catch { /* IO exceptions keep UI responsive */ }
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
                if (!SkippedFolders.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                    result.AddRange(FindHttpFiles(subDir));
            }
        }
        catch { }
        return result;
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
            if (filtered is not null) CollectionMenuRoots.Add(filtered);
        }

        HistoryMenuRoots.Clear();
        foreach (var node in HistoryNodes.Where(n => MatchNode(n, query)))
            HistoryMenuRoots.Add(CloneNode(node));

        RefreshActiveMenuRoots();
    }

    private void RefreshActiveMenuRoots()
    {
        ActiveMenuRoots.Clear();
        foreach (var node in IsCollectionsMode ? CollectionMenuRoots : (IEnumerable<WorkspaceNavNode>)HistoryMenuRoots)
            ActiveMenuRoots.Add(node);
    }

    private static WorkspaceNavNode? CloneFiltered(WorkspaceNavNode source, string query)
    {
        var clone = CloneNode(source);
        foreach (var child in source.Children)
        {
            var fc = CloneFiltered(child, query);
            if (fc is not null) clone.Children.Add(fc);
        }
        return (MatchNode(source, query) || clone.Children.Count > 0) ? clone : null;
    }

    private static WorkspaceNavNode CloneNode(WorkspaceNavNode source) => new()
    {
        Header = source.Header,
        Kind = source.Kind,
        Method = source.Method,
        Url = source.Url,
        FilePath = source.FilePath,
        RelativePath = source.RelativePath,
        SegmentIndex = source.SegmentIndex,
        RequestId = source.RequestId,
        HeadersText = source.HeadersText,
        BodyText = source.BodyText,
        NoLog = source.NoLog
    };

    private static bool MatchNode(WorkspaceNavNode node, string query) =>
        string.IsNullOrWhiteSpace(query)
        || node.Header.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (node.Method?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (node.Url?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (node.RelativePath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

    private string? GetHistoryFilePath()
    {
        if (string.IsNullOrWhiteSpace(_workspaceRootPath)) return null;
        return Path.Combine(_workspaceRootPath, ".resty", "history.json");
    }

    private void SaveHistoryToDisk()
    {
        var filePath = GetHistoryFilePath();
        if (string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            var data = HistoryNodes.Select(n => new HistoryRecord
            {
                Method = n.Method ?? "GET",
                Url = n.Url ?? string.Empty,
                CreatedAt = DateTimeOffset.Now
            }).ToList();

            File.WriteAllText(filePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void LoadHistoryFromDisk()
    {
        var filePath = GetHistoryFilePath();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        try
        {
            var records = JsonSerializer.Deserialize<List<HistoryRecord>>(File.ReadAllText(filePath)) ?? [];
            foreach (var r in records.OrderByDescending(x => x.CreatedAt))
                AddHistoryEntry(r.Method ?? "GET", r.Url ?? string.Empty, persist: false);
        }
        catch { }
    }

    private sealed class HistoryRecord
    {
        public string? Method { get; set; }
        public string? Url { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
