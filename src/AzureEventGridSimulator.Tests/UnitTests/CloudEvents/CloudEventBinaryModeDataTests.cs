using System.Text.Json;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.CloudEvents;

/// <summary>
///     How a binary-mode CloudEvent body becomes Data. The body is read with the default JSON
///     reader rules (no trailing commas, no comments, max depth 64). Single-event structured mode
///     rejects the same inputs, because ParseStructuredMode first runs JsonDocument.Parse with
///     default options. Only batch mode, which deserialises with
///     JsonSerializerOptionsProvider.Default, allows trailing commas. In binary mode, anything
///     those rules reject becomes a string rather than an error.
/// </summary>
[Trait("Category", "unit")]
public class CloudEventBinaryModeDataTests
{
    private static readonly string NestedArrays64 = new string('[', 64) + new string(']', 64);
    private static readonly string NestedArrays65 = new string('[', 65) + new string(']', 65);

    private static object? ParseData(string body)
    {
        var events = new CloudEventSchemaParser(new EventSchemaDetector()).Parse(
            TestHelpers.CreateCloudEventsBinaryModeContext(),
            body
        );

        return events.ShouldHaveSingleItem().CloudEvent.ShouldNotBeNullAnd().Data;
    }

    [Theory]
    [InlineData("""{"a": 1, "b": [true, null]}""", """{"a": 1, "b": [true, null]}""")]
    [InlineData("  {\"a\":1}  \n", """{"a":1}""")]
    [InlineData("""{ "name" : "Zoë <admin>" }""", """{ "name" : "Zoë <admin>" }""")]
    [InlineData("[1, 2]", "[1, 2]")]
    [InlineData("42", "42")]
    [InlineData("1e400", "1e400")]
    [InlineData("\"text\"", "\"text\"")]
    [InlineData("true", "true")]
    public void GivenJsonBody_WhenParsed_ThenDataIsJsonElementWithTheBodyText(
        string body,
        string expectedRawText
    )
    {
        var data = ParseData(body);

        data.ShouldBeOfType<JsonElement>().GetRawText().ShouldBe(expectedRawText);
    }

    [Fact]
    public void GivenBodyNestedToMaxDepth_WhenParsed_ThenDataIsJsonElement()
    {
        var data = ParseData(NestedArrays64);

        data.ShouldBeOfType<JsonElement>().GetRawText().ShouldBe(NestedArrays64);
    }

    [Theory]
    [InlineData("""{"a":1,}""")]
    [InlineData("[1,2,]")]
    [InlineData("""{"a":1} // comment""")]
    [InlineData("""/* comment */ {"a":1}""")]
    [InlineData("""{'a':1}""")]
    [InlineData("""{"a":1} trailing""")]
    [InlineData("NaN")]
    [InlineData("plain text")]
    public void GivenBodyTheDefaultJsonRulesReject_WhenParsed_ThenDataIsTheBodyString(string body)
    {
        ParseData(body).ShouldBe(body);
    }

    [Fact]
    public void GivenBodyWithLeadingByteOrderMark_WhenParsed_ThenDataIsTheBodyString()
    {
        var body = (char)0xFEFF + """{"a":1}""";

        ParseData(body).ShouldBe(body);
    }

    [Fact]
    public void GivenBodyNestedBeyondMaxDepth_WhenParsed_ThenDataIsTheBodyString()
    {
        ParseData(NestedArrays65).ShouldBe(NestedArrays65);
    }

    [Theory]
    [InlineData("null")]
    [InlineData(" null ")]
    public void GivenJsonNullBody_WhenParsed_ThenDataIsNull(string body)
    {
        ParseData(body).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenBlankBody_WhenParsed_ThenDataIsNull(string body)
    {
        ParseData(body).ShouldBeNull();
    }
}
