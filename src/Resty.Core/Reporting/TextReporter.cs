using Resty.Core.Models;

namespace Resty.Core.Reporting;

/// <summary>Human-readable console report with optional ANSI colour.</summary>
public sealed class TextReporter : IReporter
{
    private readonly bool _useColor;
    private readonly bool _verbose;

    public TextReporter(bool useColor = true, bool verbose = false)
    {
        _useColor = useColor;
        _verbose = verbose;
    }

    public Task WriteAsync(IReadOnlyList<RequestExecutionResult> results, TextWriter output)
    {
        foreach (var result in results)
            WriteResult(result, output);

        WriteSummary(results, output);
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------

    private void WriteResult(RequestExecutionResult result, TextWriter out_)
    {
        var req = result.Request;
        var res = result.Response;

        out_.WriteLine();
        out_.WriteLine(new string('─', 60));

        if (res is null || !res.IsSuccess)
        {
            var errorMsg = res?.Error ?? "No response";
            out_.WriteLine($"{Color(Red, "✗")} {Bold(req.Method)} {req.Url}");
            out_.WriteLine($"  {Color(Red, "Error:")} {errorMsg}");
            return;
        }

        var statusColor = res.StatusCode < 300 ? Green
                        : res.StatusCode < 500 ? Yellow
                        : Red;
        var passIcon = result.AllAssertionsPassed ? Color(Green, "✓") : Color(Red, "✗");

        // Request line
        out_.WriteLine($"{passIcon} {Bold($"{req.Method} {req.Url}")}");

        if (!string.IsNullOrEmpty(req.Name))
            out_.WriteLine($"  {Dim(req.Name)}");

        // Status + timing
        out_.WriteLine(
            $"  {Color(statusColor, $"HTTP {res.StatusCode}")}  " +
            $"{Dim($"{res.ElapsedMs}ms")}  " +
            $"{Dim(FormatBytes(res.Body.Length))}");

        // Response headers (verbose)
        if (_verbose)
        {
            out_.WriteLine();
            foreach (var (k, v) in res.Headers)
                out_.WriteLine($"  {Dim(k + ":")} {v}");
        }

        // Response body
        out_.WriteLine();
        out_.WriteLine(res.Body.Length <= 4096
            ? res.Body
            : res.Body[..4096] + $"\n{Dim("... (truncated)")}");

        // Assertions
        if (result.AssertionResults.Count > 0)
        {
            out_.WriteLine();
            foreach (var ar in result.AssertionResults)
            {
                if (ar.Passed)
                {
                    out_.WriteLine($"  {Color(Green, "✓")} {ar.Rule.RawText}");
                }
                else
                {
                    out_.WriteLine($"  {Color(Red, "✗")} {ar.Rule.RawText}");
                    if (ar.ActualValue is not null)
                        out_.WriteLine($"    {Dim("actual:")} {ar.ActualValue}");
                    if (ar.ErrorMessage is not null)
                        out_.WriteLine($"    {Dim("error:")} {ar.ErrorMessage}");
                }
            }
        }
    }

    private void WriteSummary(IReadOnlyList<RequestExecutionResult> results, TextWriter out_)
    {
        var total = results.Count;
        var passed = results.Count(r => r.IsSuccess);
        var failed = total - passed;
        var totalAsserts = results.Sum(r => r.AssertionResults.Count);
        var passedAsserts = results.Sum(r => r.AssertionResults.Count(a => a.Passed));

        out_.WriteLine();
        out_.WriteLine(new string('─', 60));

        var summaryColor = failed == 0 ? Green : Red;
        out_.WriteLine(Color(summaryColor,
            $"Results: {passed}/{total} passed" +
            (totalAsserts > 0 ? $"  |  Assertions: {passedAsserts}/{totalAsserts}" : string.Empty)));
    }

    // ---- Colour helpers -------------------------------------------------------

    private const string Reset = "\x1b[0m";
    private const string Green = "\x1b[32m";
    private const string Red = "\x1b[31m";
    private const string Yellow = "\x1b[33m";
    private const string BoldOn = "\x1b[1m";
    private const string DimOn = "\x1b[2m";

    private string Color(string code, string text) =>
        _useColor ? $"{code}{text}{Reset}" : text;

    private string Bold(string text) =>
        _useColor ? $"{BoldOn}{text}{Reset}" : text;

    private string Dim(string text) =>
        _useColor ? $"{DimOn}{text}{Reset}" : text;

    private static string FormatBytes(int len) =>
        len < 1024 ? $"{len} B" : $"{len / 1024.0:F1} KB";
}
