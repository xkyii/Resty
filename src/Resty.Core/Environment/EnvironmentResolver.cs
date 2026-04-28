using System.Text.Json;
using Resty.Core.Models;

namespace Resty.Core.Environment;

/// <summary>
/// Loads environment variables from http-client.env.json / http-client.private.env.json
/// and resolves {{variable}} placeholders in request fields.
/// </summary>
public sealed class EnvironmentResolver
{
    private readonly Dictionary<string, string> _variables;

    private EnvironmentResolver(Dictionary<string, string> variables)
    {
        _variables = variables;
    }

    /// <summary>The resolved environment variables (read-only view).</summary>
    public IReadOnlyDictionary<string, string> Variables => _variables;

    /// <summary>
    /// Loads variables for <paramref name="envName"/> from the directory of <paramref name="httpFilePath"/>.
    /// Private env variables overlay public ones.
    /// File-level variables from the .http file itself are the lowest priority.
    /// </summary>
    public static EnvironmentResolver Load(
        string httpFilePath,
        string envName,
        IReadOnlyDictionary<string, string>? fileVariables = null)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(httpFilePath)) ?? ".";
        var vars = new Dictionary<string, string>(StringComparer.Ordinal);

        // 1. File-level @variables (lowest priority)
        if (fileVariables is not null)
            foreach (var kv in fileVariables)
                vars[kv.Key] = kv.Value;

        // 2. http-client.env.json
        ApplyEnvFile(Path.Combine(dir, "http-client.env.json"), envName, vars);

        // 3. http-client.private.env.json (highest priority, overlays public env)
        ApplyEnvFile(Path.Combine(dir, "http-client.private.env.json"), envName, vars);

        return new EnvironmentResolver(vars);
    }

    /// <summary>Resolves all {{variable}} placeholders in the given string.</summary>
    public string Resolve(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        var result = input;
        foreach (var (key, value) in _variables)
            result = result.Replace("{{" + key + "}}", value, StringComparison.Ordinal);

        return result;
    }

    /// <summary>Returns a copy of <paramref name="request"/> with all placeholders resolved.</summary>
    public HttpRequestDefinition ApplyTo(HttpRequestDefinition request)
    {
        var resolvedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in request.Headers)
            resolvedHeaders[k] = Resolve(v);

        return new HttpRequestDefinition
        {
            Name = request.Name,
            Method = request.Method,
            Url = Resolve(request.Url),
            Headers = resolvedHeaders,
            Body = request.Body is null ? null : Resolve(request.Body),
            Assertions = request.Assertions,
        };
    }

    // -------------------------------------------------------------------------

    private static void ApplyEnvFile(
        string filePath,
        string envName,
        Dictionary<string, string> target)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(envName, out var envElement)
                || envElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var prop in envElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    target[prop.Name] = prop.Value.GetString() ?? string.Empty;
            }
        }
        catch (Exception)
        {
            // Silently ignore malformed env files — don't crash the tool
        }
    }
}
