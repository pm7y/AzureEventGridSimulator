using System.Globalization;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.EventGrid;

[Trait("Category", "unit")]
public class EventGridSchemaParserTests
{
    private readonly EventGridSchemaParser _parser = new();

    [Fact]
    public void GivenValidEventGridEventsArray_WhenParsed_ThenEventsCreated()
    {
        var context = TestHelpers.CreateHttpContext();
        const string requestBody = """
            [{
                        "id": "test-id-123",
                        "subject": "/test/subject",
                        "eventType": "Test.EventType",
                        "eventTime": "2025-01-15T10:30:00Z",
                        "dataVersion": "1.0",
                        "data": { "Property": "Value" }
                    }]
            """;

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].Schema.ShouldBe(EventSchema.EventGridSchema);
        events[0].EventGridEvent.ShouldNotBeNullAnd().Id.ShouldBe("test-id-123");
        events[0].EventGridEvent.ShouldNotBeNullAnd().Subject.ShouldBe("/test/subject");
        events[0].EventGridEvent.ShouldNotBeNullAnd().EventType.ShouldBe("Test.EventType");
        events[0].EventGridEvent.ShouldNotBeNullAnd().DataVersion.ShouldBe("1.0");
    }

    [Fact]
    public void GivenMultipleEvents_WhenParsed_ThenAllEventsReturned()
    {
        var context = TestHelpers.CreateHttpContext();
        const string requestBody = """
            [
                        {
                            "id": "event-1",
                            "subject": "/test/subject1",
                            "eventType": "Test.EventType1",
                            "eventTime": "2025-01-15T10:30:00Z",
                            "dataVersion": "1.0"
                        },
                        {
                            "id": "event-2",
                            "subject": "/test/subject2",
                            "eventType": "Test.EventType2",
                            "eventTime": "2025-01-15T10:31:00Z",
                            "dataVersion": "2.0"
                        }
                    ]
            """;

        var events = _parser.Parse(context, requestBody);

        events.Length.ShouldBe(2);
        events[0].EventGridEvent.ShouldNotBeNullAnd().Id.ShouldBe("event-1");
        events[0].EventGridEvent.ShouldNotBeNullAnd().EventType.ShouldBe("Test.EventType1");
        events[1].EventGridEvent.ShouldNotBeNullAnd().Id.ShouldBe("event-2");
        events[1].EventGridEvent.ShouldNotBeNullAnd().EventType.ShouldBe("Test.EventType2");
    }

    [Fact]
    public void GivenEventWithAllOptionalFields_WhenParsed_ThenAllFieldsPopulated()
    {
        var context = TestHelpers.CreateHttpContext();
        const string requestBody = """
            [{
                        "id": "test-id-123",
                        "subject": "/test/subject",
                        "eventType": "Test.EventType",
                        "eventTime": "2025-01-15T10:30:00Z",
                        "dataVersion": "1.0",
                        "metadataVersion": "1",
                        "data": { "Key1": "Value1", "Key2": 42 }
                    }]
            """;

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].EventGridEvent.ShouldNotBeNullAnd().MetadataVersion.ShouldBe("1");
        var data = events[0].EventGridEvent.ShouldNotBeNullAnd().Data.ShouldBeOfType<JsonElement>();
        data.GetProperty("Key1").GetString().ShouldBe("Value1");
        data.GetProperty("Key2").GetInt32().ShouldBe(42);
    }

    [Fact]
    public void GivenEventWithMinimalFields_WhenParsed_ThenRequiredFieldsPresent()
    {
        var context = TestHelpers.CreateHttpContext();
        const string requestBody = """
            [{
                        "id": "min-id",
                        "subject": "/min/subject",
                        "eventType": "Min.Type",
                        "eventTime": "2025-01-15T10:30:00Z"
                    }]
            """;

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].EventGridEvent.ShouldNotBeNullAnd().Id.ShouldBe("min-id");
        events[0].EventGridEvent.ShouldNotBeNullAnd().Subject.ShouldBe("/min/subject");
        events[0].EventGridEvent.ShouldNotBeNullAnd().EventType.ShouldBe("Min.Type");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GivenEmptyWhitespaceOrNullBody_WhenParsed_ThenExceptionThrown(string? requestBody)
    {
        var context = TestHelpers.CreateHttpContext();

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody!)
        );
        exception.Message.ShouldContain("Unexpected end when reading JSON");
    }

    [Fact]
    public void GivenMalformedJson_WhenParsed_ThenExceptionThrown()
    {
        var context = TestHelpers.CreateHttpContext();
        const string requestBody = "[{ invalid json }]";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        // The parser passes System.Text.Json's message through unchanged and adds no report
        // suffix; EventGridMiddleware appends the suffix to the response, as Azure does. STJ's
        // exact wording varies between runtime versions, so only its type is pinned here
        // (JsonDocument throws an internal JsonException subclass).
        exception.Message.ShouldNotContain("to our forums");
        exception.InnerException.ShouldBeAssignableTo<JsonException>();
    }

    [Fact]
    public void GivenEmptyArray_WhenParsed_ThenExceptionThrown()
    {
        var context = TestHelpers.CreateHttpContext();
        const string requestBody = "[]";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        exception.Message.ShouldContain("configured to receive event");
    }

    [Fact]
    public void GivenValidEvents_WhenValidated_ThenNoExceptionThrown()
    {
        var events = new[]
        {
            SimulatorEvent.FromEventGridEvent(
                new EventGridEvent
                {
                    Id = "test-id",
                    Subject = "/test/subject",
                    EventType = "Test.EventType",
                    EventTime = "2025-01-15T10:30:00Z",
                }
            ),
        };

        Should.NotThrow(() => _parser.Validate(events));
    }

    [Fact]
    public void GivenEventJsonWithMissingId_WhenParsed_ThenExceptionThrown()
    {
        var context = TestHelpers.CreateHttpContext();
        const string requestBody = """
            [{
                "subject": "/test/subject",
                "eventType": "Test.EventType",
                "eventTime": "2025-01-15T10:30:00Z"
            }]
            """;

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        exception.Message.ShouldStartWith(
            "This resource is configured for 'EventGridEvent' schema and requires 'id' property to be set."
        );
    }

    [Fact]
    public void GivenEventJsonWithMissingSubject_WhenParsed_ThenExceptionThrown()
    {
        var context = TestHelpers.CreateHttpContext();
        const string requestBody = """
            [{
                "id": "test-id",
                "eventType": "Test.EventType",
                "eventTime": "2025-01-15T10:30:00Z"
            }]
            """;

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        exception.Message.ShouldStartWith(
            "This resource is configured for 'EventGridEvent' schema and requires 'subject' property to be set."
        );
    }

    // System.Text.Json lists every missing required property in declaration order (id, subject,
    // eventType, eventTime). The parser names only one of them, picked in the order Azure
    // validates: subject, id, eventType, eventTime. The single-field rows also pin the `required`
    // modifiers on EventGridEvent: without them the event would parse with the field left null.
    [Theory]
    [InlineData(new[] { "id" }, "id", false)]
    [InlineData(new[] { "subject" }, "subject", false)]
    [InlineData(new[] { "eventType" }, "eventType", false)]
    [InlineData(new[] { "eventTime" }, "eventTime", false)]
    [InlineData(new[] { "subject", "id" }, "subject", false)]
    [InlineData(new[] { "id", "eventType" }, "id", false)]
    [InlineData(new[] { "eventType", "eventTime" }, "eventType", false)]
    [InlineData(new[] { "id", "subject", "eventType", "eventTime" }, "subject", false)]
    [InlineData(new[] { "subject", "id" }, "subject", true)]
    [InlineData(new[] { "eventType", "eventTime" }, "eventType", true)]
    public void GivenEventMissingRequiredFields_WhenParsed_ThenMessageNamesTheFirstInAzureOrder(
        string[] missingFields,
        string expectedField,
        bool singleObject
    )
    {
        var context = TestHelpers.CreateHttpContext();
        var evt = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["id"] = "test-id",
            ["subject"] = "/test/subject",
            ["eventType"] = "Test.EventType",
            ["eventTime"] = "2025-01-15T10:30:00Z",
        };
        foreach (var field in missingFields)
        {
            evt.Remove(field);
        }

        var requestBody = singleObject
            ? JsonSerializer.Serialize(evt)
            : JsonSerializer.Serialize(new[] { evt });

        // STJ joins the missing names with the UI culture's list separator (';' in de-DE, for
        // example), and the parser splits on ','
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        InvalidOperationException exception;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

            exception = Should.Throw<InvalidOperationException>(() =>
                _parser.Parse(context, requestBody)
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        exception.Message.ShouldStartWith(
            $"This resource is configured for 'EventGridEvent' schema and requires '{expectedField}' property to be set."
        );
    }

    [Fact]
    public void GivenEventWithComplexData_WhenParsed_ThenDataPreserved()
    {
        var context = TestHelpers.CreateHttpContext();
        const string requestBody = """
            [{
                        "id": "test-id-123",
                        "subject": "/test/subject",
                        "eventType": "Test.EventType",
                        "eventTime": "2025-01-15T10:30:00Z",
                        "data": {
                            "nested": {
                                "value": 123,
                                "array": [1, 2, 3]
                            },
                            "string": "test"
                        }
                    }]
            """;

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        var data = events[0].EventGridEvent.ShouldNotBeNullAnd().Data.ShouldBeOfType<JsonElement>();
        data.GetProperty("nested").GetProperty("value").GetInt32().ShouldBe(123);
        data.GetProperty("nested").GetProperty("array")[2].GetInt32().ShouldBe(3);
        data.GetProperty("string").GetString().ShouldBe("test");
    }

    [Fact]
    public void Schema_ShouldReturnEventGridSchema()
    {
        _parser.Schema.ShouldBe(EventSchema.EventGridSchema);
    }
}
