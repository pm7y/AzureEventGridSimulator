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
        var topic = TestHelpers.CreateValidTopicSettings();
        var evt = TestHelpers.CreateSimulatorEventFromEventGrid();

        // SendAsync swallows delivery errors (logs them), so both calls complete
        await service.SendAsync(subscription, evt, topic, EventSchema.EventGridSchema);
        await service.SendAsync(subscription, evt, topic, EventSchema.EventGridSchema);

        // The client factory must run once per attempt; a cached failed creation
        // would log "Creating Storage Queue client" only once.
        logger
            .Received(2)
            .Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => string.Concat(o).Contains("Creating Storage Queue client")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );

        // Both attempts surface the failure in the error log rather than silently dropping
        logger
            .Received(2)
            .Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => string.Concat(o).Contains("Failed to send event")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }
}
