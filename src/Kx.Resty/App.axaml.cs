using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Kx.Resty.Features.Shell.ViewModels;
using Kx.Resty.Infrastructure.Http;
using Kx.Resty.Infrastructure.Persistence;
using Kx.Resty.Views;

namespace Kx.Resty;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    new SystemHttpRequestExecutor(),
                    new JsonDirectoryStore())
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
