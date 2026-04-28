namespace Resty.Core.Models;

public sealed class HttpExecutionResult
{
    public int StatusCode { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string Body { get; init; } = string.Empty;
    public long ElapsedMs { get; init; }

    /// <summary>Non-null when a network/transport error occurred (not an HTTP error status).</summary>
    public string? Error { get; init; }

    public bool IsSuccess => Error is null;
}
