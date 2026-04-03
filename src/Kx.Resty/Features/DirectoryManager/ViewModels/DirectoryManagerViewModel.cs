using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Kx.Resty.Domain.Abstractions;
using Kx.Resty.Domain.Directories;
using Kx.Resty.ViewModels;

namespace Kx.Resty.Features.DirectoryManager.ViewModels;

public enum DirectoryEntryKind { Recent, Managed }

public enum DirectoryValidationState { Unknown, Accessible, NotFound, PermissionDenied }

public sealed class DirectoryEntryItem : ReactiveObject
{
    private DirectoryValidationState _validationState = DirectoryValidationState.Unknown;
    private int _httpFileCount = -1;

    public required string Name { get; init; }
    public required string Path { get; init; }
    public DateTime LastOpenedAt { get; set; }
    public DirectoryEntryKind Kind { get; init; }

    public DirectoryValidationState ValidationState
    {
        get => _validationState;
        set
        {
            this.RaiseAndSetIfChanged(ref _validationState, value);
            this.RaisePropertyChanged(nameof(IsAccessible));
            this.RaisePropertyChanged(nameof(StatusText));
            this.RaisePropertyChanged(nameof(StatusColor));
        }
    }

    public int HttpFileCount
    {
        get => _httpFileCount;
        set { this.RaiseAndSetIfChanged(ref _httpFileCount, value); this.RaisePropertyChanged(nameof(HttpFileCountText)); }
    }

    public bool IsAccessible => ValidationState == DirectoryValidationState.Accessible;

    public string StatusText => ValidationState switch
    {
        DirectoryValidationState.Unknown => "检测中…",
        DirectoryValidationState.Accessible => "可访问",
        DirectoryValidationState.NotFound => "路径不存在",
        DirectoryValidationState.PermissionDenied => "无读取权限",
        _ => ""
    };

    public string StatusColor => ValidationState switch
    {
        DirectoryValidationState.Accessible => "#52C41A",
        DirectoryValidationState.NotFound => "#FF4D4F",
        DirectoryValidationState.PermissionDenied => "#FA8C16",
        _ => "#888888"
    };

    public string HttpFileCountText => HttpFileCount < 0 ? "—" : $"{HttpFileCount} 个 .http 文件";
}

public sealed class DirectoryMenuNode
{
    public required string Header { get; init; }
    public ObservableCollection<DirectoryMenuNode> Children { get; } = [];
    public DirectoryEntryItem? Entry { get; init; }
}

public sealed class DirectoryManagerViewModel : ViewModelBase
{
    private readonly IDirectoryStore? _store;
    private string _searchText = string.Empty;
    private DirectoryEntryItem? _selectedEntry;
    private DirectoryMenuNode? _selectedMenuNode;
    private string _errorBanner = string.Empty;

    public DirectoryManagerViewModel(IDirectoryStore? store = null)
    {
        _store = store;
        RecentEntries = [];
        ManagedEntries = [];

        RevealInExplorerCommand = new SimpleCommand(RevealInExplorer);
        RemoveEntryCommand = new SimpleCommand(RemoveEntry);
        AddToManagedCommand = new SimpleCommand(AddToManaged);
        OpenInWorkspaceCommand = new SimpleCommand(OpenSelectedToWorkspace);

        _ = LoadFromStoreAsync();
    }

    public ObservableCollection<DirectoryEntryItem> RecentEntries { get; }
    public ObservableCollection<DirectoryEntryItem> ManagedEntries { get; }
    public ObservableCollection<DirectoryMenuNode> MenuRoots { get; } = [];

    public ICommand RevealInExplorerCommand { get; }
    public ICommand RemoveEntryCommand { get; }
    public ICommand AddToManagedCommand { get; }
    public ICommand OpenInWorkspaceCommand { get; }

    public Action<DirectoryEntryItem>? OpenInWorkspaceRequested { get; set; }

    public string SearchText
    {
        get => _searchText;
        set { this.RaiseAndSetIfChanged(ref _searchText, value); ApplyFilter(); }
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
            ErrorBanner = string.Empty;
        }
    }

    public string ErrorBanner
    {
        get => _errorBanner;
        private set { this.RaiseAndSetIfChanged(ref _errorBanner, value); this.RaisePropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorBanner);
    public bool HasSelection => SelectedEntry is not null;
    public bool HasOpenableProjects => RecentEntries.Count > 0 || ManagedEntries.Count > 0;
    public bool CanAddToManaged => SelectedEntry?.Kind == DirectoryEntryKind.Recent;

    public string SelectedTypeText => SelectedEntry?.Kind == DirectoryEntryKind.Recent ? "最近" : "收藏";
    public string SelectedName => SelectedEntry?.Name ?? "—";
    public string SelectedPath => SelectedEntry?.Path ?? "—";
    public string SelectedLastOpenedText =>
        SelectedEntry is null ? "—" : SelectedEntry.LastOpenedAt.ToString("yyyy-MM-dd HH:mm");

    public void OpenSelectedInWorkspace() => OpenSelectedToWorkspace();

    public async Task OpenPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var existing = RecentEntries.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
        DirectoryEntryItem entry;
        if (existing is not null)
        {
            existing.LastOpenedAt = DateTime.Now;
            entry = existing;
        }
        else
        {
            var name = System.IO.Path.GetFileName(path.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(name)) name = path;
            entry = new DirectoryEntryItem { Name = name, Path = path, LastOpenedAt = DateTime.Now, Kind = DirectoryEntryKind.Recent };
            RecentEntries.Insert(0, entry);
            if (RecentEntries.Count > 20)
                RecentEntries.RemoveAt(RecentEntries.Count - 1);
        }

        SelectedEntry = entry;
        await ValidateEntryAsync(entry);
        TryInvokeOpen(entry);
    }

    private void OpenSelectedToWorkspace()
    {
        if (SelectedEntry is null) return;
        ErrorBanner = string.Empty;
        try
        {
            SelectedEntry.ValidationState = Validate(SelectedEntry.Path);
            TryInvokeOpen(SelectedEntry);
        }
        catch (Exception ex)
        {
            ErrorBanner = $"⚠ 打开失败：{ex.Message}";
        }
    }

    private void TryInvokeOpen(DirectoryEntryItem entry)
    {
        if (entry.ValidationState == DirectoryValidationState.NotFound)
        {
            ErrorBanner = $"⚠ 路径不存在：{entry.Path}";
            return;
        }
        if (entry.ValidationState == DirectoryValidationState.PermissionDenied)
        {
            ErrorBanner = $"⚠ 无读取权限：{entry.Path}";
            return;
        }

        entry.LastOpenedAt = DateTime.Now;
        EnsureInRecent(entry);
        _ = SaveToStoreAsync();
        OpenInWorkspaceRequested?.Invoke(entry);
    }

    private void RemoveEntry()
    {
        if (SelectedEntry is null) return;
        if (SelectedEntry.Kind == DirectoryEntryKind.Recent)
            RecentEntries.Remove(SelectedEntry);
        else
            ManagedEntries.Remove(SelectedEntry);

        SelectedEntry = null;
        SelectedMenuNode = null;
        ApplyFilter();
        RaiseOpenableChanged();
        _ = SaveToStoreAsync();
    }

    private void AddToManaged()
    {
        if (SelectedEntry is null || SelectedEntry.Kind != DirectoryEntryKind.Recent) return;
        if (ManagedEntries.Any(x => string.Equals(x.Path, SelectedEntry.Path, StringComparison.OrdinalIgnoreCase)))
            return;

        ManagedEntries.Add(new DirectoryEntryItem
        {
            Name = SelectedEntry.Name,
            Path = SelectedEntry.Path,
            LastOpenedAt = DateTime.Now,
            Kind = DirectoryEntryKind.Managed,
            ValidationState = SelectedEntry.ValidationState,
            HttpFileCount = SelectedEntry.HttpFileCount
        });

        ApplyFilter();
        RaiseOpenableChanged();
        _ = SaveToStoreAsync();
    }

    private void RevealInExplorer()
    {
        if (SelectedEntry is null) return;
        var path = SelectedEntry.Path;
        if (!Directory.Exists(path)) return;

        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", $"-R \"{path}\"");
            else
                System.Diagnostics.Process.Start("xdg-open", $"\"{System.IO.Path.GetDirectoryName(path)}\"");
        }
        catch { }
    }

    private static DirectoryValidationState Validate(string path)
    {
        if (!Directory.Exists(path)) return DirectoryValidationState.NotFound;
        try { Directory.GetFiles(path); return DirectoryValidationState.Accessible; }
        catch (UnauthorizedAccessException) { return DirectoryValidationState.PermissionDenied; }
    }

    private static int CountHttpFiles(string path)
    {
        try { return Directory.GetFiles(path, "*.http", SearchOption.AllDirectories).Length; }
        catch { return 0; }
    }

    private async Task ValidateEntryAsync(DirectoryEntryItem entry)
    {
        var state = await Task.Run(() => Validate(entry.Path)).ConfigureAwait(false);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => entry.ValidationState = state);

        if (state == DirectoryValidationState.Accessible)
        {
            var count = await Task.Run(() => CountHttpFiles(entry.Path)).ConfigureAwait(false);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => entry.HttpFileCount = count);
        }
    }

    private async Task LoadFromStoreAsync()
    {
        if (_store is null)
        {
            LoadDemoData();
            ApplyFilter();
            return;
        }

        var data = await _store.LoadAsync().ConfigureAwait(false);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            RecentEntries.Clear();
            ManagedEntries.Clear();

            foreach (var r in data.Recent)
            {
                var name = System.IO.Path.GetFileName(r.Path.TrimEnd('/', '\\'));
                if (string.IsNullOrEmpty(name)) name = r.Path;
                RecentEntries.Add(new DirectoryEntryItem { Name = name, Path = r.Path, LastOpenedAt = r.LastOpenedAt, Kind = DirectoryEntryKind.Recent });
            }

            foreach (var m in data.Managed)
            {
                var name = System.IO.Path.GetFileName(m.Path.TrimEnd('/', '\\'));
                if (string.IsNullOrEmpty(name)) name = m.Path;
                ManagedEntries.Add(new DirectoryEntryItem { Name = name, Path = m.Path, LastOpenedAt = m.AddedAt, Kind = DirectoryEntryKind.Managed });
            }

            ApplyFilter();
            RaiseOpenableChanged();
        });

        _ = ValidateAllAsync();
    }

    private async Task ValidateAllAsync()
    {
        foreach (var entry in RecentEntries.Concat(ManagedEntries).ToList())
            await ValidateEntryAsync(entry).ConfigureAwait(false);
    }

    private async Task SaveToStoreAsync()
    {
        if (_store is null) return;
        var data = new DirectoriesData(
            RecentEntries.Select(r => new RecentDirectoryRecord(r.Path, r.LastOpenedAt)).ToList(),
            ManagedEntries.Select(m => new ManagedDirectoryRecord(m.Path, m.LastOpenedAt)).ToList()
        );
        await _store.SaveAsync(data).ConfigureAwait(false);
    }

    private void ApplyFilter() => RebuildMenuRoots();

    private void RebuildMenuRoots()
    {
        MenuRoots.Clear();
        var query = SearchText.Trim();

        var recentRoot = new DirectoryMenuNode { Header = "最近" };
        foreach (var entry in RecentEntries.Where(x => MatchEntry(x, query)))
            recentRoot.Children.Add(new DirectoryMenuNode { Header = entry.Name, Entry = entry });

        var managedRoot = new DirectoryMenuNode { Header = "收藏" };
        foreach (var entry in ManagedEntries.Where(x => MatchEntry(x, query)))
            managedRoot.Children.Add(new DirectoryMenuNode { Header = entry.Name, Entry = entry });

        MenuRoots.Add(recentRoot);
        MenuRoots.Add(managedRoot);
    }

    private static bool MatchEntry(DirectoryEntryItem entry, string query) =>
        string.IsNullOrWhiteSpace(query)
        || entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void EnsureInRecent(DirectoryEntryItem entry)
    {
        var existing = RecentEntries.FirstOrDefault(x => string.Equals(x.Path, entry.Path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.LastOpenedAt = entry.LastOpenedAt;
            RecentEntries.Move(RecentEntries.IndexOf(existing), 0);
        }
        else
        {
            RecentEntries.Insert(0, new DirectoryEntryItem
            {
                Name = entry.Name,
                Path = entry.Path,
                LastOpenedAt = entry.LastOpenedAt,
                Kind = DirectoryEntryKind.Recent,
                ValidationState = entry.ValidationState,
                HttpFileCount = entry.HttpFileCount
            });
            if (RecentEntries.Count > 20)
                RecentEntries.RemoveAt(RecentEntries.Count - 1);
        }
        ApplyFilter();
    }

    private void RaiseOpenableChanged() => this.RaisePropertyChanged(nameof(HasOpenableProjects));

    private void LoadDemoData()
    {
        RecentEntries.Add(new DirectoryEntryItem { Name = "demo-api", Path = "D:/workspace/demo-api", LastOpenedAt = DateTime.Now.AddMinutes(-15), Kind = DirectoryEntryKind.Recent });
        RecentEntries.Add(new DirectoryEntryItem { Name = "backend-service", Path = "D:/workspace/backend-service", LastOpenedAt = DateTime.Now.AddHours(-2), Kind = DirectoryEntryKind.Recent });
        ManagedEntries.Add(new DirectoryEntryItem { Name = "sandbox", Path = "D:/workspace/sandbox", LastOpenedAt = DateTime.Now.AddDays(-1), Kind = DirectoryEntryKind.Managed });
    }
}
