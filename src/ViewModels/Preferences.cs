using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Kx.Resty.Models;

namespace Kx.Resty.ViewModels;

public class Preferences : ObservableObject
{
    private const string CurrentWindowsDirName = "Resty";
    private const string LegacyWindowsDirName = "Kx.Resty";
    private const string CurrentMacDirName = "Resty";
    private const string LegacyMacDirName = "Kx.Resty";
    private const string CurrentLinuxDirName = ".resty";
    private const string LegacyLinuxDirName = ".kx.resty";

    [JsonIgnore]
    public static Preferences Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = Load();
            _instance._isLoading = false;
            return _instance;
        }
    }

    public string Locale
    {
        get => _locale;
        set
        {
            if (SetProperty(ref _locale, value) && !_isLoading)
                App.SetLocale(value);
        }
    }

    public string Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value) && !_isLoading)
                App.SetTheme(value);
        }
    }

    public void Save()
    {
        if (_isLoading)
            return;

        try
        {
            var dir = DataDir;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, PreferencesJsonContext.Default.Preferences);
            File.WriteAllText(Path.Combine(dir, "preferences.json"), json);
        }
        catch
        {
            // ignore
        }
    }

    private static Preferences Load()
    {
        try
        {
            foreach (var dataDir in EnumerateDataDirs())
            {
                var file = Path.Combine(dataDir, "preferences.json");
                if (!File.Exists(file))
                    continue;

                var json = File.ReadAllText(file);
                var pref = JsonSerializer.Deserialize(json, PreferencesJsonContext.Default.Preferences);
                if (pref != null)
                    return pref;
            }
        }
        catch
        {
            // ignore
        }

        return new Preferences();
    }

    private static string DataDir
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), CurrentWindowsDirName);
            if (OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", CurrentMacDirName);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CurrentLinuxDirName);
        }
    }

    private static IEnumerable<string> EnumerateDataDirs()
    {
        yield return DataDir;

        if (OperatingSystem.IsWindows())
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyWindowsDirName);
        else if (OperatingSystem.IsMacOS())
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", LegacyMacDirName);
        else
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), LegacyLinuxDirName);
    }

    private static Preferences? _instance = null;

    private bool _isLoading = true;
    private string _locale = "en_US";
    private string _theme = "Dark";

    public List<WorkspaceEntry> ManagedWorkspaces { get; set; } = [];
    public List<WorkspaceEntry> RecentWorkspaces  { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Preferences))]
[JsonSerializable(typeof(List<WorkspaceEntry>))]
internal partial class PreferencesJsonContext : JsonSerializerContext
{
}