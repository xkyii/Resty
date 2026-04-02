using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kx.Resty.Features.Workspace.Models;

namespace Kx.Resty.Features.Workspace.Services;

public static class HttpFileParser
{
    private static readonly Regex MethodLineRegex =
        new("^(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS|TRACE|CONNECT)\\s+(.+?)(?:\\s+HTTP/\\d(?:\\.\\d)?)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VariableRegex =
        new("^@([A-Za-z0-9_.-]+)\\s*=\\s*(.*)$", RegexOptions.Compiled);

    public static ParsedHttpCollection ParseCollection(string workspaceRoot, string filePath)
    {
        var text = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
        var segments = SplitSegments(text);
        var fileVars = ParseFileVariables(text);
        var requests = new List<ParsedHttpRequest>();

        for (var i = 0; i < segments.Count; i++)
        {
            var parsed = ParseRequestSegment(segments[i], filePath, i);
            if (parsed is not null)
                requests.Add(parsed);
        }

        return new ParsedHttpCollection
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            RelativePath = Path.GetRelativePath(workspaceRoot, filePath),
            Requests = requests,
            FileVariables = fileVars
        };
    }

    public static bool TrySaveRequestBlock(string filePath, int segmentIndex, ParsedHttpRequest request)
    {
        if (!File.Exists(filePath))
            return false;

        var content = File.ReadAllText(filePath);
        var segments = SplitSegments(content);
        if (segmentIndex < 0 || segmentIndex >= segments.Count)
            return false;

        segments[segmentIndex] = RenderRequestSegment(request);
        File.WriteAllText(filePath, string.Concat(segments));
        return true;
    }

    private static string RenderRequestSegment(ParsedHttpRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("### ").AppendLine(request.Name);
        sb.Append(request.Method).Append(' ').AppendLine(request.Url);

        if (!string.IsNullOrWhiteSpace(request.HeadersText))
        {
            foreach (var line in request.HeadersText.Replace("\r\n", "\n").Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    sb.AppendLine(line);
        }

        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(request.BodyText))
            sb.AppendLine(request.BodyText.Replace("\r\n", "\n"));

        return sb.ToString();
    }

    private static List<string> SplitSegments(string content)
    {
        if (string.IsNullOrEmpty(content))
            return [string.Empty];

        var normalized = content.Replace("\r\n", "\n");
        var matches = Regex.Matches(normalized, "(?m)^###.*$");
        if (matches.Count == 0)
            return [normalized];

        var segments = new List<string>();
        var firstStart = matches[0].Index;
        if (firstStart > 0)
            segments.Add(normalized[..firstStart]);

        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : normalized.Length;
            segments.Add(normalized[start..end]);
        }

        return segments;
    }

    private static ParsedHttpRequest? ParseRequestSegment(string segment, string filePath, int segmentIndex)
    {
        var lines = segment.Replace("\r\n", "\n").Split('\n');
        var title = string.Empty;
        var noLog = false;
        int? timeoutMs = null;

        foreach (var line in lines)
        {
            var trim = line.Trim();
            if (trim.StartsWith("###"))
            {
                var v = trim.TrimStart('#').Trim();
                if (!string.IsNullOrWhiteSpace(v))
                    title = v;
            }
            else if (trim.StartsWith("# @name", StringComparison.OrdinalIgnoreCase))
            {
                var idx = trim.IndexOf(' ');
                if (idx >= 0)
                {
                    var n = trim[(idx + 1)..].Trim();
                    if (!string.IsNullOrWhiteSpace(n))
                        title = n;
                }
            }
            else if (trim.Contains("@no-log", StringComparison.OrdinalIgnoreCase))
            {
                noLog = true;
            }
            else if (trim.StartsWith("# @timeout", StringComparison.OrdinalIgnoreCase))
            {
                var n = new string(trim.Where(char.IsDigit).ToArray());
                if (int.TryParse(n, out var t))
                    timeoutMs = t;
            }
        }

        var requestLineIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var trim = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(trim) || trim.StartsWith("#") || trim.StartsWith("//") || trim.StartsWith("###") || trim.StartsWith("@"))
                continue;

            if (MethodLineRegex.IsMatch(trim) || trim.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trim.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                requestLineIndex = i;
                break;
            }
        }

        if (requestLineIndex < 0)
            return null;

        var requestLine = lines[requestLineIndex].Trim();
        string method;
        string url;
        var methodMatch = MethodLineRegex.Match(requestLine);
        if (methodMatch.Success)
        {
            method = methodMatch.Groups[1].Value.ToUpperInvariant();
            url = methodMatch.Groups[2].Value.Trim();
        }
        else
        {
            method = "GET";
            url = requestLine;
        }

        var headers = new List<string>();
        var body = new StringBuilder();
        var hitBody = false;

        for (var i = requestLineIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!hitBody)
            {
                if (string.IsNullOrWhiteSpace(line)) { hitBody = true; continue; }
                if (!line.TrimStart().StartsWith("#") && line.Contains(':'))
                    headers.Add(line.TrimEnd());
            }
            else
            {
                body.AppendLine(line);
            }
        }

        var requestName = string.IsNullOrWhiteSpace(title)
            ? $"{method} {TryExtractPath(url)}"
            : title;

        return new ParsedHttpRequest
        {
            Id = $"{filePath}::{segmentIndex}::{requestName}",
            Name = requestName,
            Method = method,
            Url = url,
            HeadersText = string.Join("\n", headers),
            BodyText = body.ToString().TrimEnd('\n', '\r'),
            SegmentIndex = segmentIndex,
            NoLog = noLog,
            TimeoutMs = timeoutMs
        };
    }

    private static Dictionary<string, string> ParseFileVariables(string text)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var m = VariableRegex.Match(line.Trim());
            if (m.Success)
                vars[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        }
        return vars;
    }

    private static string TryExtractPath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;
        return string.IsNullOrWhiteSpace(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
    }
}
