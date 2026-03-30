using Avalonia.Headless.XUnit;
using Kx.Resty.Models;
using Kx.Resty.ViewModels;
using Xunit;

namespace Kx.Resty.HeadlessTests.Views;

public class RequestTabTests
{
    [AvaloniaFact]
    public void RequestTab_LoadsDataFromEntry()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Name = "Test Request",
            Method = "GET",
            Url = "https://example.com/api"
        };

        var tab = new RequestTab(entry, collection);

        Assert.Equal("Test Request", tab.RequestName);
        Assert.Equal("https://example.com/api", tab.Url);
        Assert.Equal("GET", tab.SelectedMethod.Name);
    }

    [AvaloniaFact]
    public void RequestTab_ShowsUnsavedStateInTitle()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Name = "Original",
            Method = "GET",
            Url = "https://example.com"
        };

        var tab = new RequestTab(entry, collection);
        Assert.Equal("Original", tab.TabTitle);
        Assert.True(tab.IsSaved);

        tab.Url = "https://example.com/modified";

        Assert.Equal("Original *", tab.TabTitle);
        Assert.False(tab.IsSaved);
    }

    [AvaloniaFact]
    public void RequestTab_UnlinkedRequestIsNotSaveable()
    {
        var tab = new RequestTab();

        Assert.False(tab.CanSave);
        Assert.False(tab.IsSaved);
    }

    [AvaloniaFact]
    public void RequestTab_RespondsToAuthTypeChange()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Method = "GET",
            Url = "https://example.com"
        };

        var tab = new RequestTab(entry, collection);

        Assert.False(tab.HasCredentialsAuth);

        var basicAuth = RequestTab.AuthTypes.First(x => x.Code == "basic");
        tab.SelectedAuthType = basicAuth;

        Assert.True(tab.HasCredentialsAuth);
        Assert.Equal("basic", tab.SelectedAuthType.Code);
    }
}
