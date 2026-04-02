using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Kx.Resty.Domain.Abstractions;
using Kx.Resty.Domain.Http;

namespace Kx.Resty.Infrastructure.Http;

public sealed class SystemHttpRequestExecutor : IHttpRequestExecutor
{
    private readonly HttpClient _httpClient = new();

    public async Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken = default)
    {
        var method = ParseMethod(request.Method);
        using var req = new HttpRequestMessage(method, request.Url);

        foreach (var header in request.Headers)
        {
            if (!req.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                req.Content ??= new StringContent(string.Empty);
                req.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (!string.IsNullOrEmpty(request.Body) && method != HttpMethod.Get && method != HttpMethod.Head)
            req.Content = new StringContent(request.Body, Encoding.UTF8);

        var sw = Stopwatch.StartNew();
        using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var bytes = await res.Content.ReadAsByteArrayAsync(cancellationToken);
        sw.Stop();

        return new HttpResponseData
        {
            StatusCode = (int)res.StatusCode,
            ElapsedMilliseconds = sw.ElapsedMilliseconds,
            SizeBytes = bytes.LongLength,
            BodyContent = DecodeBody(res.Content.Headers.ContentType?.MediaType, bytes),
            HeadersContent = BuildHeadersText(res),
            CookiesContent = BuildCookiesText(res)
        };
    }

    private static HttpMethod ParseMethod(string method) =>
        (string.IsNullOrWhiteSpace(method) ? "GET" : method.ToUpperInvariant()) switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            var m => new HttpMethod(m)
        };

    private static string DecodeBody(string? mediaType, byte[] bytes)
    {
        if (bytes.Length == 0) return string.Empty;
        var mt = mediaType?.ToLowerInvariant() ?? string.Empty;
        var isText = mt.StartsWith("text/") || mt.Contains("json") || mt.Contains("xml") || mt.Contains("javascript");
        return isText ? Encoding.UTF8.GetString(bytes) : Convert.ToBase64String(bytes);
    }

    private static string BuildHeadersText(HttpResponseMessage response)
    {
        var lines = new List<string>();
        foreach (var h in response.Headers) lines.Add($"{h.Key}: {string.Join(", ", h.Value)}");
        foreach (var h in response.Content.Headers) lines.Add($"{h.Key}: {string.Join(", ", h.Value)}");
        return string.Join("\n", lines);
    }

    private static string BuildCookiesText(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies)) return "(无 Cookies)";
        var text = string.Join("\n", cookies);
        return string.IsNullOrWhiteSpace(text) ? "(无 Cookies)" : text;
    }
}
