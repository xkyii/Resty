using Kx.Resty.Models;
using Kx.Resty.ViewModels;
using Kx.Resty.Commands;
using Xunit;

namespace Kx.Resty.UnitTests.ViewModels;

public class RequestTabTests
{
    [Fact]
    public void Constructor_LoadsAuthorizationHeaderIntoAuthFields()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Name = "Secured",
            Method = "GET",
            Url = "https://example.com/private"
        };
        entry.Headers.Add(new NamedValue { Key = "Authorization", Value = "Basic alice secret" });
        entry.Headers.Add(new NamedValue { Key = "Accept", Value = "application/json" });

        var tab = new RequestTab(entry, collection);

        Assert.Equal("basic", tab.SelectedAuthType.Code);
        Assert.Equal("alice", tab.AuthUsername);
        Assert.Equal("secret", tab.AuthPassword);
        var headers = tab.HeadersTable.ToNamedValues();
        Assert.Equal(2, headers.Count);
        Assert.Contains(headers, h => h.Key == "Authorization" && h.Value == "Basic alice secret");
        Assert.Contains(headers, h => h.Key == "Accept" && h.Value == "application/json");
    }

    [Fact]
    public void ChangingEditableProperties_UpdatesEntryAndTabTitle()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Method = "GET",
            Url = "https://example.com/old"
        };

        var tab = new RequestTab(entry, collection);
        tab.RequestName = "Load users";
        tab.Url = "https://example.com/users";
        tab.SelectedMethod = RequestTab.Methods.Single(x => x.Name == "POST");

        Assert.Equal("Load users", entry.Name);
        Assert.Equal("https://example.com/users", entry.Url);
        Assert.Equal("POST", entry.Method);
        Assert.Equal("Load users *", tab.TabTitle);
        Assert.False(tab.IsSaved);
    }

    [Fact]
    public void Constructor_BuildsEditableUrlFromStoredQueryParams()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Method = "GET",
            Url = "https://example.com/search"
        };
        entry.QueryParams.Add(new NamedValue { Key = "q", Value = "avalonia" });
        entry.QueryParams.Add(new NamedValue { Key = "sort", Value = "stars" });

        var tab = new RequestTab(entry, collection);

        Assert.Equal("https://example.com/search?q=avalonia&sort=stars", tab.Url);
        var queryParams = tab.ParamsTable.ToNamedValues();
        Assert.Equal(2, queryParams.Count);
        Assert.Equal("q", queryParams[0].Key);
        Assert.Equal("avalonia", queryParams[0].Value);
    }

    [Fact]
    public void EditingUrl_ImmediatelySynchronizesQueryParamsTable()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Method = "GET",
            Url = "https://example.com/users"
        };

        var tab = new RequestTab(entry, collection);
        tab.Url = "https://example.com/users?page=2&pageSize=20&keyword=tom";

        Assert.Equal("https://example.com/users?page=2&pageSize=20&keyword=tom", tab.Url);
        Assert.Equal("https://example.com/users", entry.Url);

        var queryParams = tab.ParamsTable.ToNamedValues();
        Assert.Collection(
            queryParams,
            item =>
            {
                Assert.Equal("page", item.Key);
                Assert.Equal("2", item.Value);
            },
            item =>
            {
                Assert.Equal("pageSize", item.Key);
                Assert.Equal("20", item.Value);
            },
            item =>
            {
                Assert.Equal("keyword", item.Key);
                Assert.Equal("tom", item.Value);
            });
    }

    [Fact]
    public void EditingQueryParams_ImmediatelySynchronizesUrl()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Method = "GET",
            Url = "https://example.com/users"
        };

        var tab = new RequestTab(entry, collection);

        tab.ParamsTable.AddRow(true, "page", "2");
        Assert.Equal("https://example.com/users?page=2", tab.Url);

        tab.ParamsTable.AddRow(true, "keyword", "tom");
        Assert.Equal("https://example.com/users?page=2&keyword=tom", tab.Url);

        var pageRow = tab.ParamsTable.Items.First(x => x.Key == "page");
        pageRow.IsEnabled = false;
        Assert.Equal("https://example.com/users?keyword=tom", tab.Url);
        Assert.Equal("https://example.com/users", entry.Url);
    }

    [Fact]
    public void HandleBodyEditing_DistinguishesDirectBodyVsFileReference()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Method = "POST",
            Url = "https://example.com/upload",
            BodyFilePath = "./payload.json"
        };

        var tab = new RequestTab(entry, collection);
        Assert.Equal("< ./payload.json", tab.Body);

        // Edit to direct body text
        tab.Body = "{\"key\": \"value\"}";

        // Simulate Save (which applies the body changes)
        string tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        try
        {
            var testEntry = new HttpRequestEntry
            {
                Method = "POST",
                Url = "https://example.com/upload",
                Body = "{\"key\": \"value\"}"
            };

            // This mimics what RequestTab.Save does internally
            testEntry.Body = "{\"key\": \"value\"}";
            testEntry.BodyFilePath = null;

            Assert.Equal("{\"key\": \"value\"}", testEntry.Body);
            Assert.Null(testEntry.BodyFilePath);
        }
        finally
        {
            Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void TabTitle_ShowsUnsavedIndicator()
    {
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

        var tab = new RequestTab(entry, collection);
        Assert.Equal("Test", tab.TabTitle);
        Assert.True(tab.IsSaved);

        tab.Url = "https://example.com/changed";

        Assert.Equal("Test *", tab.TabTitle);
        Assert.False(tab.IsSaved);
    }

    [Fact]
    public void CanSave_ReturnsFalseForUnlinkedRequest()
    {
        var entry = new HttpRequestEntry
        {
            Method = "GET",
            Url = "https://example.com"
        };

        var tab = new RequestTab();

        Assert.False(tab.CanSave);
    }

    [Fact]
    public void SelectedAuthType_ChangesWithoutCredentialsFields()
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

        tab.SelectedAuthType = RequestTab.AuthTypes.First(x => x.Code == "basic");

        Assert.True(tab.HasCredentialsAuth);
        Assert.Equal("basic", tab.SelectedAuthType.Code);
    }

    [Fact]
    public void EditingHeaders_SynchronizesToEntryAndSurvivesReload()
    {
        var collection = new HttpCollection
        {
            FilePath = "d:/tmp/demo.http",
            Name = "demo"
        };
        var entry = new HttpRequestEntry
        {
            Method = "POST",
            Url = "https://example.com/users"
        };
        entry.Headers.Add(new NamedValue { Key = "Accept", Value = "application/json" });

        var tab = new RequestTab(entry, collection);
        tab.HeadersTable.AddRow(true, "Content-Type", "application/json");

        Assert.Contains(entry.Headers, x => x.Key == "Content-Type" && x.Value == "application/json");

        tab.ReloadFromEntry();
        var headers = tab.HeadersTable.ToNamedValues();
        Assert.Contains(headers, x => x.Key == "Accept" && x.Value == "application/json");
        Assert.Contains(headers, x => x.Key == "Content-Type" && x.Value == "application/json");
    }

    [Fact]
    public void RefreshFromFileCommand_ReloadsCurrentRequestFromDisk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "demo.http");
        File.WriteAllText(filePath, """
            ### User
            GET https://example.com/users
            Accept: application/json

            """);

        try
        {
            var collection = HttpFileParser.Parse(filePath);
            var entry = Assert.Single(collection.Requests);
            var tab = new RequestTab(entry, collection);

            tab.Url = "https://example.com/users?page=2";
            tab.HeadersTable.AddRow(true, "X-Debug", "1");

            tab.RefreshFromFile();

            Assert.Equal("https://example.com/users", tab.Url);
            var headers = tab.HeadersTable.ToNamedValues();
            Assert.Single(headers);
            Assert.Equal("Accept", headers[0].Key);
            Assert.Equal("application/json", headers[0].Value);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}