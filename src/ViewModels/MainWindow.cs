using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.ViewModels;

public partial class MainWindow : ObservableObject
{
    public string Title => "Kx.Resty";

    public ObservableCollection<WorkspaceTab> Workspaces { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWorkspaces))]
    [NotifyPropertyChangedFor(nameof(HasActiveRequest))]
    [NotifyPropertyChangedFor(nameof(HasWorkspacesButNoRequest))]
    private WorkspaceTab? _activeWorkspace;

    public bool HasWorkspaces             => Workspaces.Count > 0;
    public bool HasActiveRequest          => ActiveWorkspace?.ActiveRequest is not null;
    public bool HasWorkspacesButNoRequest => HasWorkspaces && !HasActiveRequest;

    public MainWindow()
    {
        Workspaces.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasWorkspaces));
            OnPropertyChanged(nameof(HasWorkspacesButNoRequest));
        };
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task OpenDirectory()
    {
        var mainWindow =
            (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        if (mainWindow is null) return;

        var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Open Directory", AllowMultiple = false });

        if (result.Count == 0) return;

        var path = result[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        // Focus the existing workspace if already open.
        var existing = Workspaces.FirstOrDefault(w => w.DirectoryPath == path);
        if (existing is not null) { SetActiveWorkspace(existing); return; }

        var ws = new WorkspaceTab
        {
            DirectoryPath = path,
            Name          = Path.GetFileName(path)
        };
        ws.StartScanning();
        Workspaces.Add(ws);
        SetActiveWorkspace(ws);
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

    private void SetActiveWorkspace(WorkspaceTab? ws)
    {
        foreach (var w in Workspaces) w.IsActive = ReferenceEquals(w, ws);
        ActiveWorkspace = ws;
    }

    partial void OnActiveWorkspaceChanged(WorkspaceTab? value)
    {
        OnPropertyChanged(nameof(HasActiveRequest));
        OnPropertyChanged(nameof(HasWorkspacesButNoRequest));
        // Forward active-request change notifications from the new workspace.
        if (value is not null)
            value.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(WorkspaceTab.ActiveRequest))
                {
                    OnPropertyChanged(nameof(HasActiveRequest));
                    OnPropertyChanged(nameof(HasWorkspacesButNoRequest));
                }
            };
    }
}