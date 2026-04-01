using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kx.Resty.Models;

namespace Kx.Resty.ViewModels;

public partial class MainWindow : ObservableObject
{
    public string Title => "Resty";

    public ObservableCollection<WorkspaceTab> Workspaces { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorkspaces))]
    [NotifyPropertyChangedFor(nameof(HasNoWorkspaces))]
    [NotifyPropertyChangedFor(nameof(HasCollections))]
    [NotifyPropertyChangedFor(nameof(HasNoCollections))]
    [NotifyPropertyChangedFor(nameof(HasActiveRequest))]
    [NotifyPropertyChangedFor(nameof(HasCollectionsButNoRequest))]
    private WorkspaceTab? _activeWorkspace;

    public bool HasWorkspaces             => Workspaces.Count > 0;
    public bool HasNoWorkspaces           => !HasWorkspaces;
    public bool HasCollections            => ActiveWorkspace?.SidePanel.RootNodes.Count > 0;
    public bool HasNoCollections          => HasWorkspaces && !HasCollections;
    public bool HasActiveRequest          => ActiveWorkspace?.ActiveRequest is not null;
    public bool HasCollectionsButNoRequest => HasCollections && !HasActiveRequest;

    private ObservableCollection<CollectionTreeNode>? _trackedRootNodes;

    public MainWindow()
    {
        // Load workspace lists from persisted preferences
        foreach (var e in Preferences.Instance.ManagedWorkspaces)
            ManagedWorkspaces.Add(e);
        foreach (var e in Preferences.Instance.RecentWorkspaces)
            RecentWorkspaces.Add(e);

        ManagedWorkspaces.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoManagedWorkspaces));
        RecentWorkspaces.CollectionChanged  += (_, _) => OnPropertyChanged(nameof(HasNoRecentWorkspaces));

        Workspaces.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasWorkspaces));
            OnPropertyChanged(nameof(HasNoWorkspaces));
            OnPropertyChanged(nameof(HasCollections));
            OnPropertyChanged(nameof(HasNoCollections));
            OnPropertyChanged(nameof(HasCollectionsButNoRequest));
        };
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task OpenDirectory()
    {
        var path = await PickFolder();
        if (string.IsNullOrEmpty(path)) return;
        AddOrUpdateRecent(path, Path.GetFileName(path));
        OpenDirectoryPath(path);
    }

    // ─── Workspace management (Welcome page) ─────────────────────────────────

    public ObservableCollection<WorkspaceEntry> ManagedWorkspaces { get; } = [];
    public ObservableCollection<WorkspaceEntry> RecentWorkspaces  { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEntry))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedEntry))]
    [NotifyPropertyChangedFor(nameof(SelectedEntryIsFromManaged))]
    private WorkspaceEntry? _selectedEntry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedEntryIsFromManaged))]
    private bool _selectedEntryIsFromRecent;

    public bool HasSelectedEntry        => SelectedEntry is not null;
    public bool HasNoSelectedEntry      => SelectedEntry is null;
    public bool SelectedEntryIsFromManaged => HasSelectedEntry && !SelectedEntryIsFromRecent;
    public bool HasNoManagedWorkspaces  => ManagedWorkspaces.Count == 0;
    public bool HasNoRecentWorkspaces   => RecentWorkspaces.Count == 0;

    partial void OnSelectedEntryChanged(WorkspaceEntry? oldValue, WorkspaceEntry? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
    }

    [RelayCommand]
    public void SelectRecentEntry(WorkspaceEntry? entry)
    {
        SelectedEntryIsFromRecent = true;
        SelectedEntry = entry;
    }

    [RelayCommand]
    public void SelectManagedEntry(WorkspaceEntry? entry)
    {
        SelectedEntryIsFromRecent = false;
        SelectedEntry = entry;
    }

    /// <summary>Adds directory to the managed list and immediately opens the workspace.</summary>
    [RelayCommand]
    public async Task AddDirectory()
    {
        var path = await PickFolder();
        if (string.IsNullOrEmpty(path)) return;

        var name = Path.GetFileName(path);

        if (!ManagedWorkspaces.Any(e => e.Path == path))
            ManagedWorkspaces.Add(new WorkspaceEntry { Path = path, Name = name, LastOpenedAt = DateTime.Now });

        AddOrUpdateRecent(path, name);
        OpenDirectoryPath(path);
    }

    /// <summary>Opens a workspace from a WorkspaceEntry (recent or managed).</summary>
    [RelayCommand]
    public void OpenEntry(WorkspaceEntry entry)
    {
        if (entry.IsMissing) return;
        AddOrUpdateRecent(entry.Path, entry.Name);
        OpenDirectoryPath(entry.Path);
    }

    [RelayCommand]
    public void AddToManaged(WorkspaceEntry entry)
    {
        if (ManagedWorkspaces.Any(e => e.Path == entry.Path)) return;
        ManagedWorkspaces.Add(new WorkspaceEntry { Path = entry.Path, Name = entry.Name, LastOpenedAt = entry.LastOpenedAt });
        SyncWorkspacesToPrefs();
    }

    [RelayCommand]
    public void RemoveFromManaged(WorkspaceEntry entry)
    {
        ManagedWorkspaces.Remove(entry);
        if (ReferenceEquals(SelectedEntry, entry)) SelectedEntry = null;
        SyncWorkspacesToPrefs();
    }

    [RelayCommand]
    public void RemoveFromRecent(WorkspaceEntry entry)
    {
        RecentWorkspaces.Remove(entry);
        if (ReferenceEquals(SelectedEntry, entry)) SelectedEntry = null;
        SyncWorkspacesToPrefs();
    }

    [RelayCommand]
    public void RevealInExplorer(WorkspaceEntry entry)
    {
        if (entry.IsMissing) return;
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start("explorer.exe", entry.Path);
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", entry.Path);
            else
                Process.Start("xdg-open", entry.Path);
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    public void SwitchWorkspace(WorkspaceTab ws) => SetActiveWorkspace(ws);

    [RelayCommand]
    public void CloseWorkspace(WorkspaceTab ws)
    {
        var idx = Workspaces.IndexOf(ws);
        ws.Dispose();
        Workspaces.Remove(ws);

        if (Workspaces.Count == 0) { ActiveWorkspace = null; return; }
        SetActiveWorkspace(Workspaces[Math.Clamp(idx, 0, Workspaces.Count - 1)]);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<string?> PickFolder()
    {
        var mainWindow =
            (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        if (mainWindow is null) return null;

        var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Open Directory", AllowMultiple = false });

        if (result.Count == 0) return null;
        return result[0].TryGetLocalPath();
    }

    private void OpenDirectoryPath(string path)
    {
        var existing = Workspaces.FirstOrDefault(w => w.DirectoryPath == path);
        if (existing is not null) { SetActiveWorkspace(existing); return; }

        var ws = new WorkspaceTab { DirectoryPath = path, Name = Path.GetFileName(path) };
        ws.StartScanning();
        Workspaces.Add(ws);
        SetActiveWorkspace(ws);
    }

    private void AddOrUpdateRecent(string path, string name)
    {
        var existing = RecentWorkspaces.FirstOrDefault(e => e.Path == path);
        if (existing is not null)
        {
            existing.LastOpenedAt = DateTime.Now;
        }
        else
        {
            RecentWorkspaces.Insert(0, new WorkspaceEntry { Path = path, Name = name, LastOpenedAt = DateTime.Now });
            while (RecentWorkspaces.Count > 8)
                RecentWorkspaces.RemoveAt(RecentWorkspaces.Count - 1);
        }
        SyncWorkspacesToPrefs();
    }

    private void SyncWorkspacesToPrefs()
    {
        Preferences.Instance.ManagedWorkspaces.Clear();
        Preferences.Instance.ManagedWorkspaces.AddRange(ManagedWorkspaces);
        Preferences.Instance.RecentWorkspaces.Clear();
        Preferences.Instance.RecentWorkspaces.AddRange(RecentWorkspaces);
        Preferences.Instance.Save();
    }

    private void SetActiveWorkspace(WorkspaceTab? ws)
    {
        foreach (var w in Workspaces) w.IsActive = ReferenceEquals(w, ws);
        ActiveWorkspace = ws;
    }

    partial void OnActiveWorkspaceChanged(WorkspaceTab? value)
    {
        if (_trackedRootNodes is not null)
            _trackedRootNodes.CollectionChanged -= OnRootNodesChanged;

        _trackedRootNodes = value?.SidePanel.RootNodes;
        if (_trackedRootNodes is not null)
            _trackedRootNodes.CollectionChanged += OnRootNodesChanged;

        OnPropertyChanged(nameof(HasActiveRequest));
        OnPropertyChanged(nameof(HasCollections));
        OnPropertyChanged(nameof(HasNoCollections));
        OnPropertyChanged(nameof(HasCollectionsButNoRequest));
        // Forward active-request change notifications from the new workspace.
        if (value is not null)
            value.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(WorkspaceTab.ActiveRequest))
                {
                    OnPropertyChanged(nameof(HasActiveRequest));
                    OnPropertyChanged(nameof(HasCollectionsButNoRequest));
                }
            };
    }

    private void OnRootNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasCollections));
        OnPropertyChanged(nameof(HasNoCollections));
        OnPropertyChanged(nameof(HasCollectionsButNoRequest));
    }
}