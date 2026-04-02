namespace Kx.Resty.Domain.Http;

public sealed class HttpResponseData
{
    public required int StatusCode { get; init; }
    public required long ElapsedMilliseconds { get; init; }
    public required long SizeBytes { get; init; }
    public required string BodyContent { get; init; }
    public required string HeadersContent { get; init; }
    public required string CookiesContent { get; init; }
}
