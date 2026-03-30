using Kx.Resty.Commands;
using Kx.Resty.Models;
using Xunit;

namespace Kx.Resty.UnitTests.Commands;

public class HttpFileParserTests
{
    [Fact]
    public void ParseInto_ParsesVariablesBlocksAndQueryParams()
    {
        const string text = """
            @baseUrl = https://example.com
            @token = abc123

            ### List users
            // @no-log
            # @timeout 15
            GET {{baseUrl}}/users?page=1&size=20
            Accept: application/json

            ### Create user
            POST https://example.com/users
            Content-Type: application/json

            {
                            "name": "resty"
            }
            """;

        var collection = new HttpCollection();

        HttpFileParser.ParseInto(collection, text);

        Assert.Collection(
            collection.Variables,
            variable =>
            {
                Assert.Equal("baseUrl", variable.Name);
                Assert.Equal("https://example.com", variable.Value);
            },
            variable =>
            {
                Assert.Equal("token", variable.Name);
                Assert.Equal("abc123", variable.Value);
            });

        Assert.Equal(2, collection.Requests.Count);

        var first = collection.Requests[0];
        Assert.Equal("List users", first.Name);
        Assert.Equal("GET", first.Method);
        Assert.Equal("{{baseUrl}}/users", first.Url);
        Assert.True(first.Annotations.NoLog);
        Assert.Equal(15, first.Annotations.TimeoutSeconds);
        Assert.Collection(
            first.QueryParams,
            param =>
            {
                Assert.Equal("page", param.Key);
                Assert.Equal("1", param.Value);
            },
            param =>
            {
                Assert.Equal("size", param.Key);
                Assert.Equal("20", param.Value);
            });

        var second = collection.Requests[1];
        Assert.Equal("Create user", second.Name);
        Assert.Equal("POST", second.Method);
        Assert.Equal("https://example.com/users", second.Url);
        Assert.Contains("\"name\": \"resty\"", second.Body);
    }

    [Fact]
    public void ParseInto_ParsesBareUrlAndBodyFileReference()
    {
        const string text = """
            ### Raw body file
            https://example.com/export

            < ./payload.json
            """;

        var collection = new HttpCollection();

        HttpFileParser.ParseInto(collection, text);

        var request = Assert.Single(collection.Requests);
        Assert.Equal("Raw body file", request.Name);
        Assert.Equal("GET", request.Method);
        Assert.Equal("https://example.com/export", request.Url);
        Assert.Equal("./payload.json", request.BodyFilePath);
        Assert.Equal(string.Empty, request.Body);
    }

    [Fact]
    public void ParseInto_JoinsIndentedRequestLineContinuation()
    {
        const string text = """
            ### Continued URL
            GET https://example.com/api/
              v1/users?role=admin
            """;

        var collection = new HttpCollection();

        HttpFileParser.ParseInto(collection, text);

        var request = Assert.Single(collection.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal("https://example.com/api/v1/users", request.Url);
        Assert.Equal("admin", Assert.Single(request.QueryParams).Value);
    }

    [Fact]
    public void ParseInto_SkipsEmptyBlocks()
    {
        const string text = """
            ### First
            GET https://example.com/a

            ###


            ### Third
            POST https://example.com/c
            """;

        var collection = new HttpCollection();

        HttpFileParser.ParseInto(collection, text);

        Assert.Equal(2, collection.Requests.Count);
        Assert.Equal("First", collection.Requests[0].Name);
        Assert.Equal("Third", collection.Requests[1].Name);
    }

    [Fact]
    public void ParseInto_HandlesMultipleAnnotationsOnSingleRequest()
    {
        const string text = """
            ### Complex
            // @no-log
            // @no-redirect
            // @no-cookie-jar
            # @timeout 45
            // @connection-timeout 10
            GET https://example.com/test
            """;

        var collection = new HttpCollection();

        HttpFileParser.ParseInto(collection, text);

        var request = Assert.Single(collection.Requests);
        Assert.True(request.Annotations.NoLog);
        Assert.True(request.Annotations.NoRedirect);
        Assert.True(request.Annotations.NoCookieJar);
        Assert.Equal(45, request.Annotations.TimeoutSeconds);
        Assert.Equal(10, request.Annotations.ConnectionTimeoutSeconds);
    }
}