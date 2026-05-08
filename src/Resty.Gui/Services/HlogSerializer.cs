using Resty.Gui.Models;

namespace Resty.Gui.Services;

/// <summary>
/// .hlog 格式序列化/反序列化。
///
/// 文件结构：
///   @@meta
///   name: Get User
///   timestamp: 2026-05-07T10:30:45.123Z
///   source: D:\workspace\users.http
///   elapsed: 45ms
///   status: 200
///   error: (可选，仅网络错误时出现)
///
///   @@request
///   GET https://... HTTP/1.1
///   Header: Value
///
///   body text
///
///   @@response
///   HTTP/1.1 200 OK
///   Header: Value
///
///   body text
///
///   @@assertions
///   [PASS] status == 200        (actual: 200)
///   [FAIL] body.$.role == admin (actual: user)
/// </summary>
public static class HlogSerializer
{
    private const string SecMeta       = "@@meta";
    private const string SecRequest    = "@@request";
    private const string SecResponse   = "@@response";
    private const string SecAssertions = "@@assertions";

    // ── 序列化 ───────────────────────────────────────────────────
    public static string Serialize(HistoryRecord record)
    {
        var s = record.Summary;
        var sb = new System.Text.StringBuilder();

        // @@meta
        sb.AppendLine(SecMeta);
        sb.Append("name: ").AppendLine(s.RequestName);
        sb.Append("timestamp: ").AppendLine(s.Timestamp.ToString("O"));
        sb.Append("source: ").AppendLine(s.FilePath);
        sb.Append("elapsed: ").Append(s.ElapsedMs).AppendLine("ms");
        sb.Append("status: ").AppendLine(s.StatusCode.ToString());
        if (s.Error is not null)
            sb.Append("error: ").AppendLine(s.Error);
        sb.AppendLine();

        // @@request
        sb.AppendLine(SecRequest);
        sb.AppendLine(record.RequestSection.TrimEnd());
        sb.AppendLine();

        // @@response
        sb.AppendLine(SecResponse);
        sb.AppendLine(record.ResponseSection.TrimEnd());
        sb.AppendLine();

        // @@assertions（可选）
        if (record.AssertionsSection is not null)
        {
            sb.AppendLine(SecAssertions);
            sb.AppendLine(record.AssertionsSection.TrimEnd());
        }

        return sb.ToString();
    }

    // ── 反序列化 ─────────────────────────────────────────────────
    public static HistoryRecord? Deserialize(string id, string content)
    {
        var sections = SplitSections(content);

        if (!sections.TryGetValue(SecMeta, out var metaText)) return null;
        var meta = ParseMeta(metaText);

        if (!meta.TryGetValue("name",      out var name))      name      = string.Empty;
        if (!meta.TryGetValue("timestamp", out var tsStr))     tsStr     = string.Empty;
        if (!meta.TryGetValue("source",    out var source))    source    = string.Empty;
        if (!meta.TryGetValue("elapsed",   out var elapsedStr)) elapsedStr = "0ms";
        if (!meta.TryGetValue("status",    out var statusStr)) statusStr  = "0";
        meta.TryGetValue("error", out var error);

        var timestamp = DateTime.TryParse(tsStr, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.MinValue;
        var elapsedMs = long.TryParse(elapsedStr.TrimEnd('m', 's'), out var el) ? el : 0;
        var statusCode = int.TryParse(statusStr, out var sc) ? sc : 0;

        // method+url を request 先頭行から取り出す
        sections.TryGetValue(SecRequest,    out var requestSec);
        sections.TryGetValue(SecResponse,   out var responseSec);
        sections.TryGetValue(SecAssertions, out var assertSec);

        var firstLine = (requestSec ?? string.Empty).Split('\n', 2)[0].Trim();
        var parts     = firstLine.Split(' ', 3);
        var method    = parts.Length >= 1 ? parts[0] : "GET";
        var url       = parts.Length >= 2 ? parts[1] : string.Empty;

        var summary = new HistorySummary(id, name, method, url, statusCode, elapsedMs,
                                         timestamp, source, error == "" ? null : error);

        return new HistoryRecord(summary,
                                 requestSec    ?? string.Empty,
                                 responseSec   ?? string.Empty,
                                 string.IsNullOrWhiteSpace(assertSec) ? null : assertSec);
    }

    // ── 辅助：构建 @@request 区块文本 ───────────────────────────
    public static string BuildRequestSection(
        string method, string url,
        IReadOnlyDictionary<string, string> headers,
        string? body)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(method).Append(' ').Append(url).AppendLine(" HTTP/1.1");
        foreach (var (k, v) in headers)
            sb.Append(k).Append(": ").AppendLine(v);
        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }
        return sb.ToString();
    }

    // ── 辅助：构建 @@response 区块文本 ──────────────────────────
    public static string BuildResponseSection(
        int statusCode, string statusText,
        IReadOnlyDictionary<string, string> headers,
        string body, string? error)
    {
        var sb = new System.Text.StringBuilder();
        if (error is not null)
        {
            sb.Append("ERROR: ").AppendLine(error);
            return sb.ToString();
        }
        sb.Append("HTTP/1.1 ").Append(statusCode).Append(' ').AppendLine(statusText);
        foreach (var (k, v) in headers)
            sb.Append(k).Append(": ").AppendLine(v);
        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }
        return sb.ToString();
    }

    // ── 辅助：构建 @@assertions 区块文本 ────────────────────────
    public static string? BuildAssertionsSection(
        IReadOnlyList<Core.Models.AssertionResult>? assertions)
    {
        if (assertions is null or { Count: 0 }) return null;
        var sb = new System.Text.StringBuilder();
        foreach (var a in assertions)
        {
            var mark = a.Passed ? "[PASS]" : "[FAIL]";
            sb.Append(mark).Append(' ').Append(a.Rule.RawText);
            if (a.ActualValue is not null)
                sb.Append(" (actual: ").Append(a.ActualValue).Append(')');
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ── 私有：按 @@ 前缀行切割区块 ─────────────────────────────
    private static Dictionary<string, string> SplitSections(string content)
    {
        var result   = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines    = content.Split('\n');
        string? curKey = null;
        var curLines = new System.Text.StringBuilder();

        void Flush()
        {
            if (curKey is not null)
                result[curKey] = curLines.ToString().Trim();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                Flush();
                curKey   = line.Trim();
                curLines = new System.Text.StringBuilder();
            }
            else
            {
                curLines.AppendLine(line);
            }
        }
        Flush();
        return result;
    }

    private static Dictionary<string, string> ParseMeta(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            var idx  = line.IndexOf(": ", StringComparison.Ordinal);
            if (idx < 0) continue;
            result[line[..idx].Trim()] = line[(idx + 2)..].Trim();
        }
        return result;
    }
}
