using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kx.Resty.Models;

namespace Kx.Resty.ViewModels;

public partial class CollectionPanel : ObservableObject
{
    [ObservableProperty] private string          _searchText        = string.Empty;
    [ObservableProperty] private EnvironmentSet? _activeEnvironment;
    [ObservableProperty] private HttpCollection? _selectedCollection;

    public string WorkspacePath { get; set; } = string.Empty;

    public ObservableCollection<CollectionTreeNode> RootNodes         { get; } = [];
    public ObservableCollection<CollectionTreeNode> FilteredRootNodes { get; } = [];
    public ObservableCollection<EnvironmentSet>     Environments      { get; } = [];

    /// <summary>
    /// Delegate set by <see cref="WorkspaceTab"/> to handle request-open events
    /// triggered from the sidebar.
    /// </summary>
    public Action<HttpRequestEntry, HttpCollection>? OnRequestOpen { get; set; }

    // ─── Selection tracking ───────────────────────────────────────────────────

    private CollectionTreeNode?  _selectedNode;
    private HttpRequestEntry?    _selectedEntry;

    public CollectionPanel()
    {
        RootNodes.CollectionChanged += (_, _) => ApplyFilter();
    }

    /// <summary>Called from CollectionNodeView code-behind when a request row is tapped.</summary>
    public void OpenRequest(HttpRequestEntry entry, HttpCollection collection)
    {
        TryRefreshEntryFromDisk(collection, entry);

        // Clear old selection
        if (_selectedEntry != null) _selectedEntry.IsSelected = false;
        if (_selectedNode  != null) _selectedNode.IsSelected  = false;

        // Set new selection
        entry.IsSelected = true;
        _selectedEntry = entry;

        var node = FindNode(collection);
        if (node != null) { node.IsSelected = true; _selectedNode = node; }

        SelectedCollection = collection;
        OnRequestOpen?.Invoke(entry, collection);
    }

    private static void TryRefreshEntryFromDisk(HttpCollection collection, HttpRequestEntry target)
    {
        try
        {
            var index = collection.Requests.IndexOf(target);
            if (index < 0) return;

            var latest = Commands.HttpFileParser.Parse(collection.FilePath);
            if (index >= latest.Requests.Count) return;

            CopyRequest(latest.Requests[index], target);
        }
        catch
        {
            // Best-effort refresh; keep current in-memory values if parsing fails.
        }
    }

    private static void CopyRequest(HttpRequestEntry src, HttpRequestEntry dst)
    {
        dst.Name = src.Name;
        dst.Method = src.Method;
        dst.Url = src.Url;
        dst.Body = src.Body;
        dst.BodyFilePath = src.BodyFilePath;

        dst.Headers.Clear();
        foreach (var h in src.Headers)
        {
            dst.Headers.Add(new NamedValue
            {
                Enabled = h.Enabled,
                Key = h.Key,
                Value = h.Value,
            });
        }

        dst.QueryParams.Clear();
        foreach (var p in src.QueryParams)
        {
            dst.QueryParams.Add(new NamedValue
            {
                Enabled = p.Enabled,
                Key = p.Key,
                Value = p.Value,
            });
        }

        dst.Annotations.NoRedirect = src.Annotations.NoRedirect;
        dst.Annotations.NoLog = src.Annotations.NoLog;
        dst.Annotations.NoCookieJar = src.Annotations.NoCookieJar;
        dst.Annotations.NoAutoEncoding = src.Annotations.NoAutoEncoding;
        dst.Annotations.TimeoutSeconds = src.Annotations.TimeoutSeconds;
        dst.Annotations.ConnectionTimeoutSeconds = src.Annotations.ConnectionTimeoutSeconds;
    }

    /// <summary>Called when a collection node header is clicked (expand/collapse).</summary>
    public void SelectCollectionNode(CollectionTreeNode node)
    {
        if (_selectedEntry != null)
        {
            _selectedEntry.IsSelected = false;
            _selectedEntry = null;
        }

        if (_selectedNode != null) _selectedNode.IsSelected = false;
        node.IsSelected = true;
        _selectedNode = node;

        SelectedCollection = node.Collection;
    }

    /// <summary>
    /// Synchronizes left-side selection state from top request-tab selection.
    /// This updates highlights only and does not trigger open-request callbacks.
    /// </summary>
    public void SyncSelectionFromRequest(HttpRequestEntry? entry, HttpCollection? collection)
    {
        if (_selectedEntry != null)
            _selectedEntry.IsSelected = false;

        if (_selectedNode != null)
            _selectedNode.IsSelected = false;

        _selectedEntry = null;
        _selectedNode = null;

        if (entry is null || collection is null)
            return;

        entry.IsSelected = true;
        _selectedEntry = entry;

        var node = FindNode(collection);
        if (node != null)
        {
            node.IsSelected = true;
            _selectedNode = node;
            SelectedCollection = collection;
        }
    }

    private CollectionTreeNode? FindNode(HttpCollection collection)
    {
        foreach (var root in RootNodes)
        {
            var found = FindNodeIn(root, collection);
            if (found != null) return found;
        }
        return null;
    }

    private static CollectionTreeNode? FindNodeIn(CollectionTreeNode node, HttpCollection collection)
    {
        if (ReferenceEquals(node.Collection, collection)) return node;
        foreach (var child in node.Children)
        {
            var found = FindNodeIn(child, collection);
            if (found != null) return found;
        }
        return null;
    }

    [RelayCommand]
    public void CreateCollection()
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath) || !Directory.Exists(WorkspacePath)) return;

        var baseName = "new-collection";
        var path = Path.Combine(WorkspacePath, baseName + ".http");
        var index = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(WorkspacePath, $"{baseName}-{index}.http");
            index++;
        }

        File.WriteAllText(path, "### New Request\nGET https://example.com/\n\n");
    }

    [RelayCommand]
    public async Task ImportCollection()
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath) || !Directory.Exists(WorkspacePath)) return;

        var mainWindow =
            (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        if (mainWindow is null) return;

        var result = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import HTTP Collection",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("HTTP Files")
                {
                    Patterns = ["*.http"]
                }
            ]
        });

        if (result.Count == 0) return;
        var sourcePath = result[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return;

        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(WorkspacePath, fileName);
        if (File.Exists(destPath))
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var i = 1;
            while (File.Exists(destPath))
            {
                destPath = Path.Combine(WorkspacePath, $"{name}-{i}{ext}");
                i++;
            }
        }

        File.Copy(sourcePath, destPath);
    }

    public bool RenameCollection(HttpCollection collection, string newName)
    {
        if (collection is null) return false;
        if (string.IsNullOrWhiteSpace(newName)) return false;

        var oldPath = collection.FilePath;
        var dir = Path.GetDirectoryName(oldPath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return false;

        var sanitized = SanitizeFileName(newName);
        if (string.IsNullOrWhiteSpace(sanitized)) return false;

        var newPath = Path.Combine(dir, sanitized + ".http");
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            collection.Name = sanitized;
            return true;
        }

        if (File.Exists(newPath))
        {
            var i = 1;
            while (File.Exists(newPath))
            {
                newPath = Path.Combine(dir, $"{sanitized}-{i}.http");
                i++;
            }
        }

        File.Move(oldPath, newPath);
        collection.FilePath = newPath;
        collection.Name = Path.GetFileNameWithoutExtension(newPath);
        return true;
    }

    /// <summary>
    /// Renames a request entry within a collection (changes text after ### in the .http file).
    /// </summary>
    public bool RenameRequest(HttpCollection collection, HttpRequestEntry entry, string newName)
    {
        if (collection is null || entry is null) return false;

        var trimmed = newName.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;

        entry.Name = trimmed;
        Commands.HttpFileWriter.Write(collection);
        return true;
    }

    [RelayCommand]
    public void SelectEnvironment(EnvironmentSet env)
    {
        foreach (var e in Environments)
            e.IsActive = e == env;
        ActiveEnvironment = env;
    }

    partial void OnSearchTextChanged(string value)
        => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        FilteredRootNodes.Clear();

        if (string.IsNullOrEmpty(query))
        {
            foreach (var node in RootNodes)
                FilteredRootNodes.Add(node);
            return;
        }

        foreach (var node in RootNodes)
        {
            var matched = FilterNode(node, query);
            if (matched is not null)
                FilteredRootNodes.Add(matched);
        }
    }

    private static CollectionTreeNode? FilterNode(CollectionTreeNode node, string query)
    {
        if (!node.IsDirectory)
            return MatchCollectionNode(node, query) ? node : null;

        var matchedChildren = new List<CollectionTreeNode>();
        foreach (var child in node.Children)
        {
            var matched = FilterNode(child, query);
            if (matched is not null)
                matchedChildren.Add(matched);
        }

        if (Contains(node.Name, query))
            return node;

        if (matchedChildren.Count == 0)
            return null;

        var folder = new CollectionTreeNode
        {
            Name = node.Name,
            IsDirectory = true,
            IsExpanded = true
        };
        foreach (var child in matchedChildren)
            folder.Children.Add(child);
        return folder;
    }

    private static bool MatchCollectionNode(CollectionTreeNode node, string query)
    {
        if (Contains(node.Name, query)) return true;
        var collection = node.Collection;
        if (collection is null) return false;

        if (Contains(collection.Name, query)) return true;
        foreach (var req in collection.Requests)
        {
            if (Contains(req.DisplayName, query) ||
                Contains(req.Method, query) ||
                Contains(req.Url, query))
                return true;
        }

        return false;
    }

    private static bool Contains(string value, string query)
        => value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Trim().Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars).Trim();
    }
}
