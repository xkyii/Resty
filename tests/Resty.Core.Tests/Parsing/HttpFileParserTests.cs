using Resty.Core.Parsing;

namespace Resty.Core.Tests.Parsing;

public class HttpFileParserTests
{
    // ---- Basic GET request ----

    [Fact]
    public void Parse_SimpleGet_ReturnsOneRequest()
    {
        const string content = """
            ### Get User
            GET https://example.com/api/users/1
            Accept: application/json
            """;

        var file = HttpFileParser.ParseContent(content);

        Assert.Single(file.Requests);
        var req = file.Requests[0];
        Assert.Equal("Get User", req.Name);
        Assert.Equal("GET", req.Method);
        Assert.Equal("https://example.com/api/users/1", req.Url);
        Assert.Equal("application/json", req.Headers["Accept"]);
    }

    // ---- Multiple requests ----

    [Fact]
    public void Parse_MultipleRequests_ReturnsAll()
    {
        const string content = """
            ### Get User
            GET https://example.com/api/users/1

            ###

            ### Create User
            POST https://example.com/api/users
            Content-Type: application/json

            {"name": "John"}
            """;

        var file = HttpFileParser.ParseContent(content);

        Assert.Equal(2, file.Requests.Count);
        Assert.Equal("GET", file.Requests[0].Method);
        Assert.Equal("POST", file.Requests[1].Method);
    }

    // ---- Request body ----

    [Fact]
    public void Parse_PostWithBody_CapturesBody()
    {
        const string content = """
            ### Create User
            POST https://example.com/api/users
            Content-Type: application/json

            {"name": "John Doe"}
            """;

        var file = HttpFileParser.ParseContent(content);

        var req = file.Requests[0];
        Assert.NotNull(req.Body);
        Assert.Contains("John Doe", req.Body);
    }

    // ---- File-level variables ----

    [Fact]
    public void Parse_FileVariables_ExtractedCorrectly()
    {
        const string content = """
            @host = localhost:8080
            @token = abc123

            ### Get
            GET https://{{host}}/api/users
            """;

        var file = HttpFileParser.ParseContent(content);

        Assert.Equal("localhost:8080", file.FileVariables["host"]);
        Assert.Equal("abc123", file.FileVariables["token"]);
    }

    // ---- Assertion block ----

    [Fact]
    public void Parse_AssertionBlock_ParsedIntoRules()
    {
        const string content = """
            ### Get User
            GET https://example.com/api/users/1

            > {%
            assert status == 200
            assert responseTime < 500
            assert body.$.name == "John"
            %}
            """;

        var file = HttpFileParser.ParseContent(content);
        var req = file.Requests[0];

        Assert.Equal(3, req.Assertions.Count);
        Assert.Equal("assert status == 200", req.Assertions[0].RawText);
        Assert.Equal("assert responseTime < 500", req.Assertions[1].RawText);
        Assert.Equal("assert body.$.name == \"John\"", req.Assertions[2].RawText);
    }

    // ---- Anonymous request (no name after ###) ----

    [Fact]
    public void Parse_AnonymousRequest_EmptyName()
    {
        const string content = """
            ###
            GET https://example.com/ping
            """;

        var file = HttpFileParser.ParseContent(content);

        Assert.Single(file.Requests);
        Assert.Equal(string.Empty, file.Requests[0].Name);
    }

    // ---- Case-insensitive headers ----

    [Fact]
    public void Parse_Headers_CaseInsensitiveLookup()
    {
        const string content = """
            ### Test
            GET https://example.com
            authorization: Bearer token123
            content-type: application/json
            """;

        var file = HttpFileParser.ParseContent(content);
        var headers = file.Requests[0].Headers;

        Assert.Equal("Bearer token123", headers["Authorization"]);
        Assert.Equal("application/json", headers["Content-Type"]);
    }

    // ---- HTTP version stripped from URL ----

    [Fact]
    public void Parse_RequestLineWithHttpVersion_VersionStripped()
    {
        const string content = """
            ### Test
            GET https://example.com/api HTTP/1.1
            """;

        var file = HttpFileParser.ParseContent(content);

        Assert.Equal("https://example.com/api", file.Requests[0].Url);
    }
}
