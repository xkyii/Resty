using System.Diagnostics;
using System.Net.Http.Headers;
using Resty.Core.Models;

namespace Resty.Core.Execution;

/// <summary>
/// Executes HTTP requests defined by <see cref="HttpRequestDefinition"/>.
/// Dispose to release the underlying <see cref="HttpClient"/>.
/// </summary>
public sealed class HttpRequestExecutor : IDisposable
{
    private readonly HttpClient _client;

    public HttpRequestExecutor(int timeoutMs = 30_000)
    {
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(timeoutMs),
        };
    }

    public async Task<HttpExecutionResult> ExecuteAsync(
        HttpRequestDefinition request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var httpRequest = BuildHttpRequestMessage(request);
            using var response = await _client.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            sw.Stop();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var headers = CollectHeaders(response);

            return new HttpExecutionResult
            {
                StatusCode = (int)response.StatusCode,
                Headers = headers,
                Body = body,
                ElapsedMs = sw.ElapsedMilliseconds,
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return ErrorResult(sw.ElapsedMilliseconds, "Request cancelled or timed out.");
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return ErrorResult(sw.ElapsedMilliseconds, ex.Message);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ErrorResult(sw.ElapsedMilliseconds, ex.Message);
        }
    }

    // -------------------------------------------------------------------------

    private static HttpRequestMessage BuildHttpRequestMessage(HttpRequestDefinition request)
    {
        var message = new HttpRequestMessage(
            new System.Net.Http.HttpMethod(request.Method),
            request.Url);

        foreach (var (name, value) in request.Headers)
        {
            // Content-* headers must go on HttpContent, not the request
            if (!name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                message.Headers.TryAddWithoutValidation(name, value);
        }

        if (request.Body is not null)
        {
            var content = new StringContent(request.Body, System.Text.Encoding.UTF8);

            // Override Content-Type if the user specified one
            if (request.Headers.TryGetValue("Content-Type", out var ct)
                && MediaTypeHeaderValue.TryParse(ct, out var mediaType))
            {
                content.Headers.ContentType = mediaType;
            }

            message.Content = content;
        }

        return message;
    }

    private static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in response.Headers)
            headers[h.Key] = string.Join(", ", h.Value);
        foreach (var h in response.Content.Headers)
            headers[h.Key] = string.Join(", ", h.Value);
        return headers;
    }

    private static HttpExecutionResult ErrorResult(long elapsedMs, string error) =>
        new()
        {
            StatusCode = 0,
            Headers = [],
            Body = string.Empty,
            ElapsedMs = elapsedMs,
            Error = error,
        };

    public void Dispose() => _client.Dispose();
}
