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

        // Requests separated by ###.
        for (int i = 0; i < collection.Requests.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine("###");
                sb.AppendLine();
            }
            WriteRequest(sb, collection.Requests[i]);
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

        // Name comment.
        if (!string.IsNullOrWhiteSpace(req.Name))
            sb.AppendLine($"# @name {req.Name}");

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
        sb.AppendLine($"{req.Method} {req.Url}");

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
}
