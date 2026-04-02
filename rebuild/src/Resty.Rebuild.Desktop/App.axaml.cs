using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Resty.Rebuild.Infrastructure.Http;
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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(requestExecutor),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

}