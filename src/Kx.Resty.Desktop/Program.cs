using Avalonia;
using Kx.Resty;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Kx.Resty.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        RegisterGlobalExceptionLogging();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static void RegisterGlobalExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrashLog("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "Resty");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.log");

            var sb = new StringBuilder();
            sb.AppendLine("============================");
            sb.AppendLine(DateTimeOffset.Now.ToString("O"));
            sb.AppendLine(source);
            sb.AppendLine(ex?.ToString() ?? "(null exception)");

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Swallow to avoid recursive crash in logger.
        }
    }
}
