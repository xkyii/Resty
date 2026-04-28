namespace Resty.Core.Models;

public enum AssertionSubjectType
{
    Status,
    ResponseTime,
    BodyJsonPath,
    Header,
}

public enum AssertionOperator
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    In,
    Contains,
}

public sealed class AssertionRule
{
    public string RawText { get; init; } = string.Empty;
    public AssertionSubjectType SubjectType { get; init; }

    /// <summary>JSONPath (e.g. "$.name") for BodyJsonPath, or header name for Header.</summary>
    public string? SubjectPath { get; init; }

    public AssertionOperator Operator { get; init; }

    /// <summary>Raw expected value token from source: e.g. "200", '"John"', "null", "[200,201]".</summary>
    public string ExpectedValue { get; init; } = string.Empty;
}
