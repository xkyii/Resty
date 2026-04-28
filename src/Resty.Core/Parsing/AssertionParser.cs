using Resty.Core.Models;

namespace Resty.Core.Parsing;

/// <summary>
/// Parses JetBrains HTTP assertion blocks (> {% ... %}) into AssertionRule list.
/// Syntax:  assert &lt;subject&gt; &lt;operator&gt; &lt;value&gt;
/// </summary>
public static class AssertionParser
{
    public static List<AssertionRule> ParseBlock(IReadOnlyList<string> lines)
    {
        var rules = new List<AssertionRule>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("assert ", StringComparison.OrdinalIgnoreCase))
            {
                var rule = ParseLine(trimmed);
                if (rule is not null) rules.Add(rule);
            }
        }
        return rules;
    }

    private static AssertionRule? ParseLine(string line)
    {
        // Strip "assert "
        var body = line["assert ".Length..].TrimStart();
        if (body.Length == 0) return null;

        // --- Parse subject ---
        AssertionSubjectType subjectType;
        string? subjectPath = null;
        int subjectEnd;

        if (body.StartsWith("status", StringComparison.OrdinalIgnoreCase)
            && (body.Length == 6 || !char.IsLetterOrDigit(body[6])))
        {
            subjectType = AssertionSubjectType.Status;
            subjectEnd = 6;
        }
        else if (body.StartsWith("responseTime", StringComparison.OrdinalIgnoreCase)
            && (body.Length == 12 || !char.IsLetterOrDigit(body[12])))
        {
            subjectType = AssertionSubjectType.ResponseTime;
            subjectEnd = 12;
        }
        else if (body.StartsWith("body.", StringComparison.OrdinalIgnoreCase))
        {
            subjectType = AssertionSubjectType.BodyJsonPath;
            subjectEnd = FindTokenEnd(body, "body.".Length);
            // "body.$.name" → subjectPath = "$.name"
            subjectPath = body["body.".Length..subjectEnd];
        }
        else if (body.StartsWith("header[", StringComparison.OrdinalIgnoreCase))
        {
            subjectType = AssertionSubjectType.Header;
            var closeIdx = body.IndexOf(']');
            if (closeIdx < 0) return null;
            subjectPath = body[7..closeIdx].Trim('"', '\'', ' ');
            subjectEnd = closeIdx + 1;
        }
        else
        {
            return null;
        }

        var remaining = body[subjectEnd..].TrimStart();

        // --- Parse operator ---
        AssertionOperator op;
        int opLen;

        if (remaining.StartsWith("==")) { op = AssertionOperator.Equal; opLen = 2; }
        else if (remaining.StartsWith("!=")) { op = AssertionOperator.NotEqual; opLen = 2; }
        else if (remaining.StartsWith("<=")) { op = AssertionOperator.LessThanOrEqual; opLen = 2; }
        else if (remaining.StartsWith(">=")) { op = AssertionOperator.GreaterThanOrEqual; opLen = 2; }
        else if (remaining.StartsWith('<')) { op = AssertionOperator.LessThan; opLen = 1; }
        else if (remaining.StartsWith('>')) { op = AssertionOperator.GreaterThan; opLen = 1; }
        else if (remaining.StartsWith("in") && remaining.Length > 2 && !char.IsLetterOrDigit(remaining[2]))
        { op = AssertionOperator.In; opLen = 2; }
        else if (remaining.StartsWith("contains") && remaining.Length > 8 && !char.IsLetterOrDigit(remaining[8]))
        { op = AssertionOperator.Contains; opLen = 8; }
        else return null;

        var expectedValue = remaining[opLen..].Trim();

        return new AssertionRule
        {
            RawText = line,
            SubjectType = subjectType,
            SubjectPath = subjectPath,
            Operator = op,
            ExpectedValue = expectedValue,
        };
    }

    /// <summary>Finds the end index of the current token (stops at first whitespace).</summary>
    private static int FindTokenEnd(string s, int startIdx)
    {
        for (var i = startIdx; i < s.Length; i++)
            if (s[i] == ' ' || s[i] == '\t') return i;
        return s.Length;
    }
}
