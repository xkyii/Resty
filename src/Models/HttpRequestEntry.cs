using CommunityToolkit.Mvvm.ComponentModel;

namespace Kx.Resty.Models;

public partial class HttpRequestEntry : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Name from <c># @name</c> or after <c>###</c>.</summary>
    public string? Name        { get; set; }
    public string  Method      { get; set; } = "GET";
    public string  Url         { get; set; } = string.Empty;
    public string  Body        { get; set; } = string.Empty;
    /// <summary>Set when body is a file reference: <c>&lt; ./path</c>.</summary>
    public string? BodyFilePath { get; set; }

    public List<NamedValue>       Headers     { get; } = [];
    public List<NamedValue>       QueryParams { get; } = [];
    public RequestAnnotations     Annotations { get; } = new();

    public string DisplayName => Name ?? $"{Method} {Url}";

    /// <summary>Brush resource key used to colour the method badge in the sidebar.</summary>
    public string MethodBrushKey => Method switch
    {
        "DELETE" => "Brush.Method.DELETE",
        "POST"   => "Brush.Method.POST",
        "PUT"    => "Brush.Method.PUT",
        "PATCH"  => "Brush.Method.PATCH",
        _        => "Brush.Method.GET"
    };
}
