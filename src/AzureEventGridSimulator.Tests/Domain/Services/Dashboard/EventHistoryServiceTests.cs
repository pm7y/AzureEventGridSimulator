using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.Domain.Services.Dashboard;

[Trait("Category", "unit")]
public class EventHistoryServiceTests
{
    private readonly ILogger<EventHistoryService> _logger;
    private readonly EventHistoryService _service;
    private readonly SimulatorSettings _settings;
    private readonly EventHistoryStore _store;

    public EventHistoryServiceTests()
    {
        _store = new EventHistoryStore();
        _settings = new SimulatorSettings
        {
            Topics =
            [
                new TopicSettings
                {
                    Name = "topic-1",
                    Port = 60101,
                    Key = "key1",
                },
                new TopicSettings
                {
                    Name = "topic-2",
                    Port = 60102,
                    Key = "key2",
                    Disabled = true,
                },
            ],
        };
        _logger = Substitute.For<ILogger<EventHistoryService>>();
        _service = new EventHistoryService(_store, _settings, _logger);
    }

    [Fact]
    public void RecordEventReceived_AddsEventToStore()
    {
        var evt = CreateTestEvent("event-1");
        var topic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "key",
        };

        _service.RecordEventReceived(evt, topic, EventSchema.EventGridSchema);

        _store.Count.ShouldBe(1);
        var recorded = _store.Get("event-1");
        recorded.ShouldNotBeNull();
        recorded.TopicName.ShouldBe("test-topic");
        recorded.TopicPort.ShouldBe(60101);
    }

    [Fact]
    public void RecordDeliveryQueued_AddsDeliveryToEvent()
    {
        var evt = CreateTestEvent("event-1");
        var topic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "key",
        };
        _service.RecordEventReceived(evt, topic, EventSchema.EventGridSchema);

        var subscriber = new HttpSubscriberSettings
        {
            Name = "http-subscriber",
            Endpoint = "https://example.com/webhook",
        };

        _service.RecordDeliveryQueued("event-1", subscriber);

        var recorded = _store.Get("event-1");
        recorded.ShouldNotBeNull();
        var deliveries = recorded.GetDeliveries();
        deliveries.Count.ShouldBe(1);
        deliveries[0].SubscriberName.ShouldBe("http-subscriber");
        deliveries[0].Status.ShouldBe(DeliveryStatus.Pending);
    }

    [Fact]
    public void RecordDeliveryAttempt_UpdatesDeliveryStatus()
    {
        // Setup
        var evt = CreateTestEvent("event-1");
        var topic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "key",
        };
        _service.RecordEventReceived(evt, topic, EventSchema.EventGridSchema);

        var subscriber = new HttpSubscriberSettings
        {
            Name = "http-subscriber",
            Endpoint = "https://example.com/webhook",
        };
        _service.RecordDeliveryQueued("event-1", subscriber);

        // Act
        var attempt = new DeliveryAttempt
        {
            AttemptNumber = 1,
            AttemptTime = DateTimeOffset.UtcNow,
            Outcome = DeliveryOutcome.Success,
            HttpStatusCode = 200,
        };
        _service.RecordDeliveryAttempt("event-1", "http-subscriber", attempt);

        // Assert
        var recorded = _store.Get("event-1");
        recorded.ShouldNotBeNull();
        var deliveries = recorded.GetDeliveries();
        deliveries[0].Status.ShouldBe(DeliveryStatus.Delivered);
        deliveries[0].Attempts.Count.ShouldBe(1);
        deliveries[0].Attempts[0].HttpStatusCode.ShouldBe(200);
    }

    [Fact]
    public void RecordDeliveryAttempt_NonExistingEvent_DoesNotThrow()
    {
        var attempt = new DeliveryAttempt
        {
            AttemptNumber = 1,
            AttemptTime = DateTimeOffset.UtcNow,
            Outcome = DeliveryOutcome.Success,
        };

        Should.NotThrow(() =>
            _service.RecordDeliveryAttempt("non-existing", "subscriber", attempt)
        );
    }

    [Fact]
    public void RecordDeliveryCompleted_UpdatesStatusAndCompletedTime()
    {
        // Setup
        var evt = CreateTestEvent("event-1");
        var topic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "key",
        };
        _service.RecordEventReceived(evt, topic, EventSchema.EventGridSchema);

        var subscriber = new HttpSubscriberSettings
        {
            Name = "http-subscriber",
            Endpoint = "https://example.com/webhook",
        };
        _service.RecordDeliveryQueued("event-1", subscriber);

        // Act
        var completedAt = DateTimeOffset.UtcNow;
        _service.RecordDeliveryCompleted(
            "event-1",
            "http-subscriber",
            DeliveryStatus.DeadLettered,
            completedAt
        );

        // Assert
        var recorded = _store.Get("event-1");
        recorded.ShouldNotBeNull();
        var deliveries = recorded.GetDeliveries();
        deliveries[0].Status.ShouldBe(DeliveryStatus.DeadLettered);
        deliveries[0].CompletedAt.ShouldBe(completedAt);
    }

    [Fact]
    public void GetRecentEvents_ReturnsAllEvents()
    {
        var evt1 = CreateTestEvent("event-1");
        var evt2 = CreateTestEvent("event-2");
        var topic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "key",
        };

        _service.RecordEventReceived(evt1, topic, EventSchema.EventGridSchema);
        _service.RecordEventReceived(evt2, topic, EventSchema.EventGridSchema);

        var recent = _service.GetRecentEvents();

        recent.Count.ShouldBe(2);
    }

    [Fact]
    public void GetRecentEvents_WithTopicFilter_ReturnsFilteredEvents()
    {
        var evt1 = CreateTestEvent("event-1");
        var evt2 = CreateTestEvent("event-2");
        var topic1 = new TopicSettings
        {
            Name = "topic-a",
            Port = 60101,
            Key = "key",
        };
        var topic2 = new TopicSettings
        {
            Name = "topic-b",
            Port = 60102,
            Key = "key",
        };

        _service.RecordEventReceived(evt1, topic1, EventSchema.EventGridSchema);
        _service.RecordEventReceived(evt2, topic2, EventSchema.EventGridSchema);

        var filtered = _service.GetRecentEvents("topic-a");

        filtered.Count.ShouldBe(1);
        filtered[0].TopicName.ShouldBe("topic-a");
    }

    [Fact]
    public void GetEvent_ExistingEvent_ReturnsEvent()
    {
        var evt = CreateTestEvent("event-1");
        var topic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "key",
        };
        _service.RecordEventReceived(evt, topic, EventSchema.EventGridSchema);

        var result = _service.GetEvent("event-1");

        result.ShouldNotBeNull();
        result.Id.ShouldBe("event-1");
    }

    [Fact]
    public void GetEvent_NonExistingEvent_ReturnsNull()
    {
        var result = _service.GetEvent("non-existing");

        result.ShouldBeNull();
    }

    [Fact]
    public void GetStats_ReturnsStatsWithActiveTopicsCount()
    {
        var evt = CreateTestEvent("event-1");
        var topic = new TopicSettings
        {
            Name = "test-topic",
            Port = 60101,
            Key = "key",
        };
        _service.RecordEventReceived(evt, topic, EventSchema.EventGridSchema);

        var stats = _service.GetStats();

        stats.TotalEventsReceived.ShouldBe(1);
        stats.EventsInHistory.ShouldBe(1);
        // One topic is enabled, one is disabled
        stats.TopicsActive.ShouldBe(1);
    }

    private static SimulatorEvent CreateTestEvent(string id)
    {
        return SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = id,
                Subject = "/test/subject",
                EventType = "Test.EventType",
                EventTime = DateTimeOffset.UtcNow.ToString("o"),
                DataVersion = "1.0",
                Data = new { Property = "Value" },
            }
        );
    }
}
