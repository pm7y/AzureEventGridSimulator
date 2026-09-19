using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Delivery;

[Trait("Category", "unit")]
public class DeliveryPropertyResolverTests
{
    private readonly DeliveryPropertyResolver _resolver = new();

    private static SimulatorEvent CreateTestEvent()
    {
        return SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = "test-event-id",
                Subject = "test/subject",
                EventType = "Test.EventType",
                EventTime = "2024-01-15T10:30:00Z",
                DataVersion = "1.0",
                Data = new
                {
                    customerId = "cust-123",
                    amount = 99.99,
                    isActive = true,
                    count = 42,
                    order = new { id = "order-456", total = 150.50 },
                },
            }
        );
    }

    [Fact]
    public void ResolveProperties_WithNullProperties_ShouldReturnEmptyDictionary()
    {
        var result = _resolver.ResolveProperties(null, CreateTestEvent());

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveProperties_WithEmptyProperties_ShouldReturnEmptyDictionary()
    {
        var properties = new Dictionary<string, DeliveryPropertySettings>();

        var result = _resolver.ResolveProperties(properties, CreateTestEvent());

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveProperty_StaticProperty_ShouldReturnLiteralValue()
    {
        var setting = new DeliveryPropertySettings { Type = "static", Value = "fixed-value" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBe("fixed-value");
    }

    [Theory]
    [InlineData("Subject", "test/subject")]
    [InlineData("subject", "test/subject")]
    [InlineData("Id", "test-event-id")]
    [InlineData("id", "test-event-id")]
    [InlineData("EventType", "Test.EventType")]
    [InlineData("eventType", "Test.EventType")]
    [InlineData("Type", "Test.EventType")]
    [InlineData("type", "Test.EventType")]
    [InlineData("DataVersion", "1.0")]
    [InlineData("dataVersion", "1.0")]
    public void ResolveProperty_DynamicTopLevelProperty_ShouldReturnEventValue(
        string path,
        string expectedValue
    )
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = path };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBe(expectedValue);
    }

    [Fact]
    public void ResolveProperty_DynamicEventTime_ShouldReturnString()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "EventTime" };
        var evt = CreateTestEvent();

        var result = _resolver.ResolveProperty(setting, evt);

        result.ShouldBe("2024-01-15T10:30:00Z");
    }

    [Fact]
    public void ResolveProperty_DynamicNestedDataProperty_ShouldReturnValue()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "data.customerId" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBe("cust-123");
    }

    [Fact]
    public void ResolveProperty_DynamicNestedDataProperty_CaseInsensitive()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "Data.CustomerID" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBe("cust-123");
    }

    [Fact]
    public void ResolveProperty_DynamicDeepNestedDataProperty_ShouldReturnValue()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "data.order.id" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBe("order-456");
    }

    [Theory]
    [InlineData("data.amount", 99.99)]
    [InlineData("data.order.total", 150.50)]
    public void ResolveProperty_DynamicNumericDataProperty_ShouldReturnNumber(
        string path,
        double expectedValue
    )
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = path };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBe(expectedValue);
    }

    [Fact]
    public void ResolveProperty_DynamicBooleanDataProperty_ShouldReturnBoolean()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "data.isActive" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBe(true);
    }

    [Fact]
    public void ResolveProperty_DynamicIntegerDataProperty_ShouldReturnLong()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "data.count" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBe(42L);
    }

    [Fact]
    public void ResolveProperty_DynamicNonExistentPath_ShouldReturnNull()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "data.nonExistent" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveProperty_DynamicInvalidTopLevelProperty_ShouldReturnNull()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "InvalidProperty" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveProperty_WithNullSetting_ShouldReturnNull()
    {
        var result = _resolver.ResolveProperty(null!, CreateTestEvent());

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveProperty_WithEmptyPath_ShouldReturnNull()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveProperty_DataProperty_ShouldReturnDataObject()
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "Data" };

        var result = _resolver.ResolveProperty(setting, CreateTestEvent());

        result.ShouldNotBeNull();
    }

    [Fact]
    public void ResolveProperties_MultipleProperties_ShouldResolveAll()
    {
        var properties = new Dictionary<string, DeliveryPropertySettings>
        {
            ["Label"] = new() { Type = "dynamic", Value = "Subject" },
            ["Region"] = new() { Type = "static", Value = "west-us" },
            ["CustomerId"] = new() { Type = "dynamic", Value = "data.customerId" },
        };

        var result = _resolver.ResolveProperties(properties, CreateTestEvent());

        result.Count.ShouldBe(3);
        result["Label"].ShouldBe("test/subject");
        result["Region"].ShouldBe("west-us");
        result["CustomerId"].ShouldBe("cust-123");
    }

    [Fact]
    public void ResolveProperties_WithNullDynamicValue_ShouldNotIncludeProperty()
    {
        var properties = new Dictionary<string, DeliveryPropertySettings>
        {
            ["Label"] = new() { Type = "dynamic", Value = "Subject" },
            ["Missing"] = new() { Type = "dynamic", Value = "data.nonExistent" },
        };

        var result = _resolver.ResolveProperties(properties, CreateTestEvent());

        result.Count.ShouldBe(1);
        result.ShouldContainKey("Label");
        result.ShouldNotContainKey("Missing");
    }

    [Fact]
    public void ResolveProperty_WithNullEventData_ShouldReturnNull()
    {
        var evt = SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = "test-id",
                Subject = "test",
                EventType = "Test",
                EventTime = "2024-01-15T10:30:00Z",
                Data = null,
            }
        );
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = "data.customerId" };

        var result = _resolver.ResolveProperty(setting, evt);

        result.ShouldBeNull();
    }

    private const string ParsedPayload = """
        {
          "text": "Zo\u00EB <admin> & 'co'",
          "whole": 42,
          "big": 12345678901234567,
          "fraction": 1.50,
          "exponent": 1e3,
          "flag": true,
          "nothing": null,
          "when": "2025-01-15T10:30:00Z",
          "ref": "7d444840-9dc0-11d1-b245-5ffdce74fad2",
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

    /// <summary>
    ///     Data parsed by the real Event Grid parser, so it is a boxed JsonElement as in production.
    /// </summary>
    private static SimulatorEvent CreateParsedJsonEvent(string dataJson)
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

    public static TheoryData<string, object?> ParsedJsonPaths()
    {
        return new TheoryData<string, object?>
        {
            { "data.text", "Zoë <admin> & 'co'" },
            { "data.whole", 42L },
            { "data.big", 12345678901234567L },
            { "data.fraction", 1.5d },
            { "data.exponent", 1000d },
            { "data.flag", true },
            { "data.nothing", null },
            { "data.missing", null },
            { "data.text.deeper", null },
            { "data.when", new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero) },
            { "data.ref", new Guid("7d444840-9dc0-11d1-b245-5ffdce74fad2") },
            // Objects and arrays read as compact text re-escaped by the default encoder
            { "data.tags", """["a",2,{"b":"Zo\u00EB \u003Cx\u003E"}]""" },
            { "data.obj", """{"name":"Zo\u00EB \u003Cadmin\u003E","n":1.0,"inner":{"k":[1,2]}}""" },
            { "data.obj.inner", """{"k":[1,2]}""" },
            { "data.obj.inner.k", "[1,2]" },
            { "data.OBJ.NAME", "Zoë <admin>" },
            // The first case-insensitive match wins, even over an exact-case key
            { "data.Customer.NAME", "exact" },
            { "data.dup", "first" },
        };
    }

    [Theory]
    [MemberData(nameof(ParsedJsonPaths))]
    public void GivenParsedJsonData_WhenDynamicNestedPathResolved_ThenReturnsExpectedValue(
        string path,
        object? expected
    )
    {
        var setting = new DeliveryPropertySettings { Type = "dynamic", Value = path };

        var result = _resolver.ResolveProperty(setting, CreateParsedJsonEvent(ParsedPayload));

        result.ShouldBe(expected);
        result?.GetType().ShouldBe(expected?.GetType());
    }

    [Fact]
    public void GivenStructuredBatchPayloadWithTrailingCommas_WhenNestedObjectResolved_ThenReturnsCompactText()
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

        _resolver
            .ResolveProperty(
                new DeliveryPropertySettings { Type = "dynamic", Value = "data.obj" },
                evt
            )
            .ShouldBe("""{"a":1,"b":[1,2]}""");
        _resolver
            .ResolveProperty(
                new DeliveryPropertySettings { Type = "dynamic", Value = "data.obj.b" },
                evt
            )
            .ShouldBe("[1,2]");
    }
}
