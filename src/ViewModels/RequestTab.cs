using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kx.Resty.Models;

namespace Kx.Resty.ViewModels;

/// <summary>Represents an HTTP method option shown in the ComboBox.</summary>
public class HttpMethodOption
{
    public string Name     { get; init; } = "";
    public string BrushKey { get; init; } = "";
}

public partial class RequestTab : ObservableObject
{
    // ─── Static data ──────────────────────────────────────────────────────────

    public static readonly HttpMethodOption[] Methods =
    [
        new() { Name = "GET",     BrushKey = "Brush.Method.GET"    },
        new() { Name = "POST",    BrushKey = "Brush.Method.POST"   },
        new() { Name = "PUT",     BrushKey = "Brush.Method.PUT"    },
        new() { Name = "DELETE",  BrushKey = "Brush.Method.DELETE" },
        new() { Name = "PATCH",   BrushKey = "Brush.Method.PATCH"  },
        new() { Name = "HEAD",    BrushKey = "Brush.Method.GET"    },
        new() { Name = "OPTIONS", BrushKey = "Brush.Method.GET"    },
    ];

    // ─── Backing model ────────────────────────────────────────────────────────

    private readonly HttpRequestEntry _entry;
    private readonly HttpCollection?  _collection;   // null = unsaved new request
    private Timer?                    _saveTimer;

    /// <summary>Exposes the backing model (used by WorkspaceTab to de-duplicate open tabs).</summary>
    public HttpRequestEntry Entry => _entry;

    // ─── Constructors ─────────────────────────────────────────────────────────

    /// <summary>Opens an existing request for editing.</summary>
    public RequestTab(HttpRequestEntry entry, HttpCollection collection)
    {
        _entry      = entry;
        _collection = collection;

        _selectedMethod = Methods.FirstOrDefault(m => m.Name == entry.Method) ?? Methods[0];
        _url            = entry.Url;
        _body           = entry.Body;

        HeadersTable = new KeyValueTableViewModel(entry.Headers);
        ParamsTable  = new KeyValueTableViewModel(entry.QueryParams);

        HeadersTable.Items.CollectionChanged += (_, _) => ScheduleSave();
        ParamsTable.Items.CollectionChanged  += (_, _) => ScheduleSave();
    }

    /// <summary>Creates a new unsaved request (not linked to any file).</summary>
    public RequestTab()
    {
        _entry      = new HttpRequestEntry();
        _collection = null;

        _selectedMethod = Methods[0];
        HeadersTable    = new KeyValueTableViewModel();
        ParamsTable     = new KeyValueTableViewModel();
    }

    // ─── Request editor state ─────────────────────────────────────────────────

    public KeyValueTableViewModel HeadersTable { get; }
    public KeyValueTableViewModel ParamsTable  { get; }

    [ObservableProperty] private HttpMethodOption _selectedMethod = null!;
    [ObservableProperty] private string           _url            = string.Empty;
    [ObservableProperty] private string           _body           = string.Empty;

    // Request sub-tab: 0=Params, 1=Headers, 2=Body, 3=Auth
    [ObservableProperty] private int _requestTabIndex;
    // Response sub-tab: 0=Body, 1=Headers, 2=Cookies
    [ObservableProperty] private int _responseTabIndex;

    // ─── Response state ───────────────────────────────────────────────────────

    [ObservableProperty] private bool   _hasResponse;
    [ObservableProperty] private bool   _isSending;
    [ObservableProperty] private string _responseStatus = string.Empty;
    [ObservableProperty] private string _responseTime   = string.Empty;
    [ObservableProperty] private string _responseSize   = string.Empty;
    [ObservableProperty] private string _responseBody   = string.Empty;

    // ─── Property change → sync entry ────────────────────────────────────────

    partial void OnSelectedMethodChanged(HttpMethodOption value)
    {
        _entry.Method = value.Name;
        ScheduleSave();
    }

    partial void OnUrlChanged(string value)
    {
        _entry.Url = value;
        ScheduleSave();
    }

    partial void OnBodyChanged(string value)
    {
        _entry.Body = value;
        ScheduleSave();
    }

    // ─── Debounced write-back ─────────────────────────────────────────────────

    private void ScheduleSave()
    {
        if (_collection is null) return;
        _saveTimer?.Dispose();
        _saveTimer = new Timer(_ => WriteBack(), null, 500, Timeout.Infinite);
    }

    private void WriteBack()
    {
        if (_collection is null) return;
        // Must run on UI thread to safely read ObservableCollection items.
        Dispatcher.UIThread.Post(() =>
        {
            _entry.Headers.Clear();
            _entry.Headers.AddRange(HeadersTable.ToNamedValues());
            _entry.QueryParams.Clear();
            _entry.QueryParams.AddRange(ParamsTable.ToNamedValues());
            Commands.HttpFileWriter.Write(_collection);
        });
    }

    // ─── Send ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task Send()
    {
        // HTTP send will be implemented in the next phase.
        await Task.CompletedTask;
    }
}