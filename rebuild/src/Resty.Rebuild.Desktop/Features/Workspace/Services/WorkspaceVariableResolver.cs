using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Resty.Rebuild.Desktop.Features.Workspace.Services;

public static class WorkspaceVariableResolver
{
    private static readonly Regex PlaceholderRegex = new("\\{\\{\\s*([A-Za-z0-9_.-]+)\\s*\\}\\}", RegexOptions.Compiled);

    public static Dictionary<string, string> LoadEnvironmentVariables(string workspaceRoot)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return result;

        MergeEnvFile(result, Path.Combine(workspaceRoot, "http-client.env.json"));
        MergeEnvFile(result, Path.Combine(workspaceRoot, "http-client.private.env.json"));
        return result;
    }

    public static string Resolve(string text, IReadOnlyDictionary<string, string> fileVars, IReadOnlyDictionary<string, string> envVars)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return PlaceholderRegex.Replace(text, m =>
        {
            var key = m.Groups[1].Value;
            if (fileVars.TryGetValue(key, out var fv))
                return fv;
            if (envVars.TryGetValue(key, out var ev))
                return ev;
            return m.Value;
        });
    }

    private static void MergeEnvFile(Dictionary<string, string> target, string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // 支持两种结构：平铺对象；或 {"dev": {...}, "prod": {...}}，默认取 dev 或第一个对象。
                var hasNestedObject = doc.RootElement.EnumerateObject().Any(p => p.Value.ValueKind == JsonValueKind.Object);
                if (!hasNestedObject)
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                        target[p.Name] = p.Value.ToString();
                    return;
                }

                JsonElement selected = default;
                var found = false;
                if (doc.RootElement.TryGetProperty("dev", out var dev) && dev.ValueKind == JsonValueKind.Object)
                {
                    selected = dev;
                    found = true;
                }
                else
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        if (p.Value.ValueKind == JsonValueKind.Object)
                        {
                            selected = p.Value;
                            found = true;
                            break;
                        }
                    }
                }

                if (found)
                {
                    foreach (var p in selected.EnumerateObject())
                        target[p.Name] = p.Value.ToString();
                }
            }
        }
        catch
        {
            // invalid env file -> ignore
        }
    }
}
