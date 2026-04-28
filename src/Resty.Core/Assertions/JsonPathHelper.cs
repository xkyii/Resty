using System.Text.Json;

namespace Resty.Core.Assertions;

/// <summary>
/// Evaluates a simple JSONPath subset against a <see cref="JsonDocument"/>.
/// Supported: $.field, $.a.b.c, $.items[0], $.items[0].name
/// </summary>
internal static class JsonPathHelper
{
    /// <summary>
    /// Returns the string representation of the node at <paramref name="jsonPath"/>,
    /// or <c>null</c> if the path does not exist.
    /// </summary>
    public static string? Evaluate(string json, string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var node = Navigate(doc.RootElement, jsonPath);
            if (node is null) return null;

            return node.Value.ValueKind switch
            {
                JsonValueKind.String => node.Value.GetString(),
                JsonValueKind.Null => null,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => node.Value.GetRawText(),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns whether a node exists at the given path (even if its value is null).</summary>
    public static bool Exists(string json, string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return Navigate(doc.RootElement, jsonPath) is not null;
        }
        catch { return false; }
    }

    // -------------------------------------------------------------------------

    private static JsonElement? Navigate(JsonElement root, string path)
    {
        // Path must start with "$"
        if (!path.StartsWith('$')) return null;

        var current = (JsonElement?)root;
        foreach (var segment in ParseSegments(path[1..]))   // skip "$"
        {
            if (current is null) return null;

            if (segment.StartsWith('[') && segment.EndsWith(']'))
            {
                if (current.Value.ValueKind != JsonValueKind.Array) return null;
                if (!int.TryParse(segment[1..^1], out var idx)) return null;
                if (idx < 0 || idx >= current.Value.GetArrayLength()) return null;
                current = current.Value[idx];
            }
            else
            {
                if (current.Value.ValueKind != JsonValueKind.Object) return null;
                if (!current.Value.TryGetProperty(segment, out var prop)) return null;
                current = prop;
            }
        }

        return current;
    }

    private static IEnumerable<string> ParseSegments(string path)
    {
        // path examples: ".name", ".user.name", ".items[0].id", "[0]"
        var i = 0;
        while (i < path.Length)
        {
            if (path[i] == '.')
            {
                i++;
                var start = i;
                while (i < path.Length && path[i] != '.' && path[i] != '[')
                    i++;
                if (i > start)
                    yield return path[start..i];
            }
            else if (path[i] == '[')
            {
                var end = path.IndexOf(']', i);
                if (end < 0) yield break;
                yield return path[i..(end + 1)];
                i = end + 1;
            }
            else
            {
                i++;
            }
        }
    }
}
