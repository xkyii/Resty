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

public class HttpAuthOption
{
    public string Name { get; init; } = "";
    public string Code { get; init; } = "";
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

    public static readonly HttpAuthOption[] AuthTypes =
    [
        new() { Name = "None",   Code = "none" },
        new() { Name = "Basic",  Code = "basic" },
        new() { Name = "Digest", Code = "digest" },
    ];

    // ─── Backing model ────────────────────────────────────────────────────────

    private readonly HttpRequestEntry _entry;
    private readonly HttpCollection?  _collection;   // null = unsaved new request
    private bool _suppressDirtyMark;
    private bool _isSynchronizingUrlAndParams;

    /// <summary>Exposes the backing model (used by WorkspaceTab to de-duplicate open tabs).</summary>
    public HttpRequestEntry Entry => _entry;
    public HttpCollection? Collection => _collection;

    public event Action? TabStateChanged;

    // ─── Constructors ─────────────────────────────────────────────────────────

    /// <summary>Opens an existing request for editing.</summary>
    public RequestTab(HttpRequestEntry entry, HttpCollection collection)
    {
        _entry      = entry;
        _collection = collection;

        HeadersTable = new KeyValueTableViewModel();
        ParamsTable  = new KeyValueTableViewModel();

        ReloadFromEntry();

        HeadersTable.Changed += MarkDirty;
        ParamsTable.Changed  += OnParamsTableChanged;
    }

    /// <summary>Creates a new unsaved request (not linked to any file).</summary>
    public RequestTab()
    {
        _entry      = new HttpRequestEntry();
        _collection = null;

        _selectedMethod   = Methods[0];
        _selectedAuthType = AuthTypes[0];
        HeadersTable      = new KeyValueTableViewModel();
        ParamsTable       = new KeyValueTableViewModel();

        CollectionName = App.Text("Request.Collection.Unlinked");
        RequestName    = string.Empty;
        IsSaved        = false;
        UpdateSaveStatusText();

        HeadersTable.Changed += MarkDirty;
        ParamsTable.Changed  += OnParamsTableChanged;
    }

    public void ReloadFromEntry()
    {
        _suppressDirtyMark = true;
        _isSynchronizingUrlAndParams = true;

        CollectionName = _collection is null
            ? App.Text("Request.Collection.Unlinked")
            : Path.GetFileNameWithoutExtension(_collection.FilePath);
        RequestName = _entry.Name ?? string.Empty;
        SelectedMethod = Methods.FirstOrDefault(m => m.Name == _entry.Method) ?? Methods[0];
        Url = BuildEditableUrl(_entry.Url, _entry.QueryParams);
        Body = GetEditableBodyText(_entry);

        SelectedAuthType = AuthTypes[0];
        AuthUsername = string.Empty;
        AuthPassword = string.Empty;

        HeadersTable.ReplaceWith(GetEditableHeaders(_entry));
        ParamsTable.ReplaceWith(_entry.QueryParams);

        IsSaved = _collection is not null;
        UpdateSaveStatusText();
        NotifyTabStateChanged();

        _isSynchronizingUrlAndParams = false;
        _suppressDirtyMark = false;

        // If params table is empty but URL contains query string, extract params from URL
        if (ParamsTable.Items.Count == 0 && !string.IsNullOrEmpty(Url))
        {
            var (_, queryParams, _) = SplitUrlParts(Url);
            if (queryParams.Count > 0)
                SyncParamsFromUrl(Url);
        }
    }

    // ─── Request editor state ─────────────────────────────────────────────────

    public KeyValueTableViewModel HeadersTable { get; }
    public KeyValueTableViewModel ParamsTable  { get; }

    [ObservableProperty] private HttpMethodOption _selectedMethod = null!;
    [ObservableProperty] private HttpAuthOption   _selectedAuthType = null!;
    [ObservableProperty] private string           _url            = string.Empty;
    [ObservableProperty] private string           _body           = string.Empty;
    [ObservableProperty] private string           _authUsername   = string.Empty;
    [ObservableProperty] private string           _authPassword   = string.Empty;
    [ObservableProperty] private string           _requestName    = string.Empty;
    [ObservableProperty] private string           _collectionName = string.Empty;
    [ObservableProperty] private bool             _isSaved;
    [ObservableProperty] private string           _saveStatusText = string.Empty;

    public bool CanSave => _collection is not null;
    public bool CanRenameCollection => _collection is not null;
    public bool HasCredentialsAuth => SelectedAuthType.Code is "basic" or "digest";

    public string TabTitle
    {
        get
        {
            var baseTitle = !string.IsNullOrWhiteSpace(RequestName)
                ? RequestName.Trim()
                : string.IsNullOrWhiteSpace(Url)
                    ? "New Request"
                    : $"{SelectedMethod.Name} {Url}";
            return IsSaved ? baseTitle : $"{baseTitle} *";
        }
    }

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
        MarkDirty();
        NotifyTabStateChanged();
    }

    partial void OnUrlChanged(string value)
    {
        _entry.Url = GetUrlBasePart(value);

        if (_isSynchronizingUrlAndParams)
        {
            NotifyTabStateChanged();
            return;
        }

        SyncParamsFromUrl(value);
        MarkDirty();
        NotifyTabStateChanged();
    }

    partial void OnBodyChanged(string value)
    {
        MarkDirty();
    }

    partial void OnSelectedAuthTypeChanged(HttpAuthOption value)
    {
        OnPropertyChanged(nameof(HasCredentialsAuth));
        MarkDirty();
    }

    partial void OnAuthUsernameChanged(string value)
        => MarkDirty();

    partial void OnAuthPasswordChanged(string value)
        => MarkDirty();

    partial void OnRequestNameChanged(string value)
    {
        _entry.Name = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        MarkDirty();
        NotifyTabStateChanged();
    }

    partial void OnCollectionNameChanged(string value)
    {
        if (_collection is null) return;
        MarkDirty();
    }

    partial void OnIsSavedChanged(bool value)
    {
        UpdateSaveStatusText();
        NotifyTabStateChanged();
    }

    // ─── Save ─────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSave))]
    public void Save()
    {
        if (_collection is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            ApplyCollectionRenameIfNeeded();

            _entry.Name = string.IsNullOrWhiteSpace(RequestName) ? null : RequestName.Trim();
            _entry.Method = SelectedMethod.Name;
            _entry.Url = GetUrlBasePart(Url);
            ApplyEditableBodyToEntry();

            _entry.Headers.Clear();
            _entry.Headers.AddRange(HeadersTable.ToNamedValues());
            AppendAuthorizationHeader(_entry.Headers);
            SyncEntryQueryParams();
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

    private void ApplyCollectionRenameIfNeeded()
    {
        if (_collection is null) return;

        var originalPath = _collection.FilePath;
        var dir = Path.GetDirectoryName(originalPath);
        if (string.IsNullOrWhiteSpace(dir)) return;

        var targetName = SanitizeFileName(CollectionName);
        if (string.IsNullOrWhiteSpace(targetName))
            targetName = "collection";

        var targetPath = Path.Combine(dir, targetName + ".http");
        if (string.Equals(originalPath, targetPath, StringComparison.OrdinalIgnoreCase))
            return;

        if (File.Exists(targetPath))
            return;

        File.Move(originalPath, targetPath);
        _collection.FilePath = targetPath;
        _collection.Name = targetName;
        CollectionName = targetName;
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Trim().Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars).Trim();
    }

    private List<NamedValue> GetEditableHeaders(HttpRequestEntry entry)
    {
        var result = new List<NamedValue>();

        foreach (var header in entry.Headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase) &&
                TryParseAuthorizationHeader(header.Value, out var authType, out var username, out var password))
            {
                SelectedAuthType = authType;
                AuthUsername = username;
                AuthPassword = password;
                continue;
            }

            result.Add(new NamedValue
            {
                Enabled = header.Enabled,
                Key = header.Key,
                Value = header.Value,
            });
        }

        return result;
    }

    private static string GetEditableBodyText(HttpRequestEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.BodyFilePath))
            return $"< {entry.BodyFilePath}";

        return entry.Body;
    }

    private void ApplyEditableBodyToEntry()
    {
        var text = Body ?? string.Empty;
        var trimmed = text.Trim();

        if (trimmed.StartsWith("< ", StringComparison.Ordinal))
        {
            _entry.Body = string.Empty;
            _entry.BodyFilePath = trimmed[2..].Trim();
            return;
        }

        _entry.Body = text;
        _entry.BodyFilePath = null;
    }

    private void AppendAuthorizationHeader(List<NamedValue> headers)
    {
        if (!HasCredentialsAuth)
            return;

        var username = AuthUsername.Trim();
        var password = AuthPassword.Trim();
        if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
            return;

        headers.Add(new NamedValue
        {
            Enabled = true,
            Key = "Authorization",
            Value = $"{SelectedAuthType.Name} {username} {password}".TrimEnd(),
        });
    }

    private static bool TryParseAuthorizationHeader(
        string value,
        out HttpAuthOption authType,
        out string username,
        out string password)
    {
        authType = AuthTypes[0];
        username = string.Empty;
        password = string.Empty;

        foreach (var candidate in AuthTypes.Where(x => x.Code != "none"))
        {
            var prefix = candidate.Name + " ";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            authType = candidate;
            var payload = value[prefix.Length..].Trim();
            var parts = payload.Split(' ', 2, StringSplitOptions.None);
            username = parts.ElementAtOrDefault(0) ?? string.Empty;
            password = parts.ElementAtOrDefault(1) ?? string.Empty;
            return true;
        }

        return false;
    }

    private void MarkDirty()
    {
        if (_suppressDirtyMark) return;
        IsSaved = false;
    }

    private void OnParamsTableChanged()
    {
        SyncEntryQueryParams();

        if (_isSynchronizingUrlAndParams)
            return;

        SyncUrlFromParams();
        MarkDirty();
        NotifyTabStateChanged();
    }

    private void SyncParamsFromUrl(string url)
    {
        var (baseUrl, queryParams, _) = SplitUrlParts(url);

        _isSynchronizingUrlAndParams = true;
        try
        {
            ParamsTable.ReplaceWith(queryParams);
        }
        finally
        {
            _isSynchronizingUrlAndParams = false;
        }

        _entry.Url = baseUrl;
        SyncEntryQueryParams();
    }

    private void SyncUrlFromParams()
    {
        var (baseUrl, _, fragment) = SplitUrlParts(Url);
        var rebuiltUrl = BuildEditableUrl(baseUrl, ParamsTable.ToNamedValues(), fragment);

        _isSynchronizingUrlAndParams = true;
        try
        {
            if (!string.Equals(Url, rebuiltUrl, StringComparison.Ordinal))
                Url = rebuiltUrl;
        }
        finally
        {
            _isSynchronizingUrlAndParams = false;
        }

        _entry.Url = baseUrl;
    }

    private void SyncEntryQueryParams()
    {
        _entry.QueryParams.Clear();
        _entry.QueryParams.AddRange(ParamsTable.ToNamedValues().Select(CloneNamedValue));
    }

    private static NamedValue CloneNamedValue(NamedValue source) => new()
    {
        Enabled = source.Enabled,
        Key = source.Key,
        Value = source.Value,
    };

    private static string GetUrlBasePart(string url)
        => SplitUrlParts(url).BaseUrl;

    private static string BuildEditableUrl(string baseUrl, IEnumerable<NamedValue> queryParams, string fragment = "")
    {
        var enabledParams = queryParams
            .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key))
            .ToList();
        if (enabledParams.Count == 0)
            return baseUrl + fragment;

        var sb = new StringBuilder(baseUrl);
        var separator = baseUrl.Contains('?') ? '&' : '?';
        foreach (var p in enabledParams)
        {
            sb.Append(separator);
            sb.Append(p.Key);
            if (!string.IsNullOrEmpty(p.Value))
                sb.Append('=').Append(p.Value);
            separator = '&';
        }

        sb.Append(fragment);
        return sb.ToString();
    }

    private static (string BaseUrl, List<NamedValue> QueryParams, string Fragment) SplitUrlParts(string url)
    {
        var value = url ?? string.Empty;
        var hashIndex = value.IndexOf('#');
        var fragment = hashIndex >= 0 ? value[hashIndex..] : string.Empty;
        var withoutFragment = hashIndex >= 0 ? value[..hashIndex] : value;

        var queryIndex = withoutFragment.IndexOf('?');
        if (queryIndex < 0)
            return (withoutFragment, [], fragment);

        var baseUrl = withoutFragment[..queryIndex];
        var query = withoutFragment[(queryIndex + 1)..];
        var result = new List<NamedValue>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = part.IndexOf('=');
            if (idx < 0)
            {
                result.Add(new NamedValue { Key = part, Value = string.Empty, Enabled = true });
                continue;
            }

            result.Add(new NamedValue
            {
                Key = part[..idx],
                Value = part[(idx + 1)..],
                Enabled = true,
            });
        }

        return (baseUrl, result, fragment);
    }

    private void NotifyTabStateChanged()
        => TabStateChanged?.Invoke();

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
