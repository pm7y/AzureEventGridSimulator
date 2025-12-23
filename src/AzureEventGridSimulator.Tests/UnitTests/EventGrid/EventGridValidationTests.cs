using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.EventGrid;

[Trait("Category", "unit")]
public class EventGridValidationTests
{
    [Fact]
    public void GivenValidEventGridEvent_WhenValidated_ThenNoExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
        };

        Should.NotThrow(() => eventGridEvent.Validate());
    }

    [Fact]
    public void GivenValidEventWithOptionalFields_WhenValidated_ThenNoExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
            DataVersion = "1.0",
            MetadataVersion = "1",
            Data = new { Property = "Value" },
        };

        Should.NotThrow(() => eventGridEvent.Validate());
    }

    [Fact]
    public void GivenEventWithMissingId_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("Id");
    }

    [Fact]
    public void GivenEventWithEmptyId_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("Id");
    }

    [Fact]
    public void GivenEventWithWhitespaceId_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "   ",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("Id");
    }

    [Fact]
    public void GivenEventWithMissingSubject_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("Subject");
    }

    [Fact]
    public void GivenEventWithEmptySubject_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("Subject");
    }

    [Fact]
    public void GivenEventWithMissingEventType_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventTime = "2025-01-15T10:30:00Z",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("EventType");
    }

    [Fact]
    public void GivenEventWithEmptyEventType_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "",
            EventTime = "2025-01-15T10:30:00Z",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("EventType");
    }

    [Fact]
    public void GivenEventWithMissingEventTime_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("EventTime");
    }

    [Fact]
    public void GivenEventWithEmptyEventTime_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("EventTime");
    }

    [Fact]
    public void GivenEventWithInvalidEventTime_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "not-a-valid-datetime",
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("EventTime");
    }

    [Fact]
    public void GivenEventWithUnspecifiedDateTimeKind_WhenValidated_ThenExceptionThrown()
    {
        // A date without timezone info will be parsed as Unspecified kind
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15 10:30:00", // No timezone specified
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("EventTime");
    }

    [Theory]
    [InlineData("2025-01-15T10:30:00Z")] // UTC
    [InlineData("2025-01-15T10:30:00+00:00")] // UTC offset
    [InlineData("2025-01-15T10:30:00-05:00")] // EST offset
    public void GivenEventWithValidTimezoneEventTime_WhenValidated_ThenNoExceptionThrown(
        string eventTime
    )
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = eventTime,
        };

        Should.NotThrow(() => eventGridEvent.Validate());
    }

    [Fact]
    public void GivenEventWithInvalidMetadataVersion_WhenValidated_ThenExceptionThrown()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
            MetadataVersion = "2", // Only "1" or null is valid
        };

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent.Validate());
        exception.Message.ShouldContain("MetadataVersion");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public void GivenEventWithValidMetadataVersion_WhenValidated_ThenNoExceptionThrown(
        string metadataVersion
    )
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
            MetadataVersion = metadataVersion,
        };

        Should.NotThrow(() => eventGridEvent.Validate());
    }

    [Fact]
    public void GivenEventWithNonNullTopic_WhenValidated_ThenExceptionThrown()
    {
        // Topic should be null/empty when publishing - it's set by the service
        // Use JSON deserialization to simulate a publisher sending a Topic value
        var json = """
            {
                "id": "test-id-123",
                "subject": "/test/subject",
                "eventType": "Test.EventType",
                "eventTime": "2025-01-15T10:30:00Z",
                "topic": "/subscriptions/some/topic"
            }
            """;
        var eventGridEvent = JsonSerializer.Deserialize<EventGridEvent>(json);

        var exception = Should.Throw<InvalidOperationException>(() => eventGridEvent!.Validate());
        exception.Message.ShouldContain("Topic");
    }

    [Fact]
    public void GivenEventWithNullTopic_WhenValidated_ThenNoExceptionThrown()
    {
        // Topic defaults to null when not set
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
        };

        Should.NotThrow(() => eventGridEvent.Validate());
    }

    [Fact]
    public void GivenEventWithEmptyTopic_WhenValidated_ThenNoExceptionThrown()
    {
        // Use JSON deserialization to set empty Topic
        var json = """
            {
                "id": "test-id-123",
                "subject": "/test/subject",
                "eventType": "Test.EventType",
                "eventTime": "2025-01-15T10:30:00Z",
                "topic": ""
            }
            """;
        var eventGridEvent = JsonSerializer.Deserialize<EventGridEvent>(json);

        Should.NotThrow(() => eventGridEvent!.Validate());
    }

    [Fact]
    public void GivenEventWithNullData_WhenValidated_ThenNoExceptionThrown()
    {
        // Data is optional
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
            Data = null,
        };

        Should.NotThrow(() => eventGridEvent.Validate());
    }

    [Fact]
    public void GivenEventWithNullDataVersion_WhenValidated_ThenNoExceptionThrown()
    {
        // DataVersion is optional
        var eventGridEvent = new EventGridEvent
        {
            Id = "test-id-123",
            Subject = "/test/subject",
            EventType = "Test.EventType",
            EventTime = "2025-01-15T10:30:00Z",
            DataVersion = null,
        };

        Should.NotThrow(() => eventGridEvent.Validate());
    }
}
