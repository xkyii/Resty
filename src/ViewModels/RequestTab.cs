using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.ViewModels;

/// <summary>Represents an HTTP method option in the ComboBox.</summary>
public class HttpMethodOption
{
    public string Name { get; init; } = "";
    public string BrushKey { get; init; } = "";
}

public partial class RequestTab : ObservableObject
{
    public static readonly HttpMethodOption[] Methods =
    [
        new() { Name = "GET",    BrushKey = "Brush.Method.GET"    },
        new() { Name = "POST",   BrushKey = "Brush.Method.POST"   },
        new() { Name = "PUT",    BrushKey = "Brush.Method.PUT"    },
        new() { Name = "DELETE", BrushKey = "Brush.Method.DELETE" },
        new() { Name = "PATCH",  BrushKey = "Brush.Method.PATCH"  },
        new() { Name = "HEAD",   BrushKey = "Brush.Method.GET"    },
        new() { Name = "OPTIONS",BrushKey = "Brush.Method.GET"    },
    ];

    public HttpMethodOption SelectedMethod
    {
        get => _selectedMethod;
        set => SetProperty(ref _selectedMethod, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    // Request editor tab: 0=Params, 1=Headers, 2=Body, 3=Auth
    public int RequestTabIndex
    {
        get => _requestTabIndex;
        set => SetProperty(ref _requestTabIndex, value);
    }

    // Response viewer tab: 0=Body, 1=Headers, 2=Cookies
    public int ResponseTabIndex
    {
        get => _responseTabIndex;
        set => SetProperty(ref _responseTabIndex, value);
    }

    public bool HasResponse
    {
        get => _hasResponse;
        set => SetProperty(ref _hasResponse, value);
    }

    public string ResponseStatus
    {
        get => _responseStatus;
        set => SetProperty(ref _responseStatus, value);
    }

    public string ResponseTime
    {
        get => _responseTime;
        set => SetProperty(ref _responseTime, value);
    }

    public string ResponseSize
    {
        get => _responseSize;
        set => SetProperty(ref _responseSize, value);
    }

    public string ResponseBody
    {
        get => _responseBody;
        set => SetProperty(ref _responseBody, value);
    }

    [RelayCommand]
    public void Send()
    {
        // Business logic placeholder — not implemented yet
    }

    public RequestTab()
    {
        _selectedMethod = Methods[0];
    }

    private HttpMethodOption _selectedMethod;
    private string _url = string.Empty;
    private int _requestTabIndex = 0;
    private int _responseTabIndex = 0;
    private bool _hasResponse = false;
    private string _responseStatus = string.Empty;
    private string _responseTime = string.Empty;
    private string _responseSize = string.Empty;
    private string _responseBody = string.Empty;
}