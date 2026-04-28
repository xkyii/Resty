namespace Resty.Core.Models;

public sealed class HttpRequestDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";
    public string Url { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Body { get; init; }
    public List<AssertionRule> Assertions { get; init; } = [];
}
