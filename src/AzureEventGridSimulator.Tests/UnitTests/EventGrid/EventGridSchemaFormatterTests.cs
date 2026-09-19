using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.EventGrid;

[Trait("Category", "unit")]
public class EventGridSchemaFormatterTests
{
    // Deliberately not the events' own time, so a pass-through can't be mistaken for the fallback
    private static readonly DateTimeOffset FixedTime = new(2025, 6, 1, 8, 15, 30, TimeSpan.Zero);

    private readonly EventGridSchemaFormatter _formatter = new(new FakeTimeProvider(FixedTime));

    [Fact]
    public void GivenEventGridEvent_WhenSerialized_ThenJsonContainsAllFields()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "event-123",
            Subject = "/test/subject",
            EventType = "Test.Event.Type",
            EventTime = "2025-01-15T10:30:00Z",
            Data = new { Property = "Value" },
            DataVersion = "1.0",
            MetadataVersion = "1",
        };
        eventGridEvent.SetTopic("/test/topic");

        var simulatorEvent = SimulatorEvent.FromEventGridEvent(eventGridEvent);
        var json = _formatter.Serialize(simulatorEvent);

        // Event Grid events are serialized as an array
        using var doc = JsonDocument.Parse(json);
        var parsed = doc.RootElement;
        parsed.GetArrayLength().ShouldBe(1);

        var evt = parsed[0];
        evt.GetProperty("id").GetString().ShouldBe("event-123");
        evt.GetProperty("subject").GetString().ShouldBe("/test/subject");
        evt.GetProperty("eventType").GetString().ShouldBe("Test.Event.Type");
        evt.GetProperty("eventTime").GetString().ShouldBe("2025-01-15T10:30:00Z");
        evt.GetProperty("data").GetProperty("Property").GetString().ShouldBe("Value");
        evt.GetProperty("dataVersion").GetString().ShouldBe("1.0");
    }

    [Fact]
    public void GivenCloudEvent_WhenSerializedAsEventGrid_ThenConvertedCorrectly()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
            Subject = "/test/subject",
            Time = "2025-01-15T10:30:00Z",
            Data = new { Property = "Value" },
        };

        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);
        var json = _formatter.Serialize(simulatorEvent);

        using var doc = JsonDocument.Parse(json);
        var parsed = doc.RootElement;
        parsed.GetArrayLength().ShouldBe(1);

        var evt = parsed[0];
        evt.GetProperty("id").GetString().ShouldBe("test-id-123");
        evt.GetProperty("eventType").GetString().ShouldBe("com.example.test");
        evt.GetProperty("eventTime").GetString().ShouldBe("2025-01-15T10:30:00Z");
        evt.GetProperty("subject").GetString().ShouldBe("/test/subject");
        evt.GetProperty("topic").GetString().ShouldBe("/test/source");
        evt.GetProperty("data").GetProperty("Property").GetString().ShouldBe("Value");
    }

    [Fact]
    public void GivenCloudEventWithoutSubject_WhenSerializedAsEventGrid_ThenSubjectFallsBackToSource()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
        };

        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);
        var json = _formatter.Serialize(simulatorEvent);

        using var doc = JsonDocument.Parse(json);
        var parsed = doc.RootElement;
        var evt = parsed[0];
        evt.GetProperty("subject").GetString().ShouldBe("/test/source");
    }

    [Fact]
    public void GivenFormatter_WhenContentTypeRequested_ThenReturnsApplicationJson()
    {
        _formatter.ContentType.ShouldBe("application/json");
    }
}
