using Kx.Resty.Domain.Http;

namespace Kx.Resty.Domain.Abstractions;

public interface IHttpRequestExecutor
{
    Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken = default);
}
