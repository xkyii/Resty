using Resty.Core.Models;

namespace Resty.Core.Parsing;

/// <summary>
/// Parses JetBrains HTTP (.http) files into <see cref="HttpFileDefinition"/>.
/// Supports: request separators (###), file-level @variables, headers, bodies,
/// and assertion blocks (&gt; {%...%}).
/// </summary>
public static class HttpFileParser
{
    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "CONNECT", "TRACE",
    };

    public static HttpFileDefinition Parse(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return ParseContent(content, filePath);
    }

    public static HttpFileDefinition ParseContent(string content, string filePath = "")
    {
        var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var fileVars = new Dictionary<string, string>(StringComparer.Ordinal);
        var requests = new List<HttpRequestDefinition>();

        // ---- mutable builder state ----
        string? currentName = null;
        string? currentMethod = null;
        string? currentUrl = null;
        var currentHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var currentBodyLines = new List<string>();
        var currentAssertLines = new List<string>();

        var state = ParseState.Preamble;

        void FinalizeRequest()
        {
            if (currentMethod is null) return;

            var bodyText = string.Join("\n", currentBodyLines).Trim();
            requests.Add(new HttpRequestDefinition
            {
                Name = currentName ?? string.Empty,
                Method = currentMethod,
                Url = currentUrl ?? string.Empty,
                Headers = new Dictionary<string, string>(currentHeaders, StringComparer.OrdinalIgnoreCase),
                Body = bodyText.Length > 0 ? bodyText : null,
                Assertions = AssertionParser.ParseBlock(currentAssertLines),
            });

            currentMethod = null;
            currentUrl = null;
            currentHeaders.Clear();
            currentBodyLines.Clear();
            currentAssertLines.Clear();
        }

        foreach (var rawLine in lines)
        {
            // Request separator always takes priority
            if (rawLine.TrimStart().StartsWith("###"))
            {
                FinalizeRequest();
                var nameText = rawLine.TrimStart();
                currentName = nameText.Length > 3 ? nameText[3..].Trim() : null;
                if (string.IsNullOrEmpty(currentName)) currentName = null;
                state = ParseState.RequestLine;
                continue;
            }

            switch (state)
            {
                case ParseState.Preamble:
                    HandlePreambleLine(rawLine, fileVars, ref currentMethod, ref currentUrl, ref state);
                    break;

                case ParseState.RequestLine:
                    if (!string.IsNullOrWhiteSpace(rawLine) && !rawLine.TrimStart().StartsWith("//"))
                        TryParseRequestLine(rawLine, ref currentMethod, ref currentUrl, ref state);
                    break;

                case ParseState.Headers:
                    if (string.IsNullOrWhiteSpace(rawLine))
                    {
                        state = ParseState.Body;
                    }
                    else
                    {
                        var colonIdx = rawLine.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            var name = rawLine[..colonIdx].Trim();
                            var value = rawLine[(colonIdx + 1)..].Trim();
                            currentHeaders[name] = value;
                        }
                    }
                    break;

                case ParseState.Body:
                    var trimmedBody = rawLine.TrimStart();
                    if (trimmedBody.StartsWith("> {%"))
                        state = ParseState.Assertion;
                    else
                        currentBodyLines.Add(rawLine);
                    break;

                case ParseState.Assertion:
                    if (rawLine.TrimStart() == "%}")
                        state = ParseState.AfterAssertion;
                    else
                        currentAssertLines.Add(rawLine.Trim());
                    break;

                case ParseState.AfterAssertion:
                    // Waiting for next ### or EOF — nothing to collect
                    break;
            }
        }

        FinalizeRequest();

        return new HttpFileDefinition
        {
            FilePath = filePath,
            Requests = requests,
            FileVariables = fileVars,
        };
    }

    // -------------------------------------------------------------------------

    private static void HandlePreambleLine(
        string line,
        Dictionary<string, string> fileVars,
        ref string? method,
        ref string? url,
        ref ParseState state)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//") || string.IsNullOrWhiteSpace(trimmed))
            return;

        if (trimmed.StartsWith('@'))
        {
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx > 1)
            {
                var varName = trimmed[1..eqIdx].Trim();
                var varValue = trimmed[(eqIdx + 1)..].Trim();
                fileVars[varName] = varValue;
            }
            return;
        }

        // Could be a bare request line (file with no ### header)
        TryParseRequestLine(trimmed, ref method, ref url, ref state);
    }

    private static void TryParseRequestLine(
        string line,
        ref string? method,
        ref string? url,
        ref ParseState state)
    {
        var trimmed = line.TrimStart();
        var spaceIdx = trimmed.IndexOf(' ');
        if (spaceIdx <= 0) return;

        var candidate = trimmed[..spaceIdx];
        if (!HttpMethods.Contains(candidate)) return;

        method = candidate.ToUpperInvariant();

        // Strip optional HTTP/version at end: "GET https://host/path HTTP/1.1"
        var rest = trimmed[(spaceIdx + 1)..].TrimStart();
        var urlEnd = rest.IndexOf(' ');
        url = urlEnd < 0 ? rest : rest[..urlEnd];

        state = ParseState.Headers;
    }

    private enum ParseState
    {
        Preamble,
        RequestLine,
        Headers,
        Body,
        Assertion,
        AfterAssertion,
    }
}
