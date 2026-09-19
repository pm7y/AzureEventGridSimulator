using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Delivery;

/// <summary>
///     A failed client creation (e.g. Azurite not yet running) must be evicted
///     from the cache so the next delivery attempt retries instead of replaying
///     the cached failure forever.
/// </summary>
[Trait("Category", "unit")]
public class StorageQueueClientCacheTests
{
    [Fact]
    public async Task GivenClientCreationFails_WhenDeliveryIsRetried_ThenFailedCreationIsEvictedAndRetried()
    {
        var logger = Substitute.For<ILogger<StorageQueueEventDeliveryService>>();
        var formatterFactory = new EventSchemaFormatterFactory(
            new EventGridSchemaFormatter(TimeProvider.System),
            new CloudEventSchemaFormatter()
        );
        await using var service = new StorageQueueEventDeliveryService(logger, formatterFactory);

        var subscription = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = "this-is-not-a-valid-connection-string",
            QueueName = "my-queue",
        };
        var delivery = TestHelpers.CreatePendingDelivery(subscription);

        // DeliverAsync maps the failure to a result, so both attempts complete
        var first = await service.DeliverAsync(delivery, CancellationToken.None);
        var second = await service.DeliverAsync(delivery, CancellationToken.None);

        // The client factory must run once per attempt; a cached failed creation
        // would log "Creating Storage Queue client" only once.
        logger
            .Received(2)
            .Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null && string.Concat(o).Contains("Creating Storage Queue client")
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );

        // Both attempts surface the failure rather than silently dropping it
        first.Success.ShouldBeFalse();
        first.Outcome.ShouldBe(DeliveryOutcome.StorageQueueError);
        second.Success.ShouldBeFalse();
        second.Outcome.ShouldBe(DeliveryOutcome.StorageQueueError);
    }
}
