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
        var context = CreateEventGridContext();
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
        var context = CreateEventGridContext();
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
        var context = CreateEventGridContext();
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
        events[0].EventGridEvent.ShouldNotBeNullAnd().Data.ShouldNotBeNull();
    }

    [Fact]
    public void GivenEventWithMinimalFields_WhenParsed_ThenRequiredFieldsPresent()
    {
        var context = CreateEventGridContext();
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

    [Fact]
    public void GivenEmptyBody_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateEventGridContext();
        const string requestBody = "";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        exception.Message.ShouldContain("Unexpected end when reading JSON");
    }

    [Fact]
    public void GivenWhitespaceBody_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateEventGridContext();
        const string requestBody = "   ";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        exception.Message.ShouldContain("Unexpected end when reading JSON");
    }

    [Fact]
    public void GivenNullBody_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateEventGridContext();

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, null!)
        );
        exception.Message.ShouldContain("Unexpected end when reading JSON");
    }

    [Fact]
    public void GivenMalformedJson_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateEventGridContext();
        const string requestBody = "[{ invalid json }]";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        // Azure returns the raw JSON parsing error
        exception.ShouldNotBeNull();
    }

    [Fact]
    public void GivenEmptyArray_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateEventGridContext();
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
        var context = CreateEventGridContext();
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
        // Azure returns the raw JSON parsing error
        exception.ShouldNotBeNull();
    }

    [Fact]
    public void GivenEventJsonWithMissingSubject_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateEventGridContext();
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
        // Azure returns the raw JSON parsing error
        exception.ShouldNotBeNull();
    }

    [Fact]
    public void GivenEventWithComplexData_WhenParsed_ThenDataPreserved()
    {
        var context = CreateEventGridContext();
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
        events[0].EventGridEvent.ShouldNotBeNullAnd().Data.ShouldNotBeNull();
    }

    [Fact]
    public void Schema_ShouldReturnEventGridSchema()
    {
        _parser.Schema.ShouldBe(EventSchema.EventGridSchema);
    }

    private static DefaultHttpContext CreateEventGridContext()
    {
        return new DefaultHttpContext { Request = { ContentType = "application/json" } };
    }
}
