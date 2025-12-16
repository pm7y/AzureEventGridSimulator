using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.CloudEvents;

[Trait("Category", "unit")]
public class CloudEventSchemaFormatterTests
{
    private readonly CloudEventSchemaFormatter _formatter = new();

    [Fact]
    public void GivenCloudEvent_WhenSerialized_ThenJsonContainsAllRequiredFields()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123"
        };

        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);
        var json = _formatter.Serialize(simulatorEvent);

        // Azure Event Grid sends events "in an array that has a single event"
        var array = JArray.Parse(json);
        array.Count.ShouldBe(1);
        var parsed = array[0];
        parsed["specversion"]?.ToString().ShouldBe("1.0");
        parsed["type"]?.ToString().ShouldBe("com.example.test");
        parsed["source"]?.ToString().ShouldBe("/test/source");
        parsed["id"]?.ToString().ShouldBe("test-id-123");
    }

    [Fact]
    public void GivenCloudEventWithOptionalFields_WhenSerialized_ThenJsonContainsOptionalFields()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
            Subject = "/test/subject",
            Time = "2025-01-15T10:30:00Z",
            Data = new { Property = "Value" }
        };

        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);
        var json = _formatter.Serialize(simulatorEvent);

        var array = JArray.Parse(json);
        array.Count.ShouldBe(1);
        var parsed = array[0];
        parsed["subject"]?.ToString().ShouldBe("/test/subject");
        parsed["time"].ShouldNotBeNull(); // Time format may vary by locale
        parsed["data"].ShouldNotBeNull();
    }

    [Fact]
    public void GivenEventGridEvent_WhenSerializedAsCloudEvent_ThenConvertedCorrectly()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "event-123",
            Subject = "/test/subject",
            EventType = "Test.Event.Type",
            EventTime = "2025-01-15T10:30:00Z",
            Data = new { Property = "Value" },
            Topic = "/test/topic"
        };

        var simulatorEvent = SimulatorEvent.FromEventGridEvent(eventGridEvent);
        var json = _formatter.Serialize(simulatorEvent);

        var array = JArray.Parse(json);
        array.Count.ShouldBe(1);
        var parsed = array[0];
        parsed["specversion"]?.ToString().ShouldBe("1.0");
        parsed["type"]?.ToString().ShouldBe("Test.Event.Type");
        parsed["source"]?.ToString().ShouldBe("/test/topic");
        parsed["id"]?.ToString().ShouldBe("event-123");
        parsed["subject"]?.ToString().ShouldBe("/test/subject");
        parsed["time"].ShouldNotBeNull(); // Time format may vary by locale
    }

    [Fact]
    public void GivenFormatter_WhenContentTypeRequested_ThenReturnsCloudEventsBatchContentType()
    {
        // Azure Event Grid sends events "in an array that has a single event",
        // so we use the batch content type
        _formatter.ContentType.ShouldBe("application/cloudevents-batch+json; charset=utf-8");
    }
}
