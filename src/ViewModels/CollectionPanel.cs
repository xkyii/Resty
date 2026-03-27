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

    public CollectionPanel()
    {
        RootNodes.CollectionChanged += (_, _) => ApplyFilter();
    }

    /// <summary>Called from CollectionNodeView code-behind when a request row is tapped.</summary>
    public void OpenRequest(HttpRequestEntry entry, HttpCollection collection)
    {
        SelectedCollection = collection;
        OnRequestOpen?.Invoke(entry, collection);
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
