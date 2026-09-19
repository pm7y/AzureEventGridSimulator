using AzureEventGridSimulator.Domain.Services.Retry;
using AzureEventGridSimulator.Tests.UnitTests.Common;
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

    [Fact]
    public void GivenEmptyQueue_WhenEnqueuing_ThenCountIsOne()
    {
        var delivery = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime);

        _queue.Enqueue(delivery);

        _queue.Count.ShouldBe(1);
    }

    [Fact]
    public void GivenMultipleDeliveries_WhenEnqueuing_ThenCountIsCorrect()
    {
        var delivery1 = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime);
        var delivery2 = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime);
        var delivery3 = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime);

        _queue.Enqueue(delivery1);
        _queue.Enqueue(delivery2);
        _queue.Enqueue(delivery3);

        _queue.Count.ShouldBe(3);
    }

    [Fact]
    public void GivenDuplicateDeliveryId_WhenEnqueuing_ThenSecondIsIgnored()
    {
        var delivery = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime);

        _queue.Enqueue(delivery);
        _queue.Enqueue(delivery); // Same delivery again

        _queue.Count.ShouldBe(1);
    }

    [Fact]
    public void GivenExistingDelivery_WhenRemoving_ThenReturnsTrue()
    {
        var delivery = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime);
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
        var delivery = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime);
        _queue.Enqueue(delivery);
        _queue.Remove(delivery.Id);

        var result = _queue.Remove(delivery.Id);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GivenExistingDelivery_WhenRequeuing_ThenUpdatesDelivery()
    {
        var delivery = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime);
        _queue.Enqueue(delivery);

        var newNextAttemptTime = FixedTime.AddMinutes(5);
        delivery.NextAttemptTime = newNextAttemptTime;
        delivery.AttemptCount = 2;

        _queue.RequeueForRetry(delivery);

        _queue.Count.ShouldBe(1); // Still only 1 item
        var dueDeliveries = await _queue.GetDueDeliveriesAsync().ToListAsync();
        dueDeliveries.ShouldBeEmpty(); // Not due yet (5 minutes in future)
    }

    [Fact]
    public void GivenNewDelivery_WhenRequeuing_ThenAddsToQueue()
    {
        var delivery = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime);

        _queue.RequeueForRetry(delivery);

        _queue.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GivenNoDeliveries_WhenGettingDueDeliveries_ThenReturnsEmpty()
    {
        var dueDeliveries = await _queue.GetDueDeliveriesAsync().ToListAsync();

        dueDeliveries.ShouldBeEmpty();
    }

    [Fact]
    public async Task GivenDeliveryDueNow_WhenGettingDueDeliveries_ThenReturnsDelivery()
    {
        var delivery = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime.AddSeconds(-1));
        _queue.Enqueue(delivery);

        var dueDeliveries = await _queue.GetDueDeliveriesAsync().ToListAsync();

        dueDeliveries.Count.ShouldBe(1);
        dueDeliveries[0].Id.ShouldBe(delivery.Id);
    }

    [Fact]
    public async Task GivenDeliveryNotYetDue_WhenGettingDueDeliveries_ThenReturnsEmpty()
    {
        var delivery = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime.AddMinutes(5));
        _queue.Enqueue(delivery);

        var dueDeliveries = await _queue.GetDueDeliveriesAsync().ToListAsync();

        dueDeliveries.ShouldBeEmpty();
    }

    [Fact]
    public async Task GivenMixedDueAndNotDue_WhenGettingDueDeliveries_ThenReturnsOnlyDue()
    {
        var dueDelivery1 = TestHelpers.CreatePendingDelivery(
            nextAttemptTime: FixedTime.AddSeconds(-10)
        );
        var dueDelivery2 = TestHelpers.CreatePendingDelivery(
            nextAttemptTime: FixedTime.AddSeconds(-5)
        );
        var notDueDelivery = TestHelpers.CreatePendingDelivery(
            nextAttemptTime: FixedTime.AddMinutes(5)
        );

        _queue.Enqueue(dueDelivery1);
        _queue.Enqueue(dueDelivery2);
        _queue.Enqueue(notDueDelivery);

        var dueDeliveries = await _queue.GetDueDeliveriesAsync().ToListAsync();

        dueDeliveries.Count.ShouldBe(2);
        dueDeliveries.ShouldContain(d => d.Id == dueDelivery1.Id);
        dueDeliveries.ShouldContain(d => d.Id == dueDelivery2.Id);
        dueDeliveries.ShouldNotContain(d => d.Id == notDueDelivery.Id);
    }

    [Fact]
    public async Task GivenMultipleDueDeliveries_WhenGettingDueDeliveries_ThenOrderedByNextAttemptTime()
    {
        var delivery1 = TestHelpers.CreatePendingDelivery(
            nextAttemptTime: FixedTime.AddSeconds(-5)
        );
        var delivery2 = TestHelpers.CreatePendingDelivery(
            nextAttemptTime: FixedTime.AddSeconds(-10)
        ); // Earliest
        var delivery3 = TestHelpers.CreatePendingDelivery(
            nextAttemptTime: FixedTime.AddSeconds(-1)
        ); // Latest

        _queue.Enqueue(delivery1);
        _queue.Enqueue(delivery2);
        _queue.Enqueue(delivery3);

        var dueDeliveries = await _queue.GetDueDeliveriesAsync().ToListAsync();

        dueDeliveries.Count.ShouldBe(3);
        dueDeliveries[0].Id.ShouldBe(delivery2.Id); // Earliest first
        dueDeliveries[1].Id.ShouldBe(delivery1.Id);
        dueDeliveries[2].Id.ShouldBe(delivery3.Id); // Latest last
    }

    [Fact]
    public async Task GivenDeliveryInFuture_WhenTimeAdvances_ThenBecomesDue()
    {
        var delivery = TestHelpers.CreatePendingDelivery(nextAttemptTime: FixedTime.AddMinutes(5));
        _queue.Enqueue(delivery);

        // Initially not due
        (await _queue.GetDueDeliveriesAsync().ToListAsync()).ShouldBeEmpty();

        // Advance time by 6 minutes
        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        // Now should be due
        var dueDeliveries = await _queue.GetDueDeliveriesAsync().ToListAsync();
        dueDeliveries.Count.ShouldBe(1);
        dueDeliveries[0].Id.ShouldBe(delivery.Id);
    }
}
