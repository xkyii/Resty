using Kx.Resty.Commands;
using Kx.Resty.Models;
using Kx.Resty.ViewModels;
using Xunit;

namespace Kx.Resty.UnitTests.ViewModels;

public class CollectionPanelTests
{
    [Fact]
    public void RenameCollection_AppendsSuffixWhenTargetExists()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var sourcePath = Path.Combine(tempDir, "alpha.http");
            var existingPath = Path.Combine(tempDir, "beta.http");
            File.WriteAllText(sourcePath, "### One\nGET https://example.com\n");
            File.WriteAllText(existingPath, "### Existing\nGET https://example.com\n");

            var panel = new CollectionPanel();
            var collection = new HttpCollection
            {
                FilePath = sourcePath,
                Name = "alpha"
            };

            var renamed = panel.RenameCollection(collection, "beta");

            Assert.True(renamed);
            Assert.Equal(Path.Combine(tempDir, "beta-1.http"), collection.FilePath);
            Assert.Equal("beta-1", collection.Name);
            Assert.True(File.Exists(collection.FilePath));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SearchText_FiltersMatchingNestedCollection()
    {
        var panel = new CollectionPanel();
        var matchingCollection = new HttpCollection
        {
            FilePath = "team.http",
            Name = "team"
        };
        matchingCollection.Requests.Add(new HttpRequestEntry
        {
            Name = "List users",
            Method = "GET",
            Url = "https://example.com/users"
        });

        var folder = new CollectionTreeNode
        {
            Name = "api",
            IsDirectory = true,
        };
        folder.Children.Add(new CollectionTreeNode
        {
            Name = "team",
            IsDirectory = false,
            Collection = matchingCollection,
        });

        panel.RootNodes.Add(folder);
        panel.SearchText = "users";

        var filteredFolder = Assert.Single(panel.FilteredRootNodes);
        Assert.True(filteredFolder.IsDirectory);
        var filteredCollection = Assert.Single(filteredFolder.Children);
        Assert.Equal("team", filteredCollection.Name);
    }

    [Fact]
    public void RenameRequest_UpdatesEntryNameAndWritesCollection()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var path = Path.Combine(tempDir, "demo.http");
            File.WriteAllText(path, "### Old Name\nGET https://example.com\n");

            var collection = HttpFileParser.Parse(path);
            var entry = collection.Requests[0];

            var panel = new CollectionPanel();
            var renamed = panel.RenameRequest(collection, entry, "New Name");

            Assert.True(renamed);
            Assert.Equal("New Name", entry.Name);

            var reparsed = HttpFileParser.Parse(path);
            Assert.Equal("New Name", reparsed.Requests[0].Name);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "RestyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}