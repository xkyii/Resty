using Resty.Core.Assertions;
using Resty.Core.Models;
using Resty.Core.Parsing;

namespace Resty.Core.Tests.Assertions;

public class AssertionEngineTests
{
    private static HttpExecutionResult MakeResponse(
        int status = 200,
        string body = "{}",
        long elapsedMs = 100,
        Dictionary<string, string>? headers = null) =>
        new()
        {
            StatusCode = status,
            Body = body,
            ElapsedMs = elapsedMs,
            Headers = headers ?? new(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json; charset=utf-8",
            },
        };

    private static List<AssertionRule> ParseRules(params string[] lines) =>
        AssertionParser.ParseBlock(lines);

    // ---- Status ----

    [Fact]
    public void Status_Equal_Pass()
    {
        var rules = ParseRules("assert status == 200");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(200));
        Assert.True(results[0].Passed);
    }

    [Fact]
    public void Status_Equal_Fail()
    {
        var rules = ParseRules("assert status == 201");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(200));
        Assert.False(results[0].Passed);
        Assert.Equal("200", results[0].ActualValue);
    }

    [Fact]
    public void Status_In_Pass()
    {
        var rules = ParseRules("assert status in [200, 201]");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(201));
        Assert.True(results[0].Passed);
    }

    [Fact]
    public void Status_In_Fail()
    {
        var rules = ParseRules("assert status in [200, 201]");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(400));
        Assert.False(results[0].Passed);
    }

    // ---- ResponseTime ----

    [Fact]
    public void ResponseTime_LessThan_Pass()
    {
        var rules = ParseRules("assert responseTime < 500");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(elapsedMs: 100));
        Assert.True(results[0].Passed);
    }

    [Fact]
    public void ResponseTime_LessThan_Fail()
    {
        var rules = ParseRules("assert responseTime < 500");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(elapsedMs: 600));
        Assert.False(results[0].Passed);
    }

    // ---- Body JSONPath ----

    [Fact]
    public void BodyJsonPath_Equal_Pass()
    {
        const string body = """{"name": "John Doe"}""";
        var rules = ParseRules("assert body.$.name == \"John Doe\"");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(body: body));
        Assert.True(results[0].Passed);
    }

    [Fact]
    public void BodyJsonPath_Equal_Fail()
    {
        const string body = """{"name": "Jane"}""";
        var rules = ParseRules("assert body.$.name == \"John\"");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(body: body));
        Assert.False(results[0].Passed);
        Assert.Equal("Jane", results[0].ActualValue);
    }

    [Fact]
    public void BodyJsonPath_NotNull_Pass()
    {
        const string body = """{"id": 42}""";
        var rules = ParseRules("assert body.$.id != null");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(body: body));
        Assert.True(results[0].Passed);
    }

    [Fact]
    public void BodyJsonPath_NotNull_Fail_WhenFieldMissing()
    {
        const string body = """{"name": "John"}""";
        var rules = ParseRules("assert body.$.id != null");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(body: body));
        Assert.False(results[0].Passed);
    }

    [Fact]
    public void BodyJsonPath_NestedField_Pass()
    {
        const string body = """{"user": {"email": "john@example.com"}}""";
        var rules = ParseRules("assert body.$.user.email == \"john@example.com\"");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(body: body));
        Assert.True(results[0].Passed);
    }

    // ---- Header ----

    [Fact]
    public void Header_Contains_Pass()
    {
        var rules = ParseRules("assert header[\"Content-Type\"] contains \"application/json\"");
        var results = AssertionEngine.Evaluate(rules, MakeResponse());
        Assert.True(results[0].Passed);
    }

    [Fact]
    public void Header_Equal_Pass()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Custom"] = "my-value",
        };
        var rules = ParseRules("assert header[\"X-Custom\"] == \"my-value\"");
        var results = AssertionEngine.Evaluate(rules, MakeResponse(headers: headers));
        Assert.True(results[0].Passed);
    }

    [Fact]
    public void Header_Missing_Fail()
    {
        var rules = ParseRules("assert header[\"X-Missing\"] == \"value\"");
        var results = AssertionEngine.Evaluate(rules, MakeResponse());
        Assert.False(results[0].Passed);
        Assert.NotNull(results[0].ErrorMessage);
    }

    // ---- Multiple rules ----

    [Fact]
    public void MultipleRules_AllPass()
    {
        const string body = """{"id": 1, "name": "John"}""";
        var rules = ParseRules(
            "assert status == 200",
            "assert responseTime < 1000",
            "assert body.$.name == \"John\"");

        var results = AssertionEngine.Evaluate(rules, MakeResponse(200, body));

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Passed));
    }

    [Fact]
    public void MultipleRules_SomeFailDoNotAbort()
    {
        const string body = """{"name": "Wrong"}""";
        var rules = ParseRules(
            "assert status == 200",
            "assert body.$.name == \"John\"");

        var results = AssertionEngine.Evaluate(rules, MakeResponse(200, body));

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Passed);   // status OK
        Assert.False(results[1].Passed);  // name mismatch
    }
}
