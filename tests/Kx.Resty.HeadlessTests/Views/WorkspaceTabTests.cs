using Avalonia.Headless.XUnit;
using Kx.Resty.Models;
using Kx.Resty.ViewModels;
using Xunit;

namespace Kx.Resty.HeadlessTests.Views;

public class WorkspaceTabTests
{
    [AvaloniaFact]
    public void CollectionPanel_InstantiatesAndSearchesWork()
    {
        var panel = new CollectionPanel();
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/api.http",
            Name = "api"
        };
        collection.Requests.Add(new HttpRequestEntry
        {
            Name = "Get users",
            Method = "GET",
            Url = "https://example.com/users"
        });

        panel.RootNodes.Add(new CollectionTreeNode
        {
            Name = "api",
            IsDirectory = false,
            Collection = collection
        });

        Assert.Single(panel.RootNodes);
        Assert.Single(panel.FilteredRootNodes);

        panel.SearchText = "users";

        Assert.Single(panel.FilteredRootNodes);
    }

    [AvaloniaFact]
    public void WorkspaceTab_ManagesOpenRequests()
    {
        var workspace = new WorkspaceTab();
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Name = "Test",
            Method = "GET",
            Url = "https://example.com"
        };

        workspace.OpenRequest(entry, collection);

        Assert.Single(workspace.OpenRequests);
        Assert.NotNull(workspace.ActiveRequest);
        Assert.True(workspace.ActiveRequest.IsActive);
    }

    [AvaloniaFact]
    public void EnvironmentSet_RendersWithVariables()
    {
        var envSet = new EnvironmentSet
        {
            Name = "production"
        };
        envSet.Variables.Add(new EnvironmentVariable { Name = "baseUrl", Value = "https://api.example.com" });
        envSet.Variables.Add(new EnvironmentVariable { Name = "token", Value = "secret-token" });

        Assert.Equal("production", envSet.Name);
        Assert.Equal(2, envSet.Variables.Count);
    }
}
