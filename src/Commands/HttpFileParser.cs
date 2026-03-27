using System.Text.RegularExpressions;
using Kx.Resty.Models;

namespace Kx.Resty.Commands;

/// <summary>
/// Parses a JetBrains-style .http file into an <see cref="HttpCollection"/>.
/// Supports: in-place variables (@name = value), ### separators,
/// request annotations (@no-log, @no-redirect, …), multiline URLs,
/// file-body references (&lt; ./path), and response-handler markers.
/// </summary>
public static class HttpFileParser
{
    private static readonly string[] KnownMethods =
        ["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"];

    // ─── Public API ───────────────────────────────────────────────────────────

    public static HttpCollection Parse(string filePath)
    {
        var col = new HttpCollection
        {
            FilePath = filePath,
            Name     = Path.GetFileNameWithoutExtension(filePath)
        };
        if (File.Exists(filePath))
            ParseInto(col, File.ReadAllText(filePath));
        return col;
    }

    public static void ParseInto(HttpCollection col, string text)
    {
        col.Variables.Clear();
        col.Requests.Clear();

        var lines = text.ReplaceLineEndings("\n").Split('\n').ToList();

        // Pass 1 – collect all @var = value lines as in-place variables.
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (t.StartsWith('@') && t.Contains('='))
                col.Variables.Add(ParseVariable(t));
        }

        // Pass 2 – split by ### and parse each request block.
        foreach (var (name, blockLines) in SplitBlocks(lines))
        {
            var entry = ParseBlock(name, blockLines);
            if (entry is not null)
                col.Requests.Add(entry);
        }
    }

    // ─── Block splitting ──────────────────────────────────────────────────────

    private record struct Block(string? Name, List<string> Lines);

    private static IEnumerable<Block> SplitBlocks(List<string> lines)
    {
        string?      pendingName = null;
        List<string> current     = [];

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("###"))
            {
                if (HasRequestContent(current))
                    yield return new Block(pendingName, current);

                current     = [];
                var after   = line.TrimStart()[3..].Trim();
                pendingName = string.IsNullOrEmpty(after) ? null : after;
            }
            else
            {
                current.Add(line);
            }
        }

        if (HasRequestContent(current))
            yield return new Block(pendingName, current);
    }

    private static bool HasRequestContent(List<string> lines) =>
        lines.Any(l => IsRequestLine(l.Trim()));

    // ─── Block parsing ────────────────────────────────────────────────────────

    private static HttpRequestEntry? ParseBlock(string? blockName, List<string> lines)
    {
        var entry       = new HttpRequestEntry();
        var annotations = entry.Annotations;
        string? name    = blockName;
        int i           = 0;

        // 1. Read leading annotations / comments / in-place vars.
        while (i < lines.Count)
        {
            var t = lines[i].Trim();
            if (string.IsNullOrEmpty(t))  { i++; continue; }
            if (t.StartsWith('@') && t.Contains('=')) { i++; continue; } // in-place var, skip

            if (t.StartsWith("//") || t.StartsWith('#'))
            {
                if      (TryParseName(t, out var n))               name = n;
                else if (t.Contains("@no-redirect",   StringComparison.OrdinalIgnoreCase)) annotations.NoRedirect     = true;
                else if (t.Contains("@no-log",        StringComparison.OrdinalIgnoreCase)) annotations.NoLog          = true;
                else if (t.Contains("@no-cookie-jar", StringComparison.OrdinalIgnoreCase)) annotations.NoCookieJar    = true;
                else if (t.Contains("@no-auto-encoding", StringComparison.OrdinalIgnoreCase)) annotations.NoAutoEncoding = true;
                else if (TryParseTimeout(t, out var to))           annotations.TimeoutSeconds            = to;
                else if (TryParseConnTimeout(t, out var ct))       annotations.ConnectionTimeoutSeconds  = ct;
                i++;
                continue;
            }

            break; // must be the request line
        }

        if (i >= lines.Count) return null;

        // 2. Parse the request line (may span multiple indented continuation lines).
        var requestLine = CollectRequestLine(lines, ref i);
        if (!ParseRequestLine(requestLine, entry)) return null;
        entry.Name = name;

        // 3. Headers – until the first blank line.
        while (i < lines.Count)
        {
            var t = lines[i].Trim();
            if (string.IsNullOrEmpty(t)) { i++; break; }
            if (t.StartsWith("//") || t.StartsWith('#')) { i++; continue; }

            var colon = t.IndexOf(':');
            if (colon > 0)
                entry.Headers.Add(new NamedValue
                {
                    Key   = t[..colon].Trim(),
                    Value = t[(colon + 1)..].Trim()
                });
            i++;
        }

        // 4. Body (or file reference).
        var bodyLines = new List<string>();
        while (i < lines.Count)
        {
            var t = lines[i].Trim();

            // Response handler / redirect markers end the body section.
            if (t.StartsWith("> ") || t.StartsWith(">>")) break;

            // File body reference: < ./path
            if (t.StartsWith("< ") && bodyLines.Count == 0)
            {
                entry.BodyFilePath = t[2..].Trim();
                break;
            }

            bodyLines.Add(lines[i]);
            i++;
        }

        entry.Body = string.Join("\n", bodyLines).Trim();
        return entry;
    }

    /// <summary>Joins continuation lines (indented) that belong to the same request line.</summary>
    private static string CollectRequestLine(List<string> lines, ref int i)
    {
        var parts = new List<string>();
        parts.Add(lines[i++].Trim());

        // Continuation if next line is indented (URL folding).
        while (i < lines.Count)
        {
            var raw = lines[i];
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t') && !string.IsNullOrWhiteSpace(raw))
            {
                parts.Add(raw.Trim());
                i++;
            }
            else break;
        }

        return string.Concat(parts);
    }

    // ─── Request line parsing ─────────────────────────────────────────────────

    private static bool ParseRequestLine(string line, HttpRequestEntry entry)
    {
        if (string.IsNullOrEmpty(line)) return false;

        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        if (KnownMethods.Contains(parts[0].ToUpperInvariant()))
        {
            entry.Method = parts[0].ToUpperInvariant();
            var url = parts.Length > 1 ? parts[1] : string.Empty;
            // Strip optional HTTP/1.1 suffix.
            var vi = url.LastIndexOf(" HTTP/", StringComparison.OrdinalIgnoreCase);
            entry.Url = vi >= 0 ? url[..vi].Trim() : url.Trim();
            return true;
        }

        // GET shorthand – bare URL.
        if (parts[0].StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
            parts[0].StartsWith("{{"))
        {
            entry.Method = "GET";
            entry.Url    = parts[0].Trim();
            return true;
        }

        return false;
    }

    private static bool IsRequestLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        var first = line.Split(' ')[0];
        return KnownMethods.Contains(first.ToUpperInvariant()) ||
               line.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("{{");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static InPlaceVariable ParseVariable(string line)
    {
        var eq = line.IndexOf('=');
        return new InPlaceVariable
        {
            Name  = line[1..eq].Trim(), // skip '@'
            Value = line[(eq + 1)..].Trim()
        };
    }

    private static bool TryParseName(string line,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? name)
    {
        name = null;
        foreach (var prefix in new[] { "# @name =", "# @name", "// @name =", "// @name" })
        {
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            name = line[prefix.Length..].Trim().TrimStart('=').Trim();
            return !string.IsNullOrEmpty(name);
        }
        return false;
    }

    private static bool TryParseTimeout(string line, out int? seconds)
    {
        seconds = null;
        var m = Regex.Match(line, @"@timeout\s+(\d+)\s*(ms|s|m)?",
                            RegexOptions.IgnoreCase);
        if (!m.Success) return false;
        seconds = ToSeconds(int.Parse(m.Groups[1].Value), m.Groups[2].Value);
        return true;
    }

    private static bool TryParseConnTimeout(string line, out int? seconds)
    {
        seconds = null;
        var m = Regex.Match(line, @"@connection-timeout\s+(\d+)\s*(ms|s|m)?",
                            RegexOptions.IgnoreCase);
        if (!m.Success) return false;
        seconds = ToSeconds(int.Parse(m.Groups[1].Value), m.Groups[2].Value);
        return true;
    }

    private static int ToSeconds(int val, string unit) => unit.ToLowerInvariant() switch
    {
        "ms" => Math.Max(1, val / 1000),
        "m"  => val * 60,
        _    => val
    };
}
