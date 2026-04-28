namespace Resty.Core.Models;

public sealed class RequestExecutionResult
{
    public HttpRequestDefinition Request { get; init; } = null!;
    public HttpExecutionResult? Response { get; init; }
    public List<AssertionResult> AssertionResults { get; init; } = [];

    public bool HasTransportError => Response?.IsSuccess == false;
    public bool AllAssertionsPassed => AssertionResults.Count == 0 || AssertionResults.All(r => r.Passed);
    public bool IsSuccess => !HasTransportError && AllAssertionsPassed;
}
