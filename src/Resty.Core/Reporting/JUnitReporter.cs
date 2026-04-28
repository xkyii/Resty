using System.Text;
using Resty.Core.Models;

namespace Resty.Core.Reporting;

/// <summary>Produces JUnit XML compatible with GitHub Actions / GitLab CI / Jenkins.</summary>
public sealed class JUnitReporter : IReporter
{
    public Task WriteAsync(IReadOnlyList<RequestExecutionResult> results, TextWriter output)
    {
        var total = results.Count;
        var failures = results.Count(r => !r.AllAssertionsPassed);
        var errors = results.Count(r => r.HasTransportError);
        var totalTimeSeconds = results.Sum(r => r.Response?.ElapsedMs ?? 0) / 1000.0;

        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine($"""<testsuites tests="{total}" failures="{failures}" errors="{errors}" time="{totalTimeSeconds:F3}">""");
        sb.AppendLine($"""  <testsuite name="resty" tests="{total}" failures="{failures}" errors="{errors}" time="{totalTimeSeconds:F3}">""");

        foreach (var result in results)
        {
            var req = result.Request;
            var name = Xml(string.IsNullOrEmpty(req.Name) ? $"{req.Method} {req.Url}" : req.Name);
            var classname = Xml(req.Url);
            var time = (result.Response?.ElapsedMs ?? 0) / 1000.0;

            if (result.HasTransportError)
            {
                sb.AppendLine($"""    <testcase name="{name}" classname="{classname}" time="{time:F3}">""");
                sb.AppendLine($"""      <error message="{Xml(result.Response?.Error ?? "Transport error")}" />""");
                sb.AppendLine("    </testcase>");
                continue;
            }

            var failedAssertions = result.AssertionResults.Where(a => !a.Passed).ToList();

            if (failedAssertions.Count == 0)
            {
                sb.AppendLine($"""    <testcase name="{name}" classname="{classname}" time="{time:F3}" />""");
            }
            else
            {
                sb.AppendLine($"""    <testcase name="{name}" classname="{classname}" time="{time:F3}">""");
                foreach (var fa in failedAssertions)
                {
                    var msg = Xml(fa.Rule.RawText);
                    var detail = new StringBuilder();
                    if (fa.ActualValue is not null) detail.AppendLine($"Actual: {fa.ActualValue}");
                    if (fa.ErrorMessage is not null) detail.AppendLine($"Error: {fa.ErrorMessage}");
                    sb.AppendLine($"""      <failure message="{msg}">{Xml(detail.ToString().Trim())}</failure>""");
                }
                sb.AppendLine("    </testcase>");
            }
        }

        sb.AppendLine("  </testsuite>");
        sb.AppendLine("</testsuites>");

        output.Write(sb.ToString());
        return Task.CompletedTask;
    }

    private static string Xml(string? value) =>
        (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
}
