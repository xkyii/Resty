using Resty.Rebuild.Domain.Http;

namespace Resty.Rebuild.Application.Abstractions;

public interface IHttpRequestExecutor
{
    Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken = default);
}
