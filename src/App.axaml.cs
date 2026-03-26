using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using System.Windows.Input;

namespace Kx.Resty;

public partial class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>();
        builder.UsePlatformDetect();
        builder.LogToTrace();
        builder.WithInterFont();
        builder.With(new FontManagerOptions()
        {
            DefaultFamilyName = "fonts:Inter#Inter"
        });
        return builder;
    }

    public static ICommand OpenPreferencesCommand { get; } =
        new SimpleCommand(_ => ShowDialog(new Views.Dialogs.Preferences()));

    public static Control? CreateViewForViewModel(object data)
    {
        var dataTypeName = data.GetType().FullName;
        if (string.IsNullOrEmpty(dataTypeName) || !dataTypeName.Contains(".ViewModels.", StringComparison.Ordinal))
            return null;

        var viewTypeName = dataTypeName.Replace(".ViewModels.", ".Views.");
        var viewType = Type.GetType(viewTypeName);
        if (viewType != null)
            return Activator.CreateInstance(viewType) as Control;

        return null;
    }

    public static void SetLocale(string localeKey)
    {
        if (Current is not App app ||
            app.Resources[localeKey] is not ResourceDictionary targetLocale ||
            targetLocale == app._activeLocale)
            return;

        if (app._activeLocale != null)
            app.Resources.MergedDictionaries.Remove(app._activeLocale);

        app.Resources.MergedDictionaries.Add(targetLocale);
        app._activeLocale = targetLocale;
    }

    public static void SetTheme(string theme)
    {
        if (Current is not App app)
            return;

        if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
            app.RequestedThemeVariant = ThemeVariant.Light;
        else if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            app.RequestedThemeVariant = ThemeVariant.Dark;
        else
            app.RequestedThemeVariant = ThemeVariant.Default;
    }

    public static string Text(string key, params object[] args)
    {
        var fmt = Current?.FindResource($"Text.{key}") as string;
        if (string.IsNullOrWhiteSpace(fmt))
            return $"Text.{key}";

        if (args == null || args.Length == 0)
            return fmt;

        return string.Format(fmt, args);
    }

    public static System.Threading.Tasks.Task? ShowDialog(object data, Window? owner = null)
    {
        if (owner == null)
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
                owner = mainWindow;
            else
                return null;
        }

        if (data is Views.ChromelessWindow window)
            return window.ShowDialog(owner);

        if (CreateViewForViewModel(data) is Views.ChromelessWindow vmWindow)
        {
            vmWindow.DataContext = data;
            return vmWindow.ShowDialog(owner);
        }

        return null;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var pref = ViewModels.Preferences.Instance;
        pref.PropertyChanged += (_, _) => pref.Save();

        SetLocale(pref.Locale);
        SetTheme(pref.Theme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            BindingPlugins.DataValidators.RemoveAt(0);

            ToolTip.ToolTipOpeningEvent.AddClassHandler<Control>((c, e) =>
            {
                var topLevel = TopLevel.GetTopLevel(c);
                if (topLevel is not Window { IsActive: true })
                    e.Cancel = true;
            });

            var mainWindowVM = new ViewModels.MainWindow();
            var mainWindow = new Views.MainWindow { DataContext = mainWindowVM };
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private ResourceDictionary? _activeLocale = null;

    public class SimpleCommand(Action<object?> action) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action(parameter);
    }
}