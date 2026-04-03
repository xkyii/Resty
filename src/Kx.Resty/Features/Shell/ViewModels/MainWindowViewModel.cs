using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
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
    private string _selectedTheme = "跟随系统";
    private string _selectedLanguage = "简体中文";
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

        ToggleModeCommand = new SimpleCommand(ToggleMode);
        ShowPreferencesCommand = new SimpleCommand(() => { _ = ShowPreferencesAsync(); });
        ShowAboutCommand = new SimpleCommand(ShowAbout);
        QuitCommand = new SimpleCommand(static () =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lt)
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
                try
                {
                    var path = name is not null && _workspacePaths.TryGetValue(name, out var p) ? p : null;
                    WorkspaceNavigation.LoadWorkspace(path);
                    WorkspaceEditor.ApplyWorkspaceSelection(name, WorkspaceNavigation.HasCollections);
                }
                catch
                {
                    WorkspaceNavigation.LoadWorkspace(null);
                    WorkspaceEditor.ApplyWorkspaceSelection("未打开工作区", false);
                }
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
    public ObservableCollection<string> ThemeOptions { get; } = ["浅色", "深色", "跟随系统"];
    public ObservableCollection<string> LanguageOptions { get; } = ["英语", "简体中文"];
    public ICommand ToggleModeCommand { get; }
    public ICommand ShowPreferencesCommand { get; }
    public ICommand ShowAboutCommand { get; }
    public ICommand QuitCommand { get; }

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

    public string SelectedTheme
    {
        get => _selectedTheme;
        set => this.RaiseAndSetIfChanged(ref _selectedTheme, value);
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set => this.RaiseAndSetIfChanged(ref _selectedLanguage, value);
    }

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
        void Apply()
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

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private static void ShowAbout()
    {
        var win = new Window
        {
            Title = "关于 Resty",
            Width = 320,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Resty", FontSize = 20, FontWeight = Avalonia.Media.FontWeight.Bold },
                    new TextBlock { Text = "v2.0 — HTTP 集合管理与调试工具" },
                    new TextBlock { Text = "基于 Avalonia + Semi + Ursa + ReactiveUI 构建。", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new TextBlock { Text = "© 2025 Kx.Dev", Opacity = 0.6 }
                }
            }
        };
        win.Show();
    }

    private async Task ShowPreferencesAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            return;

        var themeCombo = new ComboBox
        {
            ItemsSource = ThemeOptions,
            SelectedItem = SelectedTheme,
            Width = 180
        };

        var languageCombo = new ComboBox
        {
            ItemsSource = LanguageOptions,
            SelectedItem = SelectedLanguage,
            Width = 180
        };

        var dialog = new Window
        {
            Title = "偏好设置",
            Width = 380,
            Height = 240,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var cancelButton = new Button { Content = "取消", MinWidth = 76 };
        var saveButton = new Button { Content = "保存", MinWidth = 76, Classes = { "accent" } };

        cancelButton.Click += (_, _) => dialog.Close(false);
        saveButton.Click += (_, _) => dialog.Close(true);

        var formGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 12,
            Children =
            {
                new TextBlock { Text = "主题", VerticalAlignment = VerticalAlignment.Center },
                themeCombo,
                new TextBlock { Text = "语言", VerticalAlignment = VerticalAlignment.Center },
                languageCombo
            }
        };

        Grid.SetColumn(themeCombo, 1);
        Grid.SetRow(themeCombo, 0);
        Grid.SetColumn(languageCombo, 1);
        Grid.SetRow(languageCombo, 1);
        Grid.SetRow(formGrid.Children[2], 1);

        var root = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 12,
            Children =
            {
                formGrid,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, saveButton }
                }
            }
        };

        Grid.SetRow(root.Children[1], 2);
        dialog.Content = root;

        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (!confirmed)
            return;

        SelectedTheme = (themeCombo.SelectedItem as string) ?? SelectedTheme;
        SelectedLanguage = (languageCombo.SelectedItem as string) ?? SelectedLanguage;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = SelectedTheme switch
        {
            "浅色" => ThemeVariant.Light,
            "深色" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
