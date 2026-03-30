using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kx.Resty.Models;

namespace Kx.Resty.ViewModels;

public partial class WorkspaceTab : ObservableObject, IDisposable
{
    public string DirectoryPath { get; init; } = string.Empty;
    public string Name          { get; init; } = string.Empty;

    public CollectionPanel SidePanel { get; } = new();

    public ObservableCollection<RequestTabItem> OpenRequests { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenRequests))]
    private RequestTabItem? _activeRequest;

    [ObservableProperty] private bool _isActive;

    public bool HasOpenRequests => OpenRequests.Count > 0;

    private Commands.WorkspaceScanner? _scanner;

    public WorkspaceTab()
    {
        OpenRequests.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasOpenRequests));
        SidePanel.OnRequestOpen = OpenRequest;
    }

    /// <summary>Starts scanning the directory and watching for changes.</summary>
    public void StartScanning()
    {
        SidePanel.WorkspacePath = DirectoryPath;
        _scanner = new Commands.WorkspaceScanner(DirectoryPath, SidePanel);
        _scanner.Start();
    }

    // ─── Open / close requests ────────────────────────────────────────────────

    /// <summary>Opens (or focuses) a request entry in the sub-tab bar.</summary>
    public void OpenRequest(HttpRequestEntry entry, HttpCollection collection)
    {
        // Re-use the existing tab if already open.
        var existing = OpenRequests.FirstOrDefault(t => ReferenceEquals(t.Content?.Entry, entry));
        if (existing is not null)
        {
            SetActiveRequest(existing);
            return;
        }

        var request = new RequestTab(entry, collection);
        var tab = new RequestTabItem
        {
            Content = request
        };
        WireTabState(tab, request);
        OpenRequests.Add(tab);
        SetActiveRequest(tab);
    }

    [RelayCommand]
    public void NewRequest()
    {
        var collection = SidePanel.SelectedCollection;
        HttpRequestEntry? entry = null;

        if (collection is not null)
        {
            entry = new HttpRequestEntry();
            collection.Requests.Add(entry);
            Commands.HttpFileWriter.Write(collection);
        }

        var request = entry is not null
            ? new RequestTab(entry, collection!)
            : new RequestTab();

        var tab = new RequestTabItem
        {
            Content = request
        };
        WireTabState(tab, request);
        OpenRequests.Add(tab);
        SetActiveRequest(tab);
    }

    [RelayCommand]
    public void SwitchRequest(RequestTabItem tab) => SetActiveRequest(tab);

    [RelayCommand]
    public void CloseRequest(RequestTabItem tab)
    {
        var idx = OpenRequests.IndexOf(tab);
        OpenRequests.Remove(tab);

        if (OpenRequests.Count == 0) { ActiveRequest = null; return; }
        SetActiveRequest(OpenRequests[Math.Clamp(idx, 0, OpenRequests.Count - 1)]);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetActiveRequest(RequestTabItem? tab)
    {
        foreach (var t in OpenRequests)
            t.IsActive = ReferenceEquals(t, tab);

        tab?.Content?.ReloadFromEntry();
        SidePanel.SyncSelectionFromRequest(tab?.Content?.Entry, tab?.Content?.Collection);
        ActiveRequest = tab;
    }

    private static void WireTabState(RequestTabItem tab, RequestTab request)
    {
        void RefreshTitle() => tab.Title = request.TabTitle;
        request.TabStateChanged += RefreshTitle;
        RefreshTitle();
    }

    partial void OnActiveRequestChanged(RequestTabItem? value)
        => OnPropertyChanged(nameof(HasOpenRequests));

    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose() => _scanner?.Dispose();
}
