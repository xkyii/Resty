using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Kx.Resty.Domain.Abstractions;
using Kx.Resty.Domain.Http;
using Kx.Resty.Features.Workspace.Services;

namespace Kx.Resty.Features.Workspace.ViewModels;

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
    private string _headersText;
    private string _bodyText;
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
        string method,
        string url,
        string headersText,
        string bodyText,
        Action<RequestTabItem>? onSave = null,
        Action<RequestTabItem>? onRefresh = null,
        Func<RequestTabItem, Task>? onSend = null)
    {
        CollectionName = collectionName;
        _requestName = requestName;
        _method = method;
        _url = url;
        _headersText = headersText;
        _bodyText = bodyText;

        SaveCommand = ReactiveCommand.Create(() => (onSave ?? (_ => { }))(this));
        RefreshCommand = ReactiveCommand.Create(() => (onRefresh ?? (_ => { }))(this));
        SendCommand = ReactiveCommand.CreateFromTask(async () => await (onSend ?? (_ => Task.CompletedTask))(this));
    }

    public static IReadOnlyList<string> HttpMethods { get; } =
        ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"];

    public string CollectionName { get; }
    public string? SourceFilePath { get; init; }
    public int SourceSegmentIndex { get; init; } = -1;
    public bool NoLog { get; init; }

    public string RequestName { get => _requestName; set => this.RaiseAndSetIfChanged(ref _requestName, value); }
    public string Method { get => _method; set => this.RaiseAndSetIfChanged(ref _method, value); }
    public string Url { get => _url; set => this.RaiseAndSetIfChanged(ref _url, value); }
    public string HeadersText { get => _headersText; set => this.RaiseAndSetIfChanged(ref _headersText, value); }
    public string BodyText { get => _bodyText; set => this.RaiseAndSetIfChanged(ref _bodyText, value); }
    public int ActiveRequestTab { get => _activeRequestTab; set => this.RaiseAndSetIfChanged(ref _activeRequestTab, value); }
    public int ActiveResponseTab
    {
        get => _activeResponseTab;
        set { this.RaiseAndSetIfChanged(ref _activeResponseTab, value); this.RaisePropertyChanged(nameof(CurrentResponseContent)); }
    }
    public bool IsSending { get => _isSending; set => this.RaiseAndSetIfChanged(ref _isSending, value); }
    public string ResponseCode { get => _responseCode; set => this.RaiseAndSetIfChanged(ref _responseCode, value); }
    public string ResponseTime { get => _responseTime; set => this.RaiseAndSetIfChanged(ref _responseTime, value); }
    public string ResponseSize { get => _responseSize; set => this.RaiseAndSetIfChanged(ref _responseSize, value); }
    public string ResponseBodyContent
    {
        get => _responseBodyContent;
        set { this.RaiseAndSetIfChanged(ref _responseBodyContent, value); this.RaisePropertyChanged(nameof(CurrentResponseContent)); }
    }
    public string ResponseHeadersContent
    {
        get => _responseHeadersContent;
        set { this.RaiseAndSetIfChanged(ref _responseHeadersContent, value); this.RaisePropertyChanged(nameof(CurrentResponseContent)); }
    }
    public string ResponseCookiesContent
    {
        get => _responseCookiesContent;
        set { this.RaiseAndSetIfChanged(ref _responseCookiesContent, value); this.RaisePropertyChanged(nameof(CurrentResponseContent)); }
    }

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
    private readonly IHttpRequestExecutor? _requestExecutor;
    private readonly WorkspaceNavigationViewModel _navigation;
    private RequestTabItem? _selectedTab;
    private WorkspaceUiState _currentState;
    private string? _collectionPrompt;

    public WorkspaceEditorViewModel(WorkspaceNavigationViewModel navigation, IHttpRequestExecutor? requestExecutor = null)
    {
        _navigation = navigation;
        _requestExecutor = requestExecutor;
        OpenTabs = [];
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
        WorkspaceUiState.ResponseReady => "请求已完成，可查看响应情况。",
        _ => ""
    };

    public event Action<string, string, bool>? RequestSent;

    public void ApplyWorkspaceSelection(string? workspaceName, bool hasCollections)
    {
        _collectionPrompt = null;
        if (string.IsNullOrWhiteSpace(workspaceName) || workspaceName == "未打开工作区")
        {
            CurrentState = WorkspaceUiState.NoWorkspace;
            return;
        }
        CurrentState = hasCollections ? WorkspaceUiState.CollectionBrowsing : WorkspaceUiState.EmptyWorkspace;
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
            _collectionPrompt = node.Children.Count == 0
                ? $"集合 \"{node.Header}\" 暂无请求，请新建请求。"
                : "请选择集合中的请求开始编辑。";
            CurrentState = WorkspaceUiState.CollectionBrowsing;
            this.RaisePropertyChanged(nameof(StateDescription));
            return;
        }

        if (node.Kind == WorkspaceNavItemKind.Request || node.Kind == WorkspaceNavItemKind.History)
        {
            var matched = OpenTabs.FirstOrDefault(t =>
                string.Equals(t.SourceFilePath, node.FilePath, StringComparison.OrdinalIgnoreCase)
                && t.SourceSegmentIndex == node.SegmentIndex
                && string.Equals(t.Method, node.Method, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Url, node.Url, StringComparison.OrdinalIgnoreCase));

            if (matched is null)
            {
                matched = CreateTab(
                    collectionName: node.Kind == WorkspaceNavItemKind.History ? "历史" : (node.RelativePath ?? "集合"),
                    requestName: node.Header,
                    method: node.Method ?? "GET",
                    url: node.Url ?? string.Empty,
                    headersText: node.HeadersText ?? string.Empty,
                    bodyText: node.BodyText ?? string.Empty,
                    sourceFilePath: node.FilePath,
                    sourceSegmentIndex: node.SegmentIndex,
                    noLog: node.NoLog);

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

    private RequestTabItem CreateTab(
        string collectionName, string requestName, string method, string url,
        string headersText, string bodyText, string? sourceFilePath, int sourceSegmentIndex, bool noLog)
        => new(collectionName, requestName, method, url, headersText, bodyText,
            SaveRequestChanges, MarkEditing, SendRequestAsync)
        {
            SourceFilePath = sourceFilePath,
            SourceSegmentIndex = sourceSegmentIndex,
            NoLog = noLog
        };

    private void SaveRequestChanges(RequestTabItem tab)
    {
        if (string.IsNullOrWhiteSpace(tab.SourceFilePath) || tab.SourceSegmentIndex < 0)
            return;

        var node = new WorkspaceNavNode
        {
            Header = tab.RequestName,
            Kind = WorkspaceNavItemKind.Request,
            Method = tab.Method,
            Url = tab.Url,
            FilePath = tab.SourceFilePath,
            SegmentIndex = tab.SourceSegmentIndex,
            HeadersText = tab.HeadersText,
            BodyText = tab.BodyText,
            NoLog = tab.NoLog
        };

        _navigation.SaveRequestChanges(node, tab.RequestName, tab.Method, tab.Url, tab.HeadersText, tab.BodyText);
        MarkEditing(tab);
    }

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

        try
        {
            var fileVars = _navigation.GetFileVariables(tab.SourceFilePath);
            var envVars = WorkspaceVariableResolver.LoadEnvironmentVariables(_navigation.WorkspaceRootPath ?? string.Empty);
            var resolvedUrl = WorkspaceVariableResolver.Resolve(tab.Url, fileVars, envVars);
            var resolvedHeaders = WorkspaceVariableResolver.Resolve(tab.HeadersText, fileVars, envVars);
            var resolvedBody = WorkspaceVariableResolver.Resolve(tab.BodyText, fileVars, envVars);

            if (_requestExecutor is null)
            {
                await Task.Delay(300);
                tab.ResponseCode = "200";
                tab.ResponseTime = "50 ms";
                tab.ResponseSize = "1.2 KB";
                tab.ResponseBodyContent = $"{{\n  \"ok\": true,\n  \"url\": \"{resolvedUrl}\"\n}}";
                tab.ResponseHeadersContent = "content-type: application/json\nx-powered-by: resty";
                tab.ResponseCookiesContent = "trace_id=demo-123; Path=/; HttpOnly";
            }
            else
            {
                var req = new HttpRequestData
                {
                    Method = tab.Method,
                    Url = resolvedUrl,
                    Headers = ParseHeaders(resolvedHeaders),
                    Body = resolvedBody
                };
                var response = await _requestExecutor.SendAsync(req);

                tab.ResponseCode = response.StatusCode.ToString();
                tab.ResponseTime = $"{response.ElapsedMilliseconds} ms";
                tab.ResponseSize = FormatSize(response.SizeBytes);
                tab.ResponseBodyContent = response.BodyContent;
                tab.ResponseHeadersContent = response.HeadersContent;
                tab.ResponseCookiesContent = response.CookiesContent;
            }

            RequestSent?.Invoke(tab.Method, resolvedUrl, !tab.NoLog);
            CurrentState = WorkspaceUiState.ResponseReady;
        }
        catch (Exception ex)
        {
            tab.ResponseCode = "ERR";
            tab.ResponseTime = "-";
            tab.ResponseSize = "-";
            tab.ResponseBodyContent = ex.Message;
            tab.ResponseHeadersContent = "(请求执行失败)";
            tab.ResponseCookiesContent = "(无 Cookies)";
            CurrentState = WorkspaceUiState.ResponseReady;
        }

        tab.IsSending = false;
    }

    private static Dictionary<string, string> ParseHeaders(string headersText)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(headersText)) return dict;

        foreach (var line in headersText.Replace("\r\n", "\n").Split('\n'))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var k = line[..idx].Trim();
            var v = line[(idx + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(k)) dict[k] = v;
        }
        return dict;
    }

    private static string FormatSize(long sizeBytes)
    {
        if (sizeBytes < 1024) return $"{sizeBytes} B";
        var kb = sizeBytes / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB";
        return $"{kb / 1024.0:F1} MB";
    }
}
