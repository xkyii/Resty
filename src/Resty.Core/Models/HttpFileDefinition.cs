namespace Resty.Core.Models;

public sealed class HttpFileDefinition
{
    public string FilePath { get; init; } = string.Empty;
    public List<HttpRequestDefinition> Requests { get; init; } = [];
    public Dictionary<string, string> FileVariables { get; init; } = new(StringComparer.Ordinal);
}
