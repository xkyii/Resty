using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;

namespace Resty.Rebuild.Desktop.Features.DirectoryManager.ViewModels;

public enum DirectoryEntryKind
{
    Recent,
    Managed
}

public sealed class DirectoryEntryItem
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public DateTime LastOpenedAt { get; init; }

    public DirectoryEntryKind Kind { get; init; }
}

public sealed class DirectoryMenuNode
{
    public required string Header { get; init; }

    public ObservableCollection<DirectoryMenuNode> Children { get; } = [];

    public DirectoryEntryItem? Entry { get; init; }
}

public sealed class DirectoryManagerViewModel : ReactiveObject
{
    private string _searchText = string.Empty;
    private DirectoryEntryItem? _selectedEntry;
    private DirectoryMenuNode? _selectedMenuNode;

    public DirectoryManagerViewModel()
    {
        RecentEntries =
        [
            new DirectoryEntryItem
            {
                Name = "demo-api",
                Path = "D:/workspace/demo-api",
                LastOpenedAt = DateTime.Now.AddMinutes(-15),
                Kind = DirectoryEntryKind.Recent
            },
            new DirectoryEntryItem
            {
                Name = "backend-service",
                Path = "D:/workspace/backend-service",
                LastOpenedAt = DateTime.Now.AddHours(-2),
                Kind = DirectoryEntryKind.Recent
            }
        ];

        ManagedEntries =
        [
            new DirectoryEntryItem
            {
                Name = "sandbox",
                Path = "D:/workspace/sandbox",
                LastOpenedAt = DateTime.Now.AddDays(-1),
                Kind = DirectoryEntryKind.Managed
            }
        ];

        RevealInExplorerCommand = ReactiveCommand.Create(RevealInExplorer);
        RemoveEntryCommand = ReactiveCommand.Create(RemoveEntry);
        AddToManagedCommand = ReactiveCommand.Create(AddToManaged);
        OpenDirectoryCommand = ReactiveCommand.Create(OpenDirectory);

        ApplyFilter();
    }

    public ObservableCollection<DirectoryEntryItem> RecentEntries { get; }

    public ObservableCollection<DirectoryEntryItem> ManagedEntries { get; }

    public ObservableCollection<DirectoryEntryItem> FilteredRecentEntries { get; } = [];

    public ObservableCollection<DirectoryEntryItem> FilteredManagedEntries { get; } = [];

    public ObservableCollection<DirectoryMenuNode> MenuRoots { get; } = [];

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RevealInExplorerCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RemoveEntryCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddToManagedCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> OpenDirectoryCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            ApplyFilter();
        }
    }

    public DirectoryEntryItem? SelectedEntry
    {
        get => _selectedEntry;
        private set
        {
            this.RaiseAndSetIfChanged(ref _selectedEntry, value);
            this.RaisePropertyChanged(nameof(HasSelection));
            this.RaisePropertyChanged(nameof(CanAddToManaged));
            this.RaisePropertyChanged(nameof(SelectedTypeText));
            this.RaisePropertyChanged(nameof(SelectedName));
            this.RaisePropertyChanged(nameof(SelectedPath));
            this.RaisePropertyChanged(nameof(SelectedLastOpenedText));
        }
    }

    public DirectoryMenuNode? SelectedMenuNode
    {
        get => _selectedMenuNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedMenuNode, value);
            SelectedEntry = value?.Entry;
        }
    }

    public bool HasSelection => SelectedEntry is not null;

    public bool CanAddToManaged => SelectedEntry?.Kind == DirectoryEntryKind.Recent;

    public string SelectedTypeText => SelectedEntry?.Kind == DirectoryEntryKind.Recent ? "最近" : "目录";

    public string SelectedName => SelectedEntry?.Name ?? "-";

    public string SelectedPath => SelectedEntry?.Path ?? "-";

    public string SelectedLastOpenedText =>
        SelectedEntry is null ? "-" : SelectedEntry.LastOpenedAt.ToString("yyyy-MM-dd HH:mm");

    private void RevealInExplorer()
    {
        // M3 先完成状态流，M6 再接入真实平台能力。
    }

    private void OpenDirectory()
    {
        // M4 实现打开目录对话框选择
    }

    private void RemoveEntry()
    {
        if (SelectedEntry is null)
            return;

        if (SelectedEntry.Kind == DirectoryEntryKind.Recent)
            RecentEntries.Remove(SelectedEntry);
        else
            ManagedEntries.Remove(SelectedEntry);

        SelectedEntry = null;
        SelectedMenuNode = null;
        ApplyFilter();
    }

    private void AddToManaged()
    {
        if (SelectedEntry is null || SelectedEntry.Kind != DirectoryEntryKind.Recent)
            return;

        if (ManagedEntries.Any(x => string.Equals(x.Path, SelectedEntry.Path, StringComparison.OrdinalIgnoreCase)))
            return;

        ManagedEntries.Add(new DirectoryEntryItem
        {
            Name = SelectedEntry.Name,
            Path = SelectedEntry.Path,
            LastOpenedAt = DateTime.Now,
            Kind = DirectoryEntryKind.Managed
        });

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();

        FilteredRecentEntries.Clear();
        FilteredManagedEntries.Clear();

        foreach (var entry in RecentEntries.Where(x => MatchEntry(x, query)))
            FilteredRecentEntries.Add(entry);

        foreach (var entry in ManagedEntries.Where(x => MatchEntry(x, query)))
            FilteredManagedEntries.Add(entry);

        RebuildMenuRoots();
    }

    private void RebuildMenuRoots()
    {
        MenuRoots.Clear();

        var recentRoot = new DirectoryMenuNode { Header = "最近" };
        foreach (var entry in FilteredRecentEntries)
        {
            recentRoot.Children.Add(new DirectoryMenuNode
            {
                Header = entry.Name,
                Entry = entry
            });
        }

        var managedRoot = new DirectoryMenuNode { Header = "目录" };
        foreach (var entry in FilteredManagedEntries)
        {
            managedRoot.Children.Add(new DirectoryMenuNode
            {
                Header = entry.Name,
                Entry = entry
            });
        }

        MenuRoots.Add(recentRoot);
        MenuRoots.Add(managedRoot);
    }

    private static bool MatchEntry(DirectoryEntryItem entry, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
               || entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
