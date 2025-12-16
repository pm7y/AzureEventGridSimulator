using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.CloudEvents;

[Trait("Category", "unit")]
public class EventGridSchemaFormatterTests
{
    private readonly EventGridSchemaFormatter _formatter = new();

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
            Topic = "/test/topic",
            MetadataVersion = "1",
        };

        var simulatorEvent = SimulatorEvent.FromEventGridEvent(eventGridEvent);
        var json = _formatter.Serialize(simulatorEvent);

        // Event Grid events are serialized as an array
        var parsed = JArray.Parse(json);
        parsed.Count.ShouldBe(1);

        var evt = parsed[0];
        evt["id"]?.ToString().ShouldBe("event-123");
        evt["subject"]?.ToString().ShouldBe("/test/subject");
        evt["eventType"]?.ToString().ShouldBe("Test.Event.Type");
        evt["eventTime"].ShouldNotBeNull(); // Time format may vary by locale
        evt["dataVersion"]?.ToString().ShouldBe("1.0");
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

        var parsed = JArray.Parse(json);
        parsed.Count.ShouldBe(1);

        var evt = parsed[0];
        evt["id"]?.ToString().ShouldBe("test-id-123");
        evt["eventType"]?.ToString().ShouldBe("com.example.test");
        evt["eventTime"].ShouldNotBeNull(); // Time format may vary by locale
        evt["subject"]?.ToString().ShouldBe("/test/subject");
        evt["topic"]?.ToString().ShouldBe("/test/source");
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

        var parsed = JArray.Parse(json);
        var evt = parsed[0];
        evt["subject"]?.ToString().ShouldBe("/test/source");
    }

    [Fact]
    public void GivenFormatter_WhenContentTypeRequested_ThenReturnsApplicationJson()
    {
        _formatter.ContentType.ShouldBe("application/json");
    }
}
