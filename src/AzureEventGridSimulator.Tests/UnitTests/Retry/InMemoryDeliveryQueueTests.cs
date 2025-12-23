using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services.Retry;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.Helpers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Retry;

[Trait("Category", "unit")]
public class InMemoryDeliveryQueueTests
{
    private static readonly DateTimeOffset FixedTime = new(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ILogger<InMemoryDeliveryQueue> _logger;
    private readonly InMemoryDeliveryQueue _queue;
    private readonly FakeTimeProvider _timeProvider = new(FixedTime);

    public InMemoryDeliveryQueueTests()
    {
        _logger = Substitute.For<ILogger<InMemoryDeliveryQueue>>();
        _queue = new InMemoryDeliveryQueue(_timeProvider, _logger);
    }

    private static PendingDelivery CreatePendingDelivery(DateTimeOffset? nextAttemptTime = null)
    {
        var subscriber = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com/webhook",
            DisableValidation = true,
            ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful,
        };

        var topic = new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
        };

        var evt = SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = Guid.NewGuid().ToString(),
                Subject = "test/subject",
                EventType = "Test.EventType",
                EventTime = FixedTime.ToString("o"),
                DataVersion = "1.0",
                Data = new { test = "data" },
            }
        );

        return new PendingDelivery
        {
            Event = evt,
            Subscriber = subscriber,
            Topic = topic,
            InputSchema = EventSchema.EventGridSchema,
            EnqueuedTime = FixedTime,
            NextAttemptTime = nextAttemptTime ?? FixedTime,
        };
    }

    [Fact]
    public void GivenEmptyQueue_WhenEnqueuing_ThenCountIsOne()
    {
        var delivery = CreatePendingDelivery();

        _queue.Enqueue(delivery);

        _queue.Count.ShouldBe(1);
    }

    [Fact]
    public void GivenMultipleDeliveries_WhenEnqueuing_ThenCountIsCorrect()
    {
        var delivery1 = CreatePendingDelivery();
        var delivery2 = CreatePendingDelivery();
        var delivery3 = CreatePendingDelivery();

        _queue.Enqueue(delivery1);
        _queue.Enqueue(delivery2);
        _queue.Enqueue(delivery3);

        _queue.Count.ShouldBe(3);
    }

    [Fact]
    public void GivenDuplicateDeliveryId_WhenEnqueuing_ThenSecondIsIgnored()
    {
        var delivery = CreatePendingDelivery();

        _queue.Enqueue(delivery);
        _queue.Enqueue(delivery); // Same delivery again

        _queue.Count.ShouldBe(1);
    }

    [Fact]
    public void GivenExistingDelivery_WhenRemoving_ThenReturnsTrue()
    {
        var delivery = CreatePendingDelivery();
        _queue.Enqueue(delivery);

        var result = _queue.Remove(delivery.Id);

        result.ShouldBeTrue();
        _queue.Count.ShouldBe(0);
    }

    [Fact]
    public void GivenNonExistingDelivery_WhenRemoving_ThenReturnsFalse()
    {
        var result = _queue.Remove("non-existing-id");

        result.ShouldBeFalse();
    }

    [Fact]
    public void GivenRemovedDelivery_WhenRemovingAgain_ThenReturnsFalse()
    {
        var delivery = CreatePendingDelivery();
        _queue.Enqueue(delivery);
        _queue.Remove(delivery.Id);

        var result = _queue.Remove(delivery.Id);

        result.ShouldBeFalse();
    }

    [Fact]
    public void GivenExistingDelivery_WhenRequeuing_ThenUpdatesDelivery()
    {
        var delivery = CreatePendingDelivery();
        _queue.Enqueue(delivery);

        var newNextAttemptTime = FixedTime.AddMinutes(5);
        delivery.NextAttemptTime = newNextAttemptTime;
        delivery.AttemptCount = 2;

        _queue.RequeueForRetry(delivery);

        _queue.Count.ShouldBe(1); // Still only 1 item
        var dueDeliveries = _queue.GetDueDeliveries().ToList();
        dueDeliveries.ShouldBeEmpty(); // Not due yet (5 minutes in future)
    }

    [Fact]
    public void GivenNewDelivery_WhenRequeuing_ThenAddsToQueue()
    {
        var delivery = CreatePendingDelivery();

        _queue.RequeueForRetry(delivery);

        _queue.Count.ShouldBe(1);
    }

    [Fact]
    public void GivenNoDeliveries_WhenGettingDueDeliveries_ThenReturnsEmpty()
    {
        var dueDeliveries = _queue.GetDueDeliveries();

        dueDeliveries.ShouldBeEmpty();
    }

    [Fact]
    public void GivenDeliveryDueNow_WhenGettingDueDeliveries_ThenReturnsDelivery()
    {
        var delivery = CreatePendingDelivery(FixedTime.AddSeconds(-1));
        _queue.Enqueue(delivery);

        var dueDeliveries = _queue.GetDueDeliveries().ToList();

        dueDeliveries.Count.ShouldBe(1);
        dueDeliveries[0].Id.ShouldBe(delivery.Id);
    }

    [Fact]
    public void GivenDeliveryNotYetDue_WhenGettingDueDeliveries_ThenReturnsEmpty()
    {
        var delivery = CreatePendingDelivery(FixedTime.AddMinutes(5));
        _queue.Enqueue(delivery);

        var dueDeliveries = _queue.GetDueDeliveries().ToList();

        dueDeliveries.ShouldBeEmpty();
    }

    [Fact]
    public void GivenMixedDueAndNotDue_WhenGettingDueDeliveries_ThenReturnsOnlyDue()
    {
        var dueDelivery1 = CreatePendingDelivery(FixedTime.AddSeconds(-10));
        var dueDelivery2 = CreatePendingDelivery(FixedTime.AddSeconds(-5));
        var notDueDelivery = CreatePendingDelivery(FixedTime.AddMinutes(5));

        _queue.Enqueue(dueDelivery1);
        _queue.Enqueue(dueDelivery2);
        _queue.Enqueue(notDueDelivery);

        var dueDeliveries = _queue.GetDueDeliveries().ToList();

        dueDeliveries.Count.ShouldBe(2);
        dueDeliveries.ShouldContain(d => d.Id == dueDelivery1.Id);
        dueDeliveries.ShouldContain(d => d.Id == dueDelivery2.Id);
        dueDeliveries.ShouldNotContain(d => d.Id == notDueDelivery.Id);
    }

    [Fact]
    public void GivenMultipleDueDeliveries_WhenGettingDueDeliveries_ThenOrderedByNextAttemptTime()
    {
        var delivery1 = CreatePendingDelivery(FixedTime.AddSeconds(-5));
        var delivery2 = CreatePendingDelivery(FixedTime.AddSeconds(-10)); // Earliest
        var delivery3 = CreatePendingDelivery(FixedTime.AddSeconds(-1)); // Latest

        _queue.Enqueue(delivery1);
        _queue.Enqueue(delivery2);
        _queue.Enqueue(delivery3);

        var dueDeliveries = _queue.GetDueDeliveries().ToList();

        dueDeliveries.Count.ShouldBe(3);
        dueDeliveries[0].Id.ShouldBe(delivery2.Id); // Earliest first
        dueDeliveries[1].Id.ShouldBe(delivery1.Id);
        dueDeliveries[2].Id.ShouldBe(delivery3.Id); // Latest last
    }

    [Fact]
    public void GivenDeliveryInFuture_WhenTimeAdvances_ThenBecomesDue()
    {
        var delivery = CreatePendingDelivery(FixedTime.AddMinutes(5));
        _queue.Enqueue(delivery);

        // Initially not due
        _queue.GetDueDeliveries().ShouldBeEmpty();

        // Advance time by 6 minutes
        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        // Now should be due
        var dueDeliveries = _queue.GetDueDeliveries().ToList();
        dueDeliveries.Count.ShouldBe(1);
        dueDeliveries[0].Id.ShouldBe(delivery.Id);
    }
}
