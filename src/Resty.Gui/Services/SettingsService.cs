using System.Text.Json;

namespace Resty.Gui.Services;

/// <summary>应用设置（超时、代理、JSON 自动格式化）。</summary>
public sealed class AppSettings
{
    public int    TimeoutSeconds    { get; set; } = 30;
    public string ProxyUrl          { get; set; } = string.Empty;
    public bool   JsonAutoFormat    { get; set; } = true;
}

/// <summary>持久化应用设置（%APPDATA%/Resty/settings.json）。</summary>
public static class SettingsService
{
    private static string StoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Resty", "settings.json");

    private static AppSettings _current = Load();
    public static AppSettings Current => _current;

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(StoragePath)) return new AppSettings();
            var json = File.ReadAllText(StoragePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            _current = settings;
            var dir = Path.GetDirectoryName(StoragePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(StoragePath, JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
