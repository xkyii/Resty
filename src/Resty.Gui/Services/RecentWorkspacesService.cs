using System.Text.Json;

namespace Resty.Gui.Services;

/// <summary>持久化最近工作区路径列表（%APPDATA%/Resty/recent_workspaces.json）。</summary>
public static class RecentWorkspacesService
{
    private const int MaxRecent = 10;

    private static string StoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Resty", "recent_workspaces.json");

    public static IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return [];
            var json = File.ReadAllText(StoragePath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list?.Where(Directory.Exists).ToList() ?? [];
        }
        catch { return []; }
    }

    public static void Add(string path)
    {
        try
        {
            var list = Load().ToList();
            list.Remove(path);
            list.Insert(0, path);
            if (list.Count > MaxRecent) list = list[..MaxRecent];
            var dir = Path.GetDirectoryName(StoragePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(StoragePath, JsonSerializer.Serialize(list));
        }
        catch { }
    }
}
