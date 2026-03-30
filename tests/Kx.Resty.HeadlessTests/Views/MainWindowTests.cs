using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Kx.Resty.Models;
using Kx.Resty.ViewModels;
using Xunit;

namespace Kx.Resty.HeadlessTests.Views;

public class MainWindowTests
{
    [AvaloniaFact]
    public void MainWindow_InstantiatesAndManagesWorkspaces()
    {
        var viewModel = new MainWindow();

        Assert.Empty(viewModel.Workspaces);
        Assert.False(viewModel.HasWorkspaces);
    }

    [AvaloniaFact]
    public void MainWindow_UpdatesStateWhenCollectionsAreAdded()
    {
        var viewModel = new MainWindow();
        var workspace = new WorkspaceTab
        {
            DirectoryPath = "d:/tmp/workspace",
            Name = "test-workspace"
        };
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/workspace/demo.http",
            Name = "demo"
        };

        viewModel.Workspaces.Add(workspace);
        viewModel.SwitchWorkspaceCommand.Execute(workspace);

        workspace.SidePanel.RootNodes.Add(new CollectionTreeNode
        {
            Name = collection.Name,
            IsDirectory = false,
            Collection = collection
        });

        Assert.True(viewModel.HasCollections);
        Assert.False(viewModel.HasActiveRequest);
        Assert.True(viewModel.HasCollectionsButNoRequest);
    }
}
