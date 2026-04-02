using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Resty.Rebuild.Application.Abstractions;
using Resty.Rebuild.Domain.Http;

namespace Resty.Rebuild.Infrastructure.Http;

public sealed class SystemHttpRequestExecutor : IHttpRequestExecutor
{
    private readonly HttpClient _httpClient = new();

    public async Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken = default)
    {
        var method = ParseMethod(request.Method);
        using var req = new HttpRequestMessage(method, request.Url);

        var sw = Stopwatch.StartNew();
        using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var bytes = await res.Content.ReadAsByteArrayAsync(cancellationToken);
        sw.Stop();

        var bodyText = DecodeBody(res.Content.Headers.ContentType?.MediaType, bytes);
        var headersText = BuildHeadersText(res);
        var cookiesText = BuildCookiesText(res);

        return new HttpResponseData
        {
            StatusCode = (int)res.StatusCode,
            ElapsedMilliseconds = sw.ElapsedMilliseconds,
            SizeBytes = bytes.LongLength,
            BodyContent = bodyText,
            HeadersContent = headersText,
            CookiesContent = cookiesText
        };
    }

    private static HttpMethod ParseMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return HttpMethod.Get;

        return method.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => new HttpMethod(method.ToUpperInvariant())
        };
    }

    private static string DecodeBody(string? mediaType, byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var mt = mediaType?.ToLowerInvariant() ?? string.Empty;
        var isText = mt.StartsWith("text/") || mt.Contains("json") || mt.Contains("xml") || mt.Contains("javascript");

        if (!isText)
            return Convert.ToBase64String(bytes);

        return Encoding.UTF8.GetString(bytes);
    }

    private static string BuildHeadersText(HttpResponseMessage response)
    {
        var lines = new List<string>();
        foreach (var header in response.Headers)
            lines.Add($"{header.Key}: {string.Join(", ", header.Value)}");

        foreach (var header in response.Content.Headers)
            lines.Add($"{header.Key}: {string.Join(", ", header.Value)}");

        return string.Join("\n", lines);
    }

    private static string BuildCookiesText(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return "(无 Cookies)";

        var cookieText = string.Join("\n", cookies);
        return string.IsNullOrWhiteSpace(cookieText) ? "(无 Cookies)" : cookieText;
    }
}
