using System.Text;
using Kx.Resty.Models;

namespace Kx.Resty.Commands;

/// <summary>
/// Serializes an <see cref="HttpCollection"/> back to a JetBrains-style .http file.
/// </summary>
public static class HttpFileWriter
{
    public static void Write(HttpCollection collection)
    {
        var sb = new StringBuilder();

        // In-place variables block.
        foreach (var v in collection.Variables)
            sb.AppendLine($"@{v.Name} = {v.Value}");

        if (collection.Variables.Count > 0)
            sb.AppendLine();

        // Requests separated by ###, using name after marker when available.
        for (int i = 0; i < collection.Requests.Count; i++)
        {
            var req = collection.Requests[i];
            if (i > 0 || !string.IsNullOrWhiteSpace(req.Name))
            {
                sb.Append("###");
                if (!string.IsNullOrWhiteSpace(req.Name))
                    sb.Append(' ').Append(req.Name!.Trim());
                sb.AppendLine();
                sb.AppendLine();
            }
            WriteRequest(sb, req);
        }

        var dir = Path.GetDirectoryName(collection.FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(collection.FilePath, sb.ToString(), Encoding.UTF8);
        collection.IsDirty = false;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void WriteRequest(StringBuilder sb, HttpRequestEntry req)
    {
        var ann = req.Annotations;

        // Behaviour annotations.
        if (ann.NoLog)          sb.AppendLine("// @no-log");
        if (ann.NoRedirect)     sb.AppendLine("// @no-redirect");
        if (ann.NoCookieJar)    sb.AppendLine("// @no-cookie-jar");
        if (ann.NoAutoEncoding) sb.AppendLine("// @no-auto-encoding");
        if (ann.TimeoutSeconds.HasValue)
            sb.AppendLine($"# @timeout {ann.TimeoutSeconds.Value}");
        if (ann.ConnectionTimeoutSeconds.HasValue)
            sb.AppendLine($"// @connection-timeout {ann.ConnectionTimeoutSeconds.Value}");

        // Request line.
        sb.AppendLine($"{req.Method} {BuildUrlWithParams(req)}");

        // Headers (only enabled ones).
        foreach (var h in req.Headers.Where(h => h.Enabled && !string.IsNullOrEmpty(h.Key)))
            sb.AppendLine($"{h.Key}: {h.Value}");

        // Body.
        if (!string.IsNullOrWhiteSpace(req.BodyFilePath))
        {
            sb.AppendLine();
            sb.AppendLine($"< {req.BodyFilePath}");
        }
        else if (!string.IsNullOrWhiteSpace(req.Body))
        {
            sb.AppendLine();
            sb.AppendLine(req.Body);
        }

        sb.AppendLine();
    }

    private static string BuildUrlWithParams(HttpRequestEntry req)
    {
        var enabledParams = req.QueryParams
            .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key))
            .ToList();
        if (enabledParams.Count == 0)
            return req.Url;

        var sb = new StringBuilder(req.Url);
        var separator = req.Url.Contains('?') ? '&' : '?';
        foreach (var p in enabledParams)
        {
            sb.Append(separator);
            sb.Append(p.Key);
            if (!string.IsNullOrEmpty(p.Value))
                sb.Append('=').Append(p.Value);
            separator = '&';
        }

        return sb.ToString();
    }
}
