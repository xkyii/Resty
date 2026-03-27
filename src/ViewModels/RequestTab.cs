using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kx.Resty.Models;
using System.Diagnostics;
using System.Text;

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
    [ObservableProperty] private string           _collectionName = string.Empty;
    [ObservableProperty] private bool             _isSaved;
    [ObservableProperty] private string           _saveStatusText = string.Empty;
    public RequestTab(HttpRequestEntry entry, HttpCollection collection)
    {
        _entry      = entry;
        _collection = collection;

        CollectionName = Path.GetFileName(collection.FilePath);
        IsSaved        = true;
        UpdateSaveStatusText();

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

        CollectionName = App.Text("Request.Collection.Unlinked");
        IsSaved        = false;
        UpdateSaveStatusText();
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
        IsSaved = false;
        ScheduleSave();
    }

    partial void OnUrlChanged(string value)
    {
        _entry.Url = value;
        IsSaved = false;
        ScheduleSave();
    }

    partial void OnBodyChanged(string value)
    {
        _entry.Body = value;
        IsSaved = false;
        ScheduleSave();
    }

    partial void OnIsSavedChanged(bool value)
        => UpdateSaveStatusText();

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
            IsSaved = true;
        });
    }

    // ─── Send ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task Send()
    {
        if (IsSending) return;

        IsSending = true;
        HasResponse = false;
        ResponseStatus = string.Empty;
        ResponseTime = string.Empty;
        ResponseSize = string.Empty;
        ResponseBody = string.Empty;

        var watch = Stopwatch.StartNew();
        await Task.Delay(280);
        watch.Stop();

        if (string.IsNullOrWhiteSpace(Url))
        {
            ResponseStatus = App.Text("Request.Send.Invalid");
            ResponseTime   = $"{watch.ElapsedMilliseconds} ms";
            ResponseSize   = "0 B";
            ResponseBody   = App.Text("Request.Send.InvalidHint");
            HasResponse    = true;
            IsSending      = false;
            return;
        }

        ResponseStatus = "200 OK (mock)";
        ResponseTime   = $"{watch.ElapsedMilliseconds} ms";
        ResponseBody =
            $"Method: {SelectedMethod.Name}\n" +
            $"URL: {Url}\n" +
            $"Collection: {CollectionName}\n" +
            $"Saved: {(IsSaved ? App.Text("Request.SaveState.Saved") : App.Text("Request.SaveState.Unsaved"))}\n" +
            "\n" +
            "Mock response generated by UI placeholder. HTTP transport is not wired yet.";
        ResponseSize = $"{Encoding.UTF8.GetByteCount(ResponseBody)} B";
        HasResponse  = true;
        IsSending    = false;
    }

    private void UpdateSaveStatusText()
    {
        if (_collection is null)
        {
            SaveStatusText = App.Text("Request.SaveState.Unlinked");
            return;
        }

        SaveStatusText = IsSaved
            ? App.Text("Request.SaveState.Saved")
            : App.Text("Request.SaveState.Unsaved");
    }
}