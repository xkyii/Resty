using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using ReactiveUI;
using Kx.Resty.Domain.Abstractions;
using Kx.Resty.ViewModels;
using Kx.Resty.Features.DirectoryManager.ViewModels;
using Kx.Resty.Features.Workspace.ViewModels;

namespace Kx.Resty.Features.Shell.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private bool _isDirectoryManagerMode = true;
    private string? _selectedWorkspaceName;
    private readonly Dictionary<string, string?> _workspacePaths = new(StringComparer.OrdinalIgnoreCase);

    public MainWindowViewModel(IHttpRequestExecutor? requestExecutor = null, IDirectoryStore? directoryStore = null)
    {
        DirectoryManager = new DirectoryManagerViewModel(directoryStore);
        WorkspaceNavigation = new WorkspaceNavigationViewModel();
        WorkspaceEditor = new WorkspaceEditorViewModel(WorkspaceNavigation, requestExecutor);

        const string noWorkspace = "未打开工作区";
        OpenWorkspaces = [noWorkspace];
        _workspacePaths[noWorkspace] = null;
        _selectedWorkspaceName = noWorkspace;

        ToggleModeCommand = ReactiveCommand.Create(ToggleMode);
        ShowAboutCommand = ReactiveCommand.Create(ShowAbout);
        QuitCommand = ReactiveCommand.Create(static () =>
        {
            if (Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lt)
                lt.Shutdown();
        });

        DirectoryManager.OpenInWorkspaceRequested = OpenDirectoryEntryInWorkspace;

        DirectoryManager.WhenAnyValue(x => x.HasOpenableProjects)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(CanSwitchToWorkspace));
                this.RaisePropertyChanged(nameof(IsModeSwitchEnabled));
                this.RaisePropertyChanged(nameof(ShouldShowOpenFolderHint));
            });

        this.WhenAnyValue(x => x.SelectedWorkspaceName)
            .Subscribe(name =>
            {
                var path = name is not null && _workspacePaths.TryGetValue(name, out var p) ? p : null;
                WorkspaceNavigation.LoadWorkspace(path);
                WorkspaceEditor.ApplyWorkspaceSelection(name, WorkspaceNavigation.HasCollections);
            });

        WorkspaceNavigation.WhenAnyValue(x => x.SelectedNode)
            .Subscribe(node => WorkspaceEditor.ApplyNavigationSelection(node));

        WorkspaceNavigation.WhenAnyValue(x => x.HasCollections)
            .Subscribe(hasCollections =>
                WorkspaceEditor.ApplyWorkspaceSelection(SelectedWorkspaceName, hasCollections));

        WorkspaceEditor.RequestSent += (method, url, shouldLog) =>
        {
            if (shouldLog)
                WorkspaceNavigation.AddHistoryEntry(method, url, persist: true);
        };

        WorkspaceNavigation.LoadWorkspace(null);
        WorkspaceEditor.ApplyWorkspaceSelection(_selectedWorkspaceName, WorkspaceNavigation.HasCollections);
    }

    public DirectoryManagerViewModel DirectoryManager { get; }
    public WorkspaceNavigationViewModel WorkspaceNavigation { get; }
    public WorkspaceEditorViewModel WorkspaceEditor { get; }
    public ObservableCollection<string> OpenWorkspaces { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ToggleModeCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ShowAboutCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> QuitCommand { get; }

    public bool CanSwitchToWorkspace => DirectoryManager.HasOpenableProjects;
    public bool IsModeSwitchEnabled => !IsDirectoryManagerMode || CanSwitchToWorkspace;
    public bool ShouldShowOpenFolderHint => IsDirectoryManagerMode && !CanSwitchToWorkspace;
    public string WorkspaceSwitchHint => ShouldShowOpenFolderHint ? "请先打开文件夹后再切换到工作区" : string.Empty;

    public bool IsDirectoryManagerMode
    {
        get => _isDirectoryManagerMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isDirectoryManagerMode, value);
            this.RaisePropertyChanged(nameof(IsWorkspaceMode));
            this.RaisePropertyChanged(nameof(ModeTitle));
            this.RaisePropertyChanged(nameof(SearchPlaceholder));
        }
    }

    public bool IsWorkspaceMode => !IsDirectoryManagerMode;
    public string ModeTitle => IsDirectoryManagerMode ? "目录管理" : "工作区";
    public string SearchPlaceholder => IsDirectoryManagerMode ? "搜索\"最近\"或\"目录\"" : "搜索\"集合\"或\"历史\"";

    public string? SelectedWorkspaceName
    {
        get => _selectedWorkspaceName;
        set => this.RaiseAndSetIfChanged(ref _selectedWorkspaceName, value);
    }

    private void ToggleMode()
    {
        if (IsDirectoryManagerMode && !CanSwitchToWorkspace)
            return;

        IsDirectoryManagerMode = !IsDirectoryManagerMode;
        this.RaisePropertyChanged(nameof(SearchPlaceholder));
        this.RaisePropertyChanged(nameof(IsModeSwitchEnabled));
        this.RaisePropertyChanged(nameof(ShouldShowOpenFolderHint));
    }

    private void OpenDirectoryEntryInWorkspace(DirectoryEntryItem entry)
    {
        if (!OpenWorkspaces.Contains(entry.Name))
            OpenWorkspaces.Add(entry.Name);
        _workspacePaths[entry.Name] = entry.Path;

        SelectedWorkspaceName = entry.Name;
        IsDirectoryManagerMode = false;
        this.RaisePropertyChanged(nameof(SearchPlaceholder));
        this.RaisePropertyChanged(nameof(IsModeSwitchEnabled));
        this.RaisePropertyChanged(nameof(ShouldShowOpenFolderHint));
    }

    private static void ShowAbout()
    {
        var win = new Avalonia.Controls.Window
        {
            Title = "关于 Resty",
            Width = 320,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            Content = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 8,
                Children =
                {
                    new Avalonia.Controls.TextBlock { Text = "Resty", FontSize = 20, FontWeight = Avalonia.Media.FontWeight.Bold },
                    new Avalonia.Controls.TextBlock { Text = "v2.0 — HTTP 集合管理与调试工具" },
                    new Avalonia.Controls.TextBlock { Text = "基于 Avalonia + Semi + Ursa + ReactiveUI 构建。", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Avalonia.Controls.TextBlock { Text = "© 2025 Kx.Dev", Opacity = 0.6 }
                }
            }
        };
        win.Show();
    }
}
