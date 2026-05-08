using System.Text.Json;

namespace Resty.Gui.Services;

/// <summary>持久化收藏工作区路径列表（%APPDATA%/Resty/favorite_workspaces.json）。</summary>
public static class FavoriteWorkspacesService
{
    private static string StoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Resty", "favorite_workspaces.json");

    public static IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return [];
            var json = File.ReadAllText(StoragePath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
        catch { return []; }
    }

    public static bool Contains(string path)
        => Load().Any(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));

    public static void Add(string path)
    {
        try
        {
            var normalizedPath = path.Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath)) return;

            var list = Load().ToList();
            list.RemoveAll(x => string.Equals(x, normalizedPath, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, normalizedPath);
            Save(list);
        }
        catch { }
    }

    public static void Remove(string path)
    {
        try
        {
            var list = Load().ToList();
            list.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
            Save(list);
        }
        catch { }
    }

    private static void Save(IReadOnlyList<string> paths)
    {
        var dir = Path.GetDirectoryName(StoragePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(StoragePath, JsonSerializer.Serialize(paths,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
