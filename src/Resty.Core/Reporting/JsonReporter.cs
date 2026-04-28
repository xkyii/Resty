using System.Text.Json;
using Resty.Core.Models;

namespace Resty.Core.Reporting;

/// <summary>Produces a machine-readable JSON report.</summary>
public sealed class JsonReporter : IReporter
{
    public Task WriteAsync(IReadOnlyList<RequestExecutionResult> results, TextWriter output)
    {
        var total = results.Count;
        var passed = results.Count(r => r.IsSuccess);
        var totalAsserts = results.Sum(r => r.AssertionResults.Count);
        var passedAsserts = results.Sum(r => r.AssertionResults.Count(a => a.Passed));

        var opts = new JsonWriterOptions { Indented = true };
        using var ms = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, opts))
        {
            writer.WriteStartObject();

            writer.WritePropertyName("summary");
            writer.WriteStartObject();
            writer.WriteNumber("total", total);
            writer.WriteNumber("passed", passed);
            writer.WriteNumber("failed", total - passed);
            writer.WriteNumber("totalAssertions", totalAsserts);
            writer.WriteNumber("passedAssertions", passedAsserts);
            writer.WriteNumber("failedAssertions", totalAsserts - passedAsserts);
            writer.WriteEndObject();

            writer.WritePropertyName("results");
            writer.WriteStartArray();
            foreach (var result in results)
                WriteResult(writer, result);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        output.Write(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
        return Task.CompletedTask;
    }

    private static void WriteResult(Utf8JsonWriter w, RequestExecutionResult result)
    {
        w.WriteStartObject();
        w.WriteString("name", result.Request.Name);
        w.WriteString("method", result.Request.Method);
        w.WriteString("url", result.Request.Url);

        if (result.Response is { } res)
        {
            w.WriteNumber("statusCode", res.StatusCode);
            w.WriteNumber("elapsedMs", res.ElapsedMs);
            if (res.Error is not null)
                w.WriteString("error", res.Error);
        }

        w.WriteBoolean("passed", result.IsSuccess);

        w.WritePropertyName("assertions");
        w.WriteStartArray();
        foreach (var ar in result.AssertionResults)
        {
            w.WriteStartObject();
            w.WriteString("rule", ar.Rule.RawText);
            w.WriteBoolean("passed", ar.Passed);
            if (ar.ActualValue is not null) w.WriteString("actual", ar.ActualValue);
            if (ar.ErrorMessage is not null) w.WriteString("error", ar.ErrorMessage);
            w.WriteEndObject();
        }
        w.WriteEndArray();

        w.WriteEndObject();
    }
}
