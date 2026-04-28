namespace Resty.Core.Models;

public sealed class AssertionResult
{
    public AssertionRule Rule { get; init; } = null!;
    public bool Passed { get; init; }
    public string? ActualValue { get; init; }
    public string? ErrorMessage { get; init; }
}
