using System.Text.Json;

namespace Resty.Gui.Services;

/// <summary>持久化最近工作区路径列表（%APPDATA%/Resty/recent_workspaces.json）。</summary>
public static class RecentWorkspacesService
{
    private const int MaxRecent = 10;

    public sealed record RecentWorkspaceEntry(string Path, DateTime LastAccessedAt);

    private static string StoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Resty", "recent_workspaces.json");

    public static IReadOnlyList<RecentWorkspaceEntry> LoadEntries()
    {
        try
        {
            if (!File.Exists(StoragePath)) return [];
            var json = File.ReadAllText(StoragePath);
            var objectList = JsonSerializer.Deserialize<List<RecentWorkspaceEntry>>(json);
            if (objectList is not null)
            {
                return objectList
                    .Where(e => !string.IsNullOrWhiteSpace(e.Path))
                    .Where(e => Directory.Exists(e.Path))
                    .ToList();
            }

            // 兼容旧格式：string[]
            var oldList = JsonSerializer.Deserialize<List<string>>(json);
            if (oldList is null) return [];
            var now = DateTime.UtcNow;
            var upgraded = oldList
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Where(Directory.Exists)
                .Select((p, i) => new RecentWorkspaceEntry(p, now.AddMinutes(-i)))
                .ToList();
            SaveEntries(upgraded);
            return upgraded;
        }
        catch { return []; }
    }

    public static IReadOnlyList<string> Load()
        => LoadEntries().Select(e => e.Path).ToList();

    public static void Add(string path)
    {
        try
        {
            var normalizedPath = path.Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath)) return;

            var list = LoadEntries().ToList();
            list.RemoveAll(x => string.Equals(x.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, new RecentWorkspaceEntry(normalizedPath, DateTime.UtcNow));
            if (list.Count > MaxRecent) list = list[..MaxRecent];
            SaveEntries(list);
        }
        catch { }
    }

    public static void Remove(string path)
    {
        try
        {
            var list = LoadEntries().ToList();
            list.RemoveAll(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
            SaveEntries(list);
        }
        catch { }
    }

    private static void SaveEntries(IReadOnlyList<RecentWorkspaceEntry> entries)
    {
        var dir = Path.GetDirectoryName(StoragePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(StoragePath, JsonSerializer.Serialize(entries,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
