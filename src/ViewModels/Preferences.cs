using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kx.Resty.ViewModels;

public class Preferences : ObservableObject
{
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
            var file = Path.Combine(DataDir, "preferences.json");
            if (File.Exists(file))
            {
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
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kx.Resty");
            if (OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Kx.Resty");
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kx.resty");
        }
    }

    private static Preferences? _instance = null;

    private bool _isLoading = true;
    private string _locale = "en_US";
    private string _theme = "Dark";
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Preferences))]
internal partial class PreferencesJsonContext : JsonSerializerContext
{
}