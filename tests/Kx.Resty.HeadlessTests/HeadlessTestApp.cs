using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Kx.Resty.HeadlessTests.HeadlessTestApp))]

namespace Kx.Resty.HeadlessTests;

public class HeadlessTestApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<Kx.Resty.App>();
        builder.UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = true
        });
        builder.WithInterFont();
        return builder;
    }
}