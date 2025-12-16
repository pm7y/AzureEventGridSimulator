using System;
using AzureEventGridSimulator.Domain.Entities;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.CloudEvents;

[Trait("Category", "unit")]
public class SimulatorEventTests
{
    [Fact]
    public void GivenEventGridEvent_WhenWrappedInSimulatorEvent_ThenPropertiesMappedCorrectly()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "event-123",
            Subject = "/test/subject",
            EventType = "Test.Event.Type",
            EventTime = "2025-01-15T10:30:00Z",
            Data = new { Property = "Value" },
            DataVersion = "1.0",
            Topic = "/test/topic"
        };

        var simulatorEvent = SimulatorEvent.FromEventGridEvent(eventGridEvent);

        simulatorEvent.Schema.ShouldBe(EventSchema.EventGridSchema);
        simulatorEvent.Id.ShouldBe("event-123");
        simulatorEvent.Subject.ShouldBe("/test/subject");
        simulatorEvent.EventType.ShouldBe("Test.Event.Type");
        simulatorEvent.EventTime.ShouldBe("2025-01-15T10:30:00Z");
        simulatorEvent.Data.ShouldNotBeNull();
        simulatorEvent.DataVersion.ShouldBe("1.0");
        simulatorEvent.Source.ShouldBe("/test/topic");
    }

    [Fact]
    public void GivenCloudEvent_WhenWrappedInSimulatorEvent_ThenPropertiesMappedCorrectly()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "event-456",
            Subject = "/test/subject",
            Type = "com.example.test",
            Time = "2025-01-15T10:30:00Z",
            Data = new { Property = "Value" },
            DataSchema = "https://example.com/schema",
            Source = "/test/source",
            SpecVersion = "1.0"
        };

        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        simulatorEvent.Schema.ShouldBe(EventSchema.CloudEventV1_0);
        simulatorEvent.Id.ShouldBe("event-456");
        simulatorEvent.Subject.ShouldBe("/test/subject");
        simulatorEvent.EventType.ShouldBe("com.example.test");
        simulatorEvent.EventTime.ShouldBe("2025-01-15T10:30:00Z");
        simulatorEvent.Data.ShouldNotBeNull();
        simulatorEvent.DataVersion.ShouldBe("https://example.com/schema");
        simulatorEvent.Source.ShouldBe("/test/source");
    }

    [Fact]
    public void GivenCloudEventWithoutSubject_WhenWrappedInSimulatorEvent_ThenSubjectFallsBackToSource()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "event-789",
            Type = "com.example.test",
            Source = "/test/source",
            SpecVersion = "1.0"
        };

        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        simulatorEvent.Subject.ShouldBe("/test/source");
    }

    [Fact]
    public void GivenSimulatorEventWithEventGridEvent_WhenValidateCalled_ThenEventGridEventValidated()
    {
        var eventGridEvent = new EventGridEvent
        {
            Id = "event-123",
            Subject = "/test/subject",
            EventType = "Test.Event.Type",
            EventTime = "2025-01-15T10:30:00Z"
        };

        var simulatorEvent = SimulatorEvent.FromEventGridEvent(eventGridEvent);

        Should.NotThrow(() => simulatorEvent.Validate());
    }

    [Fact]
    public void GivenSimulatorEventWithCloudEvent_WhenValidateCalled_ThenCloudEventValidated()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "event-456",
            Type = "com.example.test",
            Source = "/test/source",
            SpecVersion = "1.0"
        };

        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        Should.NotThrow(() => simulatorEvent.Validate());
    }

    [Fact]
    public void GivenSimulatorEventWithInvalidCloudEvent_WhenValidateCalled_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            Id = "event-456",
            Type = "com.example.test",
            // Missing Source
            SpecVersion = "1.0"
        };

        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        Should.Throw<InvalidOperationException>(() => simulatorEvent.Validate());
    }
}
