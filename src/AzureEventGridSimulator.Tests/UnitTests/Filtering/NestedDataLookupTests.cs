using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;
using Op = AzureEventGridSimulator.Infrastructure.Settings.AdvancedFilterSetting.AdvancedFilterOperatorType;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

/// <summary>
///     Nested data.* filter keys evaluated against event data produced by the real parsers, where
///     Data is a boxed JsonElement rather than the anonymous objects the other filter tests use.
///     An object value reads as compact JSON text re-escaped by the default encoder, not as the
///     client sent it, so these rows pin that text exactly.
/// </summary>
[Trait("Category", "unit")]
public class NestedDataLookupTests
{
    private const string Payload = """
        {
          "text": "Zo\u00EB <admin> & 'co'",
          "whole": 42,
          "big": 12345678901234567,
          "fraction": 1.50,
          "exponent": 1e3,
          "flag": true,
          "nothing": null,
          "tags": [ "a", 2, { "b" : "Zoë <x>" } ],
          "obj": {
            "name" : "Zoë <admin>",
            "n" : 1.0,
            "inner" : { "k" : [ 1 , 2 ] }
          },
          "Customer": { "Name": "exact", "NAME": "other" },
          "dup": "first",
          "dup": "second"
        }
        """;

    private const string ObjText =
        """{"name":"Zo\u00EB \u003Cadmin\u003E","n":1.0,"inner":{"k":[1,2]}}""";

    private const string TagObjectText = """{"b":"Zo\u00EB \u003Cx\u003E"}""";

    private static SimulatorEvent ParseEventGridEvent(string dataJson)
    {
        var body = $$"""
            [{
              "id": "1",
              "subject": "test/subject",
              "eventType": "Test.EventType",
              "eventTime": "2025-01-15T10:30:00Z",
              "dataVersion": "1.0",
              "data": {{dataJson}}
            }]
            """;

        return new EventGridSchemaParser()
            .Parse(TestHelpers.CreateHttpContext(), body)
            .ShouldHaveSingleItem();
    }

    private static SimulatorEvent ParseBinaryModeCloudEvent(string body)
    {
        return new CloudEventSchemaParser(new EventSchemaDetector())
            .Parse(TestHelpers.CreateCloudEventsBinaryModeContext(), body)
            .ShouldHaveSingleItem();
    }

    private static bool Accepts(
        SimulatorEvent evt,
        string key,
        Op operatorType,
        object[]? values = null,
        bool arrays = false,
        object? value = null
    )
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = arrays,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = key,
                    OperatorType = operatorType,
                    Value = value,
                    Values = values,
                },
            ],
        };

        return filter.AcceptsEvent(evt);
    }

    [Theory]
    [InlineData("data.text", "Zoë <admin> & 'co'")]
    [InlineData("DATA.text", "Zoë <admin> & 'co'")]
    [InlineData("data.whole", "42")]
    [InlineData("data.big", "12345678901234567")]
    [InlineData("data.flag", "True")]
    [InlineData("data.obj", ObjText)]
    [InlineData("data.obj.inner", """{"k":[1,2]}""")]
    [InlineData("data.OBJ.NAME", "Zoë <admin>")]
    [InlineData("data.Customer.Name", "exact")]
    [InlineData("data.Customer.NAME", "other")]
    [InlineData("data.customer.name", "exact")]
    [InlineData("data.customer.nAmE", "exact")]
    [InlineData("data.dup", "second")]
    public void GivenParsedJsonData_WhenNestedKeyFilteredWithStringIn_ThenValueReadsAsExpectedText(
        string key,
        string expectedText
    )
    {
        var evt = ParseEventGridEvent(Payload);

        Accepts(evt, key, Op.StringIn, [expectedText]).ShouldBeTrue();
        Accepts(evt, key, Op.StringIn, [expectedText + "-"]).ShouldBeFalse();
    }

    [Theory]
    [InlineData("data.whole", 42)]
    [InlineData("data.big", 12345678901234567d)]
    [InlineData("data.fraction", 1.5)]
    [InlineData("data.exponent", 1000)]
    [InlineData("data.obj.n", 1)]
    public void GivenParsedJsonData_WhenNestedNumberFilteredWithNumberIn_ThenMatches(
        string key,
        double expected
    )
    {
        var evt = ParseEventGridEvent(Payload);

        Accepts(evt, key, Op.NumberIn, [expected]).ShouldBeTrue();
        Accepts(evt, key, Op.NumberIn, [-1]).ShouldBeFalse();
    }

    [Fact]
    public void GivenParsedJsonData_WhenNestedBoolFiltered_ThenBoolEqualsMatches()
    {
        var evt = ParseEventGridEvent(Payload);

        Accepts(evt, "data.flag", Op.BoolEquals, value: true).ShouldBeTrue();
        Accepts(evt, "data.flag", Op.BoolEquals, value: false).ShouldBeFalse();
    }

    [Fact]
    public void GivenParsedJsonData_WhenNestedObjectFilteredWithClientText_ThenDoesNotMatch()
    {
        var evt = ParseEventGridEvent(Payload);

        Accepts(evt, "data.obj.inner", Op.StringIn, ["""{ "k" : [ 1 , 2 ] }"""]).ShouldBeFalse();
    }

    [Fact]
    public void GivenParsedJsonNullValue_WhenFiltered_ThenKeyExistsWithNullValue()
    {
        var evt = ParseEventGridEvent(Payload);

        Accepts(evt, "data.nothing", Op.IsNullOrUndefined).ShouldBeTrue();
        Accepts(evt, "data.nothing", Op.IsNotNull).ShouldBeFalse();
        // A present null can't be read as a number, so the negated operator doesn't match;
        // a missing key does match NumberNotIn.
        Accepts(evt, "data.nothing", Op.NumberNotIn, [1]).ShouldBeFalse();
        Accepts(evt, "data.missing", Op.NumberNotIn, [1]).ShouldBeTrue();
    }

    [Theory]
    [InlineData("data.missing")]
    [InlineData("data.text.deeper")]
    [InlineData("data.tags.a")]
    public void GivenParsedJsonData_WhenNestedKeyDoesNotResolve_ThenTreatedAsMissing(string key)
    {
        var evt = ParseEventGridEvent(Payload);

        Accepts(evt, key, Op.IsNullOrUndefined).ShouldBeTrue();
        Accepts(evt, key, Op.IsNotNull).ShouldBeFalse();
        Accepts(evt, key, Op.StringNotIn, ["x"]).ShouldBeTrue();
    }

    [Fact]
    public void GivenParsedJsonArray_WhenFilteredWithoutArrayFiltering_ThenNoElementMatches()
    {
        var evt = ParseEventGridEvent(Payload);

        Accepts(evt, "data.tags", Op.StringIn, ["a"]).ShouldBeFalse();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("2")]
    [InlineData(TagObjectText)]
    public void GivenParsedJsonArray_WhenFilteredWithArrayFiltering_ThenAnyElementMatches(
        string value
    )
    {
        var evt = ParseEventGridEvent(Payload);

        Accepts(evt, "data.tags", Op.StringIn, [value], arrays: true).ShouldBeTrue();
    }

    [Fact]
    public void GivenParsedJsonArray_WhenFilteredWithNumberInOnArrays_ThenNumericElementMatches()
    {
        var evt = ParseEventGridEvent(Payload);

        Accepts(evt, "data.tags", Op.NumberIn, [2], arrays: true).ShouldBeTrue();
        Accepts(evt, "data.obj.inner.k", Op.NumberIn, [3], arrays: true).ShouldBeFalse();
    }

    [Fact]
    public void GivenStructuredBatchPayloadWithTrailingCommas_WhenNestedObjectFiltered_ThenValueReadsAsCompactText()
    {
        const string body = """
            [
              {
                "specversion": "1.0",
                "id": "1",
                "source": "/test/source",
                "type": "Test.EventType",
                "data": { "obj": { "a": 1, "b": [ 1, 2, ], }, },
              },
            ]
            """;
        var evt = new CloudEventSchemaParser(new EventSchemaDetector())
            .Parse(TestHelpers.CreateCloudEventsBatchModeContext(), body)
            .ShouldHaveSingleItem();

        Accepts(evt, "data.obj", Op.StringIn, ["""{"a":1,"b":[1,2]}"""]).ShouldBeTrue();
        Accepts(evt, "data.obj.b", Op.NumberIn, [2], arrays: true).ShouldBeTrue();
    }

    [Fact]
    public void GivenTopLevelDataArray_WhenDataKeyFilteredOnArrays_ThenObjectElementsKeepClientText()
    {
        // The whole-data key hands the parsed array straight to array filtering, so its object
        // elements read as the client's own text, unlike objects reached through data.* keys.
        var evt = ParseEventGridEvent("""[ { "a" : 1 } , "x" ]""");

        Accepts(evt, "data", Op.StringIn, ["""{ "a" : 1 }"""], arrays: true).ShouldBeTrue();
        Accepts(evt, "data", Op.StringIn, ["""{"a":1}"""], arrays: true).ShouldBeFalse();
        Accepts(evt, "data", Op.StringIn, ["x"], arrays: true).ShouldBeTrue();
    }

    [Fact]
    public void GivenBinaryModeJsonBody_WhenNestedKeyFiltered_ThenResolvesFromParsedBody()
    {
        var evt = ParseBinaryModeCloudEvent("""{ "customer": { "name": "Zoë", "tier": 3 } }""");

        Accepts(evt, "data.customer.name", Op.StringIn, ["zoë"]).ShouldBeTrue();
        Accepts(evt, "data.Customer.Tier", Op.NumberIn, [3]).ShouldBeTrue();
    }

    [Fact]
    public void GivenBinaryModeNonJsonBody_WhenNestedKeyFiltered_ThenTreatedAsMissing()
    {
        var evt = ParseBinaryModeCloudEvent("plain text");

        Accepts(evt, "data.customer", Op.IsNullOrUndefined).ShouldBeTrue();
        Accepts(evt, "data", Op.StringIn, ["plain text"]).ShouldBeTrue();
    }
}
