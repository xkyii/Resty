using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;

namespace Resty.Rebuild.Desktop.Features.Workspace.ViewModels;

public sealed class RequestTabItem : ReactiveObject
{
    private string _requestName;
    private string _method;
    private string _url;
    private int _activeRequestTab;
    private int _activeResponseTab;

    public RequestTabItem(string collectionName, string requestName, string method = "GET", string url = "")
    {
        CollectionName = collectionName;
        _requestName = requestName;
        _method = method;
        _url = url;
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
        set => this.RaiseAndSetIfChanged(ref _activeResponseTab, value);
    }

    public string ResponseCode { get; set; } = "-";

    public string ResponseTime { get; set; } = "-";

    public string ResponseSize { get; set; } = "-";
}

public sealed class WorkspaceEditorViewModel : ReactiveObject
{
    private RequestTabItem? _selectedTab;

    public WorkspaceEditorViewModel()
    {
        OpenTabs =
        [
            new RequestTabItem("用户服务", "获取用户列表", "GET", "https://api.example.com/users")
            {
                ResponseCode = "200",
                ResponseTime = "120 ms",
                ResponseSize = "3.2 KB"
            },
            new RequestTabItem("认证", "用户登录", "POST", "https://api.example.com/login")
        ];

        _selectedTab = OpenTabs[0];

        SendCommand = ReactiveCommand.Create(() => { });
        SaveCommand = ReactiveCommand.Create(() => { });
        RefreshCommand = ReactiveCommand.Create(() => { });
    }

    public ObservableCollection<RequestTabItem> OpenTabs { get; }

    public RequestTabItem? SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SendCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshCommand { get; }
}
