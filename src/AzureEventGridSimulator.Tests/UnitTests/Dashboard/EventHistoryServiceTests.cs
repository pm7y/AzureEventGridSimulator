using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Dashboard;

[Trait("Category", "unit")]
public class EventHistoryServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 5, 14, 7, 9, TimeSpan.Zero);

    private readonly ILogger<EventHistoryService> _logger;
    private readonly EventHistoryService _service;
    private readonly SimulatorSettings _settings;
    private readonly EventHistoryStore _store;
    private readonly FakeTimeProvider _timeProvider = new(Now);

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
        _service = new EventHistoryService(_store, _settings, _timeProvider, _logger);
    }

    [Fact]
    public void GivenTimeProvider_WhenEventIsRecorded_ThenReceivedAtComesFromTimeProvider()
    {
        _service.RecordEventReceived(
            CreateTestEvent("event-1"),
            new TopicSettings
            {
                Name = "test-topic",
                Port = 60101,
                Key = "key",
            },
            EventSchema.EventGridSchema
        );
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        _store.Get("event-1").ShouldNotBeNullAnd().ReceivedAt.ShouldBe(Now);
    }

    [Fact]
    public void GivenRejection_WhenRecorded_ThenItIsReturnedWithTheTimeItWasCreatedWith()
    {
        var rejection = RejectedEventRecord.Create(
            "test-topic",
            60101,
            System.Net.HttpStatusCode.BadRequest,
            "Invalid JSON",
            Now,
            "[]",
            "application/json"
        );

        _service.RecordEventRejected(rejection);

        var recorded = _service.GetRecentRejections().ShouldHaveSingleItem();
        recorded.RejectedAt.ShouldBe(Now);
        recorded.TopicName.ShouldBe("test-topic");
        recorded.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
        recorded.RawBody.ShouldBe("[]");
        recorded.ContentType.ShouldBe("application/json");
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
        var attempt = new DeliveryAttempt(1, DeliveryOutcome.Success, DateTimeOffset.UtcNow, 200);
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
        var attempt = new DeliveryAttempt(1, DeliveryOutcome.Success, DateTimeOffset.UtcNow);

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

    [Fact]
    public void GivenQueuedDelivery_WhenAttemptsAreRecorded_ThenAttemptsAccumulateAndOtherFieldsAreKept()
    {
        RecordQueuedHttpDelivery("event-1", "http-subscriber");
        var firstAttemptAt = new DateTimeOffset(2025, 1, 5, 14, 0, 0, TimeSpan.Zero);
        var secondAttemptAt = firstAttemptAt.AddSeconds(10);

        _service.RecordDeliveryAttempt(
            "event-1",
            "http-subscriber",
            new DeliveryAttempt(1, DeliveryOutcome.HttpError, firstAttemptAt, 503)
        );
        _service.RecordDeliveryAttempt(
            "event-1",
            "http-subscriber",
            new DeliveryAttempt(2, DeliveryOutcome.Success, secondAttemptAt, 200)
        );

        var delivery = _store
            .Get("event-1")
            .ShouldNotBeNullAnd()
            .GetDeliveries()
            .ShouldHaveSingleItem();
        delivery.SubscriberName.ShouldBe("http-subscriber");
        delivery.SubscriberType.ShouldBe("http");
        delivery.Endpoint.ShouldBe("https://example.com/webhook");
        delivery.Status.ShouldBe(DeliveryStatus.Delivered);
        delivery.LastAttemptAt.ShouldBe(secondAttemptAt);
        delivery.CompletedAt.ShouldBeNull();
        delivery.Attempts.Select(a => a.AttemptNumber).ShouldBe([1, 2]);
        delivery.Attempts[0].Outcome.ShouldBe(DeliveryOutcome.HttpError);
        delivery.Attempts[0].HttpStatusCode.ShouldBe(503);
        delivery.Attempts[0].AttemptedAt.ShouldBe(firstAttemptAt);
    }

    [Fact]
    public void GivenFailedAttempt_WhenRecorded_ThenStatusIsRetrying()
    {
        RecordQueuedHttpDelivery("event-1", "http-subscriber");

        _service.RecordDeliveryAttempt(
            "event-1",
            "http-subscriber",
            new DeliveryAttempt(1, DeliveryOutcome.Timeout, DateTimeOffset.UtcNow)
        );

        _store
            .Get("event-1")
            .ShouldNotBeNullAnd()
            .GetDeliveries()
            .ShouldHaveSingleItem()
            .Status.ShouldBe(DeliveryStatus.Retrying);
    }

    [Fact]
    public void GivenPublishedDelivery_WhenAttemptIsRecorded_ThenThePublishedRecordIsNotChanged()
    {
        // Copy-on-write: dashboard readers may be enumerating the record they already hold
        RecordQueuedHttpDelivery("event-1", "http-subscriber");
        var published = _store.Get("event-1").ShouldNotBeNullAnd().GetDeliveries()[0];

        _service.RecordDeliveryAttempt(
            "event-1",
            "http-subscriber",
            new DeliveryAttempt(1, DeliveryOutcome.Success, DateTimeOffset.UtcNow, 200)
        );

        published.Status.ShouldBe(DeliveryStatus.Pending);
        published.Attempts.ShouldBeEmpty();
        published.LastAttemptAt.ShouldBeNull();
        _store.Get("event-1").ShouldNotBeNullAnd().GetDeliveries()[0].ShouldNotBeSameAs(published);
    }

    [Fact]
    public void GivenAttemptedDelivery_WhenCompleted_ThenAttemptsAndLastAttemptAreKept()
    {
        RecordQueuedHttpDelivery("event-1", "http-subscriber");
        var attemptAt = new DateTimeOffset(2025, 1, 5, 14, 0, 0, TimeSpan.Zero);
        var completedAt = attemptAt.AddMinutes(1);
        _service.RecordDeliveryAttempt(
            "event-1",
            "http-subscriber",
            new DeliveryAttempt(1, DeliveryOutcome.HttpError, attemptAt, 500)
        );

        _service.RecordDeliveryCompleted(
            "event-1",
            "http-subscriber",
            DeliveryStatus.DeadLettered,
            completedAt
        );

        var delivery = _store
            .Get("event-1")
            .ShouldNotBeNullAnd()
            .GetDeliveries()
            .ShouldHaveSingleItem();
        delivery.Status.ShouldBe(DeliveryStatus.DeadLettered);
        delivery.CompletedAt.ShouldBe(completedAt);
        delivery.LastAttemptAt.ShouldBe(attemptAt);
        delivery.Attempts.ShouldHaveSingleItem().HttpStatusCode.ShouldBe(500);
        delivery.SubscriberType.ShouldBe("http");
        delivery.Endpoint.ShouldBe("https://example.com/webhook");
    }

    [Fact]
    public void GivenCompletedDelivery_WhenLateAttemptIsRecorded_ThenCompletedAtIsKept()
    {
        RecordQueuedHttpDelivery("event-1", "http-subscriber");
        var completedAt = new DateTimeOffset(2025, 1, 5, 14, 0, 0, TimeSpan.Zero);
        _service.RecordDeliveryCompleted(
            "event-1",
            "http-subscriber",
            DeliveryStatus.Delivered,
            completedAt
        );

        _service.RecordDeliveryAttempt(
            "event-1",
            "http-subscriber",
            new DeliveryAttempt(1, DeliveryOutcome.Success, completedAt, 200)
        );

        _store
            .Get("event-1")
            .ShouldNotBeNullAnd()
            .GetDeliveries()
            .ShouldHaveSingleItem()
            .CompletedAt.ShouldBe(completedAt);
    }

    [Theory]
    [InlineData(
        "unknown-event",
        "http-subscriber",
        "not found in history for delivery attempt update"
    )]
    [InlineData(
        "event-1",
        "unknown-subscriber",
        "Delivery for subscriber 'unknown-subscriber' not found"
    )]
    public void GivenUnknownEventOrSubscriber_WhenAttemptIsRecorded_ThenNothingChangesAndItIsLogged(
        string eventId,
        string subscriberName,
        string expectedLogFragment
    )
    {
        RecordQueuedHttpDelivery("event-1", "http-subscriber");

        _service.RecordDeliveryAttempt(
            eventId,
            subscriberName,
            new DeliveryAttempt(1, DeliveryOutcome.Success, DateTimeOffset.UtcNow, 200)
        );

        _store
            .Get("event-1")
            .ShouldNotBeNullAnd()
            .GetDeliveries()
            .ShouldHaveSingleItem()
            .Status.ShouldBe(DeliveryStatus.Pending);
        ShouldHaveLoggedDebug(expectedLogFragment);
    }

    [Theory]
    [InlineData(
        "unknown-event",
        "http-subscriber",
        "not found in history for delivery completion update"
    )]
    [InlineData(
        "event-1",
        "unknown-subscriber",
        "Delivery for subscriber 'unknown-subscriber' not found"
    )]
    public void GivenUnknownEventOrSubscriber_WhenCompletionIsRecorded_ThenNothingChangesAndItIsLogged(
        string eventId,
        string subscriberName,
        string expectedLogFragment
    )
    {
        RecordQueuedHttpDelivery("event-1", "http-subscriber");

        _service.RecordDeliveryCompleted(
            eventId,
            subscriberName,
            DeliveryStatus.Delivered,
            DateTimeOffset.UtcNow
        );

        var delivery = _store
            .Get("event-1")
            .ShouldNotBeNullAnd()
            .GetDeliveries()
            .ShouldHaveSingleItem();
        delivery.Status.ShouldBe(DeliveryStatus.Pending);
        delivery.CompletedAt.ShouldBeNull();
        ShouldHaveLoggedDebug(expectedLogFragment);
    }

    private void RecordQueuedHttpDelivery(string eventId, string subscriberName)
    {
        _service.RecordEventReceived(
            CreateTestEvent(eventId),
            new TopicSettings
            {
                Name = "test-topic",
                Port = 60101,
                Key = "key",
            },
            EventSchema.EventGridSchema
        );
        _service.RecordDeliveryQueued(
            eventId,
            new HttpSubscriberSettings
            {
                Name = subscriberName,
                Endpoint = "https://example.com/webhook",
            }
        );
    }

    private void ShouldHaveLoggedDebug(string fragment)
    {
        _logger
            .Received(1)
            .Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o != null && string.Concat(o).Contains(fragment)),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
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
