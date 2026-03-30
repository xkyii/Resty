using Kx.Resty.Commands;
using Kx.Resty.Models;
using Xunit;

namespace Kx.Resty.UnitTests.Commands;

public class HttpFileWriterTests
{
    [Fact]
    public void Write_WritesOnlyEnabledHeadersAndQueryParams()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var path = Path.Combine(tempDir, "demo.http");
            var collection = new HttpCollection
            {
                FilePath = path,
                Name = "demo"
            };

            collection.Variables.Add(new InPlaceVariable { Name = "baseUrl", Value = "https://example.com" });
            collection.Requests.Add(new HttpRequestEntry
            {
                Name = "List",
                Method = "GET",
                Url = "https://example.com/users",
                Body = string.Empty,
            });

            collection.Requests[0].Headers.Add(new NamedValue { Enabled = true, Key = "Accept", Value = "application/json" });
            collection.Requests[0].Headers.Add(new NamedValue { Enabled = false, Key = "X-Ignored", Value = "hidden" });
            collection.Requests[0].QueryParams.Add(new NamedValue { Enabled = true, Key = "page", Value = "1" });
            collection.Requests[0].QueryParams.Add(new NamedValue { Enabled = false, Key = "debug", Value = "true" });
            collection.IsDirty = true;

            HttpFileWriter.Write(collection);

            var text = File.ReadAllText(path);
            Assert.Contains("@baseUrl = https://example.com", text);
            Assert.Contains("GET https://example.com/users?page=1", text);
            Assert.Contains("Accept: application/json", text);
            Assert.DoesNotContain("X-Ignored", text);
            Assert.DoesNotContain("debug=true", text);
            Assert.False(collection.IsDirty);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Write_RoundTripsImportantRequestFields()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var path = Path.Combine(tempDir, "roundtrip.http");
            var collection = new HttpCollection
            {
                FilePath = path,
                Name = "roundtrip"
            };

            collection.Variables.Add(new InPlaceVariable { Name = "baseUrl", Value = "https://example.com" });
            collection.Requests.Add(new HttpRequestEntry
            {
                Name = "Create user",
                Method = "POST",
                Url = "{{baseUrl}}/users",
                Body = "{\n  \"name\": \"resty\"\n}",
            });

            var request = collection.Requests[0];
            request.Headers.Add(new NamedValue { Enabled = true, Key = "Content-Type", Value = "application/json" });
            request.QueryParams.Add(new NamedValue { Enabled = true, Key = "verbose", Value = "1" });
            request.Annotations.NoLog = true;
            request.Annotations.TimeoutSeconds = 30;

            HttpFileWriter.Write(collection);

            var parsed = HttpFileParser.Parse(path);
            var reparsed = Assert.Single(parsed.Requests);

            Assert.Single(parsed.Variables);
            Assert.Equal("baseUrl", parsed.Variables[0].Name);
            Assert.Equal("https://example.com", parsed.Variables[0].Value);
            Assert.Equal("Create user", reparsed.Name);
            Assert.Equal("POST", reparsed.Method);
            Assert.Equal("{{baseUrl}}/users", reparsed.Url);
            Assert.Equal("1", Assert.Single(reparsed.QueryParams).Value);
            Assert.True(reparsed.Annotations.NoLog);
            Assert.Equal(30, reparsed.Annotations.TimeoutSeconds);
            Assert.Contains("\"name\": \"resty\"", reparsed.Body);
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