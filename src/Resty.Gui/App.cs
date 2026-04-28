using Aprillz.MewUI;
using Aprillz.MewUI.Platform.Win32;
using Resty.Gui;

Win32Platform.Register();
Direct2DBackend.Register();

Application.Create()
    .UseTheme(ThemeVariant.Dark)
    .Run(new MainWindow());
