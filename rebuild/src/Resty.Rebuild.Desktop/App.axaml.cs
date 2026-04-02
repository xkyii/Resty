using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Resty.Rebuild.Infrastructure.Http;
using Resty.Rebuild.Infrastructure.Persistence;
using Resty.Rebuild.Desktop.ViewModels;
using Resty.Rebuild.Desktop.Views;

namespace Resty.Rebuild.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var requestExecutor = new SystemHttpRequestExecutor();
            var directoryStore = new JsonDirectoryStore();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(requestExecutor, directoryStore),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

}