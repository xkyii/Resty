using System.Collections.Generic;

namespace Kx.Resty.Domain.Http;

public sealed class HttpRequestData
{
    public required string Method { get; init; }
    public required string Url { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public string? Body { get; init; }
}
