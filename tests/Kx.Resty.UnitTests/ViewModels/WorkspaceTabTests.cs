using Kx.Resty.Models;
using Kx.Resty.ViewModels;
using Xunit;

namespace Kx.Resty.UnitTests.ViewModels;

public class WorkspaceTabTests
{
    [Fact]
    public void OpenRequest_ReusesExistingTabForSameEntry()
    {
        var workspace = new WorkspaceTab();
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Name = "Users",
            Method = "GET",
            Url = "https://example.com/users"
        };

        workspace.OpenRequest(entry, collection);
        var firstTab = Assert.Single(workspace.OpenRequests);

        workspace.OpenRequest(entry, collection);

        Assert.Single(workspace.OpenRequests);
        Assert.Same(firstTab, workspace.ActiveRequest);
        Assert.True(firstTab.IsActive);
    }

    [Fact]
    public void CloseRequest_ActivatesNeighborTab()
    {
        var workspace = new WorkspaceTab();
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var first = new HttpRequestEntry { Name = "One", Method = "GET", Url = "https://example.com/1" };
        var second = new HttpRequestEntry { Name = "Two", Method = "GET", Url = "https://example.com/2" };

        workspace.OpenRequest(first, collection);
        workspace.OpenRequest(second, collection);
        var firstTab = workspace.OpenRequests[0];
        var secondTab = workspace.OpenRequests[1];

        workspace.CloseRequestCommand.Execute(secondTab);

        Assert.Single(workspace.OpenRequests);
        Assert.DoesNotContain(secondTab, workspace.OpenRequests);
        Assert.Same(firstTab, workspace.ActiveRequest);
        Assert.True(firstTab.IsActive);
    }
}