namespace Resty.Rebuild.Domain.Http;

public sealed class HttpRequestData
{
    public required string Method { get; init; }

    public required string Url { get; init; }
}
