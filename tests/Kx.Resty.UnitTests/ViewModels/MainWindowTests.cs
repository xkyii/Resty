using Kx.Resty.Models;
using Kx.Resty.ViewModels;
using Xunit;

namespace Kx.Resty.UnitTests.ViewModels;

public class MainWindowTests
{
    [Fact]
    public void StateFlags_ReflectActiveWorkspaceContent()
    {
        var viewModel = new MainWindow();
        var workspace = new WorkspaceTab
        {
            DirectoryPath = "d:/tmp/workspace",
            Name = "workspace"
        };

        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/workspace/demo.http",
            Name = "demo"
        };

        workspace.SidePanel.RootNodes.Add(new CollectionTreeNode
        {
            Name = collection.Name,
            Collection = collection,
            IsDirectory = false
        });

        viewModel.Workspaces.Add(workspace);
        viewModel.SwitchWorkspaceCommand.Execute(workspace);

        Assert.True(viewModel.HasWorkspaces);
        Assert.True(viewModel.HasCollections);
        Assert.False(viewModel.HasNoCollections);
        Assert.False(viewModel.HasActiveRequest);
        Assert.True(viewModel.HasCollectionsButNoRequest);
    }

    [Fact]
    public void CloseWorkspace_ActivatesNextAvailableWorkspace()
    {
        var viewModel = new MainWindow();
        var first = new WorkspaceTab { DirectoryPath = "d:/tmp/a", Name = "a" };
        var second = new WorkspaceTab { DirectoryPath = "d:/tmp/b", Name = "b" };

        viewModel.Workspaces.Add(first);
        viewModel.Workspaces.Add(second);
        viewModel.SwitchWorkspaceCommand.Execute(first);

        viewModel.CloseWorkspaceCommand.Execute(first);

        Assert.Single(viewModel.Workspaces);
        Assert.Same(second, viewModel.ActiveWorkspace);
        Assert.True(second.IsActive);
    }
}