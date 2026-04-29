using System.Text;
using Resty.Core.Models;

namespace Resty.Core.Parsing;

/// <summary>
/// P16 cURL ↔ HttpRequestDefinition 互转。
/// 支持常见 curl 选项：-X, -H, -d/--data/--data-raw, -u/--user, --json。
/// </summary>
public static class CurlConverter
{
    // ── 导出 ─────────────────────────────────────────────────────

    /// <summary>将请求序列化为 curl 命令字符串。</summary>
    public static string Export(HttpRequestDefinition req)
    {
        var sb = new StringBuilder("curl");

        // 方法（GET 时省略）
        if (!string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase))
            sb.Append($" -X {req.Method.ToUpperInvariant()}");

        // URL
        sb.Append($" \"{req.Url}\"");

        // Headers（跳过 Content-Type 如果 body 不存在）
        foreach (var (k, v) in req.Headers)
            sb.Append($" -H \"{EscapeQuote(k)}: {EscapeQuote(v)}\"");

        // Body
        if (!string.IsNullOrEmpty(req.Body))
            sb.Append($" -d \"{EscapeQuote(req.Body)}\"");

        return sb.ToString();
    }

    // ── 导入 ─────────────────────────────────────────────────────

    /// <summary>从 curl 命令字符串解析为请求定义。</summary>
    public static bool TryImport(string curlCommand, out HttpRequestDefinition result)
    {
        result = new HttpRequestDefinition { Method = "GET", Url = string.Empty };
        try
        {
            var tokens = Tokenize(curlCommand.Trim());
            if (tokens.Count == 0) return false;

            // 跳过可能的 "curl" 开头
            int i = 0;
            if (tokens[i].Equals("curl", StringComparison.OrdinalIgnoreCase)) i++;
            if (i >= tokens.Count) return false;

            string method = "GET";
            string url    = string.Empty;
            var    headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? body   = null;

            while (i < tokens.Count)
            {
                var tok = tokens[i];
                if (tok == "-X" || tok == "--request")
                {
                    if (++i < tokens.Count) method = tokens[i].ToUpperInvariant();
                }
                else if (tok == "-H" || tok == "--header")
                {
                    if (++i < tokens.Count)
                    {
                        var hdr = tokens[i];
                        var sep = hdr.IndexOf(':');
                        if (sep > 0)
                        {
                            var hk = hdr[..sep].Trim();
                            var hv = hdr[(sep + 1)..].Trim();
                            headers[hk] = hv;
                        }
                    }
                }
                else if (tok == "-d" || tok == "--data" || tok == "--data-raw" || tok == "--data-binary" || tok == "--json")
                {
                    if (++i < tokens.Count)
                    {
                        body = tokens[i];
                        // --json 隐含 Content-Type
                        if (tok == "--json")
                            headers.TryAdd("Content-Type", "application/json");
                    }
                    // 有 body 默认 POST（如果没显式指定方法）
                    if (method == "GET") method = "POST";
                }
                else if (tok == "-u" || tok == "--user")
                {
                    if (++i < tokens.Count)
                    {
                        var creds = tokens[i];
                        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(creds));
                        headers["Authorization"] = $"Basic {encoded}";
                    }
                }
                else if (tok == "-L" || tok == "--location" ||
                         tok == "-s" || tok == "--silent" ||
                         tok == "-v" || tok == "--verbose" ||
                         tok == "-k" || tok == "--insecure" ||
                         tok == "-i" || tok == "--include" ||
                         tok == "-I" || tok == "--head" ||
                         tok == "--compressed")
                {
                    // 忽略不影响请求结构的选项
                    if (tok == "-I" || tok == "--head") method = "HEAD";
                }
                else if (tok == "--proxy" || tok == "-x" || tok == "--user-agent" ||
                         tok == "-A" || tok == "--connect-timeout" || tok == "--max-time" ||
                         tok == "-m" || tok == "-o" || tok == "--output" ||
                         tok == "--cert" || tok == "--key")
                {
                    // 带参数的忽略选项，跳过下一个 token
                    i++;
                }
                else if (!tok.StartsWith('-'))
                {
                    // 非标志 token → URL
                    if (string.IsNullOrEmpty(url)) url = tok;
                }
                i++;
            }

            if (string.IsNullOrEmpty(url)) return false;

            result = new HttpRequestDefinition
            {
                Method  = method,
                Url     = url,
                Headers = headers,
                Body    = body,
            };
            return true;
        }
        catch { return false; }
    }

    // ── 分词器 ────────────────────────────────────────────────────
    /// <summary>
    /// 将命令行字符串拆分为 token 列表，正确处理单引号、双引号和反斜杠转义。
    /// </summary>
    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var sb     = new StringBuilder();
        int i      = 0;

        while (i < input.Length)
        {
            char c = input[i];
            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                i++;
            }
            else if (c == '"')
            {
                i++; // skip opening "
                while (i < input.Length && input[i] != '"')
                {
                    if (input[i] == '\\' && i + 1 < input.Length)
                    { i++; sb.Append(input[i]); }
                    else sb.Append(input[i]);
                    i++;
                }
                i++; // skip closing "
            }
            else if (c == '\'')
            {
                i++; // skip opening '
                while (i < input.Length && input[i] != '\'')
                { sb.Append(input[i]); i++; }
                i++; // skip closing '
            }
            else if (c == '\\' && i + 1 < input.Length)
            {
                // line continuation or escape
                if (input[i + 1] == '\n' || input[i + 1] == '\r')
                {
                    i += 2;
                    if (i < input.Length && input[i] == '\n') i++;
                }
                else { i++; sb.Append(input[i]); i++; }
            }
            else
            {
                sb.Append(c); i++;
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    private static string EscapeQuote(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
