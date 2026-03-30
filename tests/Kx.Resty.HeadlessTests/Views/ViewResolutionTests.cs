using Avalonia.Headless.XUnit;
using Kx.Resty.ViewModels;
using Xunit;

namespace Kx.Resty.HeadlessTests.Views;

public class ViewResolutionTests
{
    [AvaloniaFact]
    public void CreateViewForViewModel_ReturnsMainWindowView()
    {
        var viewModel = new MainWindow();

        var view = Kx.Resty.App.CreateViewForViewModel(viewModel);

        Assert.NotNull(view);
        Assert.IsType<Kx.Resty.Views.MainWindow>(view);
    }
}