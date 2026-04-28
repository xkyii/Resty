using Resty.Core.Models;

namespace Resty.Core.Assertions;

/// <summary>
/// Evaluates <see cref="AssertionRule"/> list against an <see cref="HttpExecutionResult"/>.
/// </summary>
public static class AssertionEngine
{
    public static List<AssertionResult> Evaluate(
        IReadOnlyList<AssertionRule> rules,
        HttpExecutionResult response)
    {
        var results = new List<AssertionResult>(rules.Count);
        foreach (var rule in rules)
            results.Add(EvaluateRule(rule, response));
        return results;
    }

    // -------------------------------------------------------------------------

    private static AssertionResult EvaluateRule(AssertionRule rule, HttpExecutionResult response)
    {
        try
        {
            return rule.SubjectType switch
            {
                AssertionSubjectType.Status =>
                    EvaluateNumeric(rule, response.StatusCode.ToString()),
                AssertionSubjectType.ResponseTime =>
                    EvaluateNumeric(rule, response.ElapsedMs.ToString()),
                AssertionSubjectType.BodyJsonPath =>
                    EvaluateJsonPath(rule, response.Body),
                AssertionSubjectType.Header =>
                    EvaluateHeader(rule, response.Headers),
                _ => Fail(rule, null, "Unknown subject type"),
            };
        }
        catch (Exception ex)
        {
            return Fail(rule, null, ex.Message);
        }
    }

    // ---- Status / ResponseTime -------------------------------------------------

    private static AssertionResult EvaluateNumeric(AssertionRule rule, string actualStr)
    {
        if (rule.Operator == AssertionOperator.In)
            return EvaluateInArray(rule, actualStr);

        if (!long.TryParse(actualStr, out var actual))
            return Fail(rule, actualStr, "Cannot parse actual value as number");

        if (!long.TryParse(rule.ExpectedValue, out var expected))
            return Fail(rule, actualStr, $"Cannot parse expected value '{rule.ExpectedValue}' as number");

        var passed = rule.Operator switch
        {
            AssertionOperator.Equal => actual == expected,
            AssertionOperator.NotEqual => actual != expected,
            AssertionOperator.LessThan => actual < expected,
            AssertionOperator.LessThanOrEqual => actual <= expected,
            AssertionOperator.GreaterThan => actual > expected,
            AssertionOperator.GreaterThanOrEqual => actual >= expected,
            _ => false,
        };

        return Result(rule, passed, actualStr);
    }

    // ---- In array [200, 201] ---------------------------------------------------

    private static AssertionResult EvaluateInArray(AssertionRule rule, string actualStr)
    {
        // ExpectedValue looks like "[200, 201]"
        var inner = rule.ExpectedValue.Trim();
        if (!inner.StartsWith('[') || !inner.EndsWith(']'))
            return Fail(rule, actualStr, "Expected value for 'in' must be an array, e.g. [200, 201]");

        var elements = inner[1..^1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var passed = elements.Any(e =>
            string.Equals(e.Trim('"', '\''), actualStr.Trim('"', '\''),
                StringComparison.OrdinalIgnoreCase));

        return Result(rule, passed, actualStr);
    }

    // ---- JSONPath --------------------------------------------------------------

    private static AssertionResult EvaluateJsonPath(AssertionRule rule, string body)
    {
        var path = rule.SubjectPath ?? "$";

        // Null / existence checks
        if (rule.Operator == AssertionOperator.Equal && IsNullToken(rule.ExpectedValue))
        {
            var exists = JsonPathHelper.Exists(body, path);
            var actualVal = exists ? JsonPathHelper.Evaluate(body, path) : null;
            var isNull = !exists || actualVal is null;
            return Result(rule, isNull, actualVal ?? "null");
        }

        if (rule.Operator == AssertionOperator.NotEqual && IsNullToken(rule.ExpectedValue))
        {
            var exists = JsonPathHelper.Exists(body, path);
            var actualVal = exists ? JsonPathHelper.Evaluate(body, path) : null;
            var notNull = exists && actualVal is not null;
            return Result(rule, notNull, actualVal ?? "null");
        }

        var actual = JsonPathHelper.Evaluate(body, path);
        if (actual is null)
            return Fail(rule, "null", $"JSONPath '{path}' not found or is null");

        return rule.Operator switch
        {
            AssertionOperator.Equal =>
                Result(rule, StringEquals(actual, StripQuotes(rule.ExpectedValue)), actual),
            AssertionOperator.NotEqual =>
                Result(rule, !StringEquals(actual, StripQuotes(rule.ExpectedValue)), actual),
            AssertionOperator.Contains =>
                Result(rule, actual.Contains(StripQuotes(rule.ExpectedValue),
                    StringComparison.OrdinalIgnoreCase), actual),
            AssertionOperator.In =>
                EvaluateInArray(rule, actual),
            _ =>
                EvaluateNumeric(rule, actual),
        };
    }

    // ---- Header ---------------------------------------------------------------

    private static AssertionResult EvaluateHeader(AssertionRule rule, Dictionary<string, string> headers)
    {
        var headerName = rule.SubjectPath ?? string.Empty;

        if (!headers.TryGetValue(headerName, out var actual))
            return Fail(rule, null, $"Response header '{headerName}' not present");

        return rule.Operator switch
        {
            AssertionOperator.Equal =>
                Result(rule, StringEquals(actual, StripQuotes(rule.ExpectedValue)), actual),
            AssertionOperator.NotEqual =>
                Result(rule, !StringEquals(actual, StripQuotes(rule.ExpectedValue)), actual),
            AssertionOperator.Contains =>
                Result(rule, actual.Contains(StripQuotes(rule.ExpectedValue),
                    StringComparison.OrdinalIgnoreCase), actual),
            _ =>
                Fail(rule, actual, $"Operator '{rule.Operator}' not supported for headers"),
        };
    }

    // ---- Helpers --------------------------------------------------------------

    private static AssertionResult Result(AssertionRule rule, bool passed, string? actual) =>
        new() { Rule = rule, Passed = passed, ActualValue = actual };

    private static AssertionResult Fail(AssertionRule rule, string? actual, string error) =>
        new() { Rule = rule, Passed = false, ActualValue = actual, ErrorMessage = error };

    private static bool StringEquals(string a, string b) =>
        string.Equals(a, b, StringComparison.Ordinal);

    private static bool IsNullToken(string value) =>
        string.Equals(value.Trim(), "null", StringComparison.OrdinalIgnoreCase);

    private static string StripQuotes(string value)
    {
        var v = value.Trim();
        if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') return v[1..^1];
        if (v.Length >= 2 && v[0] == '\'' && v[^1] == '\'') return v[1..^1];
        return v;
    }
}
