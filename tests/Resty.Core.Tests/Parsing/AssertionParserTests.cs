using Resty.Core.Models;
using Resty.Core.Parsing;

namespace Resty.Core.Tests.Parsing;

public class AssertionParserTests
{
    private static AssertionRule ParseSingle(string line)
    {
        var rules = AssertionParser.ParseBlock([line]);
        Assert.Single(rules);
        return rules[0];
    }

    // ---- Status ----

    [Fact]
    public void Parse_StatusEqual_Correct()
    {
        var rule = ParseSingle("assert status == 200");

        Assert.Equal(AssertionSubjectType.Status, rule.SubjectType);
        Assert.Equal(AssertionOperator.Equal, rule.Operator);
        Assert.Equal("200", rule.ExpectedValue);
    }

    [Fact]
    public void Parse_StatusIn_Correct()
    {
        var rule = ParseSingle("assert status in [200, 201]");

        Assert.Equal(AssertionSubjectType.Status, rule.SubjectType);
        Assert.Equal(AssertionOperator.In, rule.Operator);
        Assert.Equal("[200, 201]", rule.ExpectedValue);
    }

    // ---- ResponseTime ----

    [Fact]
    public void Parse_ResponseTimeLessThan_Correct()
    {
        var rule = ParseSingle("assert responseTime < 500");

        Assert.Equal(AssertionSubjectType.ResponseTime, rule.SubjectType);
        Assert.Equal(AssertionOperator.LessThan, rule.Operator);
        Assert.Equal("500", rule.ExpectedValue);
    }

    // ---- Body JSONPath ----

    [Fact]
    public void Parse_BodyJsonPath_Correct()
    {
        var rule = ParseSingle("assert body.$.name == \"John\"");

        Assert.Equal(AssertionSubjectType.BodyJsonPath, rule.SubjectType);
        Assert.Equal("$.name", rule.SubjectPath);
        Assert.Equal(AssertionOperator.Equal, rule.Operator);
        Assert.Equal("\"John\"", rule.ExpectedValue);
    }

    [Fact]
    public void Parse_BodyJsonPathNotNull_Correct()
    {
        var rule = ParseSingle("assert body.$.id != null");

        Assert.Equal(AssertionSubjectType.BodyJsonPath, rule.SubjectType);
        Assert.Equal(AssertionOperator.NotEqual, rule.Operator);
        Assert.Equal("null", rule.ExpectedValue);
    }

    // ---- Header ----

    [Fact]
    public void Parse_HeaderContains_Correct()
    {
        var rule = ParseSingle("assert header[\"Content-Type\"] contains \"application/json\"");

        Assert.Equal(AssertionSubjectType.Header, rule.SubjectType);
        Assert.Equal("Content-Type", rule.SubjectPath);
        Assert.Equal(AssertionOperator.Contains, rule.Operator);
        Assert.Equal("\"application/json\"", rule.ExpectedValue);
    }

    [Fact]
    public void Parse_HeaderEqual_Correct()
    {
        var rule = ParseSingle("assert header[\"X-Custom\"] == \"value\"");

        Assert.Equal(AssertionSubjectType.Header, rule.SubjectType);
        Assert.Equal("X-Custom", rule.SubjectPath);
        Assert.Equal(AssertionOperator.Equal, rule.Operator);
    }

    // ---- Non-assert lines are ignored ----

    [Fact]
    public void Parse_NonAssertLines_Ignored()
    {
        var rules = AssertionParser.ParseBlock(["// comment", "var x = 1;", "assert status == 200"]);

        Assert.Single(rules);
    }

    // ---- All operators ----

    [Theory]
    [InlineData("assert status == 200", AssertionOperator.Equal)]
    [InlineData("assert status != 200", AssertionOperator.NotEqual)]
    [InlineData("assert status < 300", AssertionOperator.LessThan)]
    [InlineData("assert status <= 299", AssertionOperator.LessThanOrEqual)]
    [InlineData("assert status > 100", AssertionOperator.GreaterThan)]
    [InlineData("assert status >= 200", AssertionOperator.GreaterThanOrEqual)]
    public void Parse_AllNumericOperators(string line, AssertionOperator expected)
    {
        var rule = ParseSingle(line);
        Assert.Equal(expected, rule.Operator);
    }
}
