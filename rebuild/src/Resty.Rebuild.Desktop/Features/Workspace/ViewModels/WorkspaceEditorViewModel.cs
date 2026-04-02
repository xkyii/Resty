using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace Resty.Rebuild.Desktop.Features.Workspace.ViewModels;

public enum WorkspaceUiState
{
    NoWorkspace,
    EmptyWorkspace,
    CollectionBrowsing,
    RequestEditing,
    Sending,
    ResponseReady
}

public sealed class RequestTabItem : ReactiveObject
{
    private string _requestName;
    private string _method;
    private string _url;
    private int _activeRequestTab;
    private int _activeResponseTab;
    private bool _isSending;
    private string _responseCode = "-";
    private string _responseTime = "-";
    private string _responseSize = "-";
    private string _responseBodyContent = "";
    private string _responseHeadersContent = "";
    private string _responseCookiesContent = "";

    public RequestTabItem(
        string collectionName,
        string requestName,
        string method = "GET",
        string url = "",
        Action<RequestTabItem>? onSave = null,
        Action<RequestTabItem>? onRefresh = null,
        Func<RequestTabItem, Task>? onSend = null)
    {
        CollectionName = collectionName;
        _requestName = requestName;
        _method = method;
        _url = url;

        SaveCommand = ReactiveCommand.Create(() => (onSave ?? (_ => { }))(this));
        RefreshCommand = ReactiveCommand.Create(() => (onRefresh ?? (_ => { }))(this));
        SendCommand = ReactiveCommand.CreateFromTask(async () => await (onSend ?? (_ => Task.CompletedTask))(this));
    }

    public static IReadOnlyList<string> HttpMethods { get; } =
        ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"];

    public string CollectionName { get; }

    public string RequestName
    {
        get => _requestName;
        set => this.RaiseAndSetIfChanged(ref _requestName, value);
    }

    public string Method
    {
        get => _method;
        set => this.RaiseAndSetIfChanged(ref _method, value);
    }

    public string Url
    {
        get => _url;
        set => this.RaiseAndSetIfChanged(ref _url, value);
    }

    public int ActiveRequestTab
    {
        get => _activeRequestTab;
        set => this.RaiseAndSetIfChanged(ref _activeRequestTab, value);
    }

    public int ActiveResponseTab
    {
        get => _activeResponseTab;
        set
        {
            this.RaiseAndSetIfChanged(ref _activeResponseTab, value);
            this.RaisePropertyChanged(nameof(ActiveResponseTabHeader));
            this.RaisePropertyChanged(nameof(CurrentResponseContent));
        }
    }

    public bool IsSending
    {
        get => _isSending;
        set => this.RaiseAndSetIfChanged(ref _isSending, value);
    }

    public string ResponseCode
    {
        get => _responseCode;
        set => this.RaiseAndSetIfChanged(ref _responseCode, value);
    }

    public string ResponseTime
    {
        get => _responseTime;
        set => this.RaiseAndSetIfChanged(ref _responseTime, value);
    }

    public string ResponseSize
    {
        get => _responseSize;
        set => this.RaiseAndSetIfChanged(ref _responseSize, value);
    }

    public string ResponseBodyContent
    {
        get => _responseBodyContent;
        set
        {
            this.RaiseAndSetIfChanged(ref _responseBodyContent, value);
            this.RaisePropertyChanged(nameof(CurrentResponseContent));
        }
    }

    public string ResponseHeadersContent
    {
        get => _responseHeadersContent;
        set
        {
            this.RaiseAndSetIfChanged(ref _responseHeadersContent, value);
            this.RaisePropertyChanged(nameof(CurrentResponseContent));
        }
    }

    public string ResponseCookiesContent
    {
        get => _responseCookiesContent;
        set
        {
            this.RaiseAndSetIfChanged(ref _responseCookiesContent, value);
            this.RaisePropertyChanged(nameof(CurrentResponseContent));
        }
    }

    public string ActiveResponseTabHeader => ActiveResponseTab switch
    {
        1 => "Headers",
        2 => "Cookies",
        _ => "Body"
    };

    public string CurrentResponseContent => ActiveResponseTab switch
    {
        1 => ResponseHeadersContent,
        2 => ResponseCookiesContent,
        _ => ResponseBodyContent
    };

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SendCommand { get; }
}

public sealed class WorkspaceEditorViewModel : ReactiveObject
{
    private RequestTabItem? _selectedTab;
    private WorkspaceUiState _currentState;
    private string? _collectionPrompt;

    public WorkspaceEditorViewModel()
    {
        var usersTab = CreateTab("用户服务", "获取用户列表", "GET", "https://api.example.com/users");
        usersTab.ResponseCode = "200";
        usersTab.ResponseTime = "120 ms";
        usersTab.ResponseSize = "3.2 KB";
        usersTab.ResponseBodyContent = "{\n  \"users\": [\n    { \"id\": 1, \"name\": \"Alice\" },\n    { \"id\": 2, \"name\": \"Bob\" }\n  ]\n}";
        usersTab.ResponseHeadersContent = "content-type: application/json\ndate: Tue, 02 Apr 2026 08:00:00 GMT\nserver: kestrel";
        usersTab.ResponseCookiesContent = "session_id=abc123; Path=/; HttpOnly\nlocale=zh-CN; Path=/";

        var loginTab = CreateTab("认证", "用户登录", "POST", "https://api.example.com/login");
        loginTab.ResponseCode = "401";
        loginTab.ResponseTime = "89 ms";
        loginTab.ResponseSize = "512 B";
        loginTab.ResponseBodyContent = "{\n  \"error\": \"invalid credentials\"\n}";
        loginTab.ResponseHeadersContent = "content-type: application/json\nwww-authenticate: Bearer realm=api";
        loginTab.ResponseCookiesContent = "(无 Cookies)";

        OpenTabs =
        [
            usersTab,
            loginTab
        ];

        _selectedTab = OpenTabs[0];

        CurrentState = WorkspaceUiState.NoWorkspace;
    }

    public ObservableCollection<RequestTabItem> OpenTabs { get; }

    public RequestTabItem? SelectedTab
    {
        get => _selectedTab;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTab, value);
            if (value is not null && CurrentState != WorkspaceUiState.Sending)
                CurrentState = WorkspaceUiState.RequestEditing;
        }
    }

    public WorkspaceUiState CurrentState
    {
        get => _currentState;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentState, value);
            this.RaisePropertyChanged(nameof(IsNoWorkspaceState));
            this.RaisePropertyChanged(nameof(IsEmptyWorkspaceState));
            this.RaisePropertyChanged(nameof(IsCollectionBrowsingState));
            this.RaisePropertyChanged(nameof(IsEditorVisible));
            this.RaisePropertyChanged(nameof(IsSendingState));
            this.RaisePropertyChanged(nameof(IsResponseReadyState));
            this.RaisePropertyChanged(nameof(StateTitle));
            this.RaisePropertyChanged(nameof(StateDescription));
        }
    }

    public bool IsNoWorkspaceState => CurrentState == WorkspaceUiState.NoWorkspace;

    public bool IsEmptyWorkspaceState => CurrentState == WorkspaceUiState.EmptyWorkspace;

    public bool IsCollectionBrowsingState => CurrentState == WorkspaceUiState.CollectionBrowsing;

    public bool IsEditorVisible =>
        CurrentState == WorkspaceUiState.RequestEditing
        || CurrentState == WorkspaceUiState.Sending
        || CurrentState == WorkspaceUiState.ResponseReady;

    public bool IsSendingState => CurrentState == WorkspaceUiState.Sending;

    public bool IsResponseReadyState => CurrentState == WorkspaceUiState.ResponseReady;

    public string StateTitle => CurrentState switch
    {
        WorkspaceUiState.NoWorkspace => "未打开工作区",
        WorkspaceUiState.EmptyWorkspace => "空工作区",
        WorkspaceUiState.CollectionBrowsing => "集合浏览",
        WorkspaceUiState.RequestEditing => "请求编辑",
        WorkspaceUiState.Sending => "发送中",
        WorkspaceUiState.ResponseReady => "响应已就绪",
        _ => "工作区"
    };

    public string StateDescription => CurrentState switch
    {
        WorkspaceUiState.NoWorkspace => "请先在标题栏选择一个工作区。",
        WorkspaceUiState.EmptyWorkspace => "当前工作区暂无集合，请新建集合。",
        WorkspaceUiState.CollectionBrowsing => _collectionPrompt ?? "请从左侧选择一个请求开始编辑。",
        WorkspaceUiState.RequestEditing => "可以编辑请求并点击 Send 发送。",
        WorkspaceUiState.Sending => "正在发送请求，请稍候...",
        WorkspaceUiState.ResponseReady => "请求已完成，可查看响应详情。",
        _ => ""
    };

    public void ApplyWorkspaceSelection(string? workspaceName, bool hasCollections)
    {
        _collectionPrompt = null;

        if (string.IsNullOrWhiteSpace(workspaceName) || workspaceName == "未打开工作区")
        {
            CurrentState = WorkspaceUiState.NoWorkspace;
            return;
        }

        if (!hasCollections)
        {
            CurrentState = WorkspaceUiState.EmptyWorkspace;
            return;
        }

        CurrentState = WorkspaceUiState.CollectionBrowsing;
    }

    public void ApplyNavigationSelection(WorkspaceNavNode? node)
    {
        if (CurrentState == WorkspaceUiState.NoWorkspace || CurrentState == WorkspaceUiState.EmptyWorkspace)
            return;

        if (node is null)
        {
            _collectionPrompt = null;
            CurrentState = WorkspaceUiState.CollectionBrowsing;
            return;
        }

        if (node.Kind == WorkspaceNavItemKind.Collection)
        {
            if (node.Children.Count == 0)
            {
                _collectionPrompt = $"集合 \"{node.Header}\" 暂无请求，请新建请求。";
                CurrentState = WorkspaceUiState.CollectionBrowsing;
                this.RaisePropertyChanged(nameof(StateDescription));
                return;
            }

            _collectionPrompt = "请选择集合中的请求开始编辑。";
            CurrentState = WorkspaceUiState.CollectionBrowsing;
            this.RaisePropertyChanged(nameof(StateDescription));
            return;
        }

        if (node.Kind == WorkspaceNavItemKind.Request || node.Kind == WorkspaceNavItemKind.History)
        {
            var matched = OpenTabs.FirstOrDefault(t =>
                string.Equals(t.Method, node.Method, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Url, node.Url, StringComparison.OrdinalIgnoreCase));

            if (matched is null)
            {
                matched = CreateTab(node.Kind == WorkspaceNavItemKind.History ? "历史" : "集合", node.Header, node.Method ?? "GET", node.Url ?? "");
                matched.ResponseCode = "-";
                matched.ResponseTime = "-";
                matched.ResponseSize = "-";
                matched.ResponseBodyContent = "(暂无响应体)";
                matched.ResponseHeadersContent = "(暂无响应头)";
                matched.ResponseCookiesContent = "(暂无 Cookies)";
                OpenTabs.Add(matched);
            }

            SelectedTab = matched;
            _collectionPrompt = null;
            CurrentState = WorkspaceUiState.RequestEditing;
            return;
        }

        CurrentState = WorkspaceUiState.CollectionBrowsing;
    }

    private RequestTabItem CreateTab(string collectionName, string requestName, string method, string url)
        => new(collectionName, requestName, method, url, MarkEditing, MarkEditing, SendRequestAsync);

    private void MarkEditing(RequestTabItem tab)
    {
        SelectedTab = tab;
        tab.IsSending = false;
        CurrentState = WorkspaceUiState.RequestEditing;
    }

    private async Task SendRequestAsync(RequestTabItem tab)
    {
        SelectedTab = tab;
        tab.IsSending = true;

        CurrentState = WorkspaceUiState.Sending;
        await Task.Delay(650);

        tab.ResponseCode = "200";
        tab.ResponseTime = "76 ms";
        tab.ResponseSize = "1.4 KB";
        tab.ResponseBodyContent = "{\n  \"ok\": true,\n  \"url\": \"" + tab.Url + "\"\n}";
        tab.ResponseHeadersContent = "content-type: application/json\nx-powered-by: resty-rebuild";
        tab.ResponseCookiesContent = "trace_id=demo-123; Path=/; HttpOnly";
        tab.IsSending = false;

        CurrentState = WorkspaceUiState.ResponseReady;
    }
}
