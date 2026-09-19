using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers;

[Trait("Category", "unit")]
public class SubscribersSettingsDefaultTests : SubscribersSettingsTestBase
{
    [Fact]
    public void GivenDefaultSettings_ThenAllArraysAreNull()
    {
        var settings = new SubscribersSettings();

        settings.Http.ShouldBeNull();
        settings.ServiceBus.ShouldBeNull();
        settings.StorageQueue.ShouldBeNull();
        settings.EventHub.ShouldBeNull();

        // Computed properties should still work correctly with null arrays
        settings.All.ShouldBeEmpty();
        settings.HttpSubscribers.ShouldBeEmpty();
        settings.ServiceBusSubscribers.ShouldBeEmpty();
        settings.StorageQueueSubscribers.ShouldBeEmpty();
        settings.EventHubSubscribers.ShouldBeEmpty();
    }

    [Fact]
    public void GivenTwoSubscribersOfEachType_ThenAllReturnsEveryOneInTypeOrder()
    {
        var settings = new SubscribersSettings
        {
            Http = [CreateValidHttpSubscriber("Http1"), CreateValidHttpSubscriber("Http2")],
            ServiceBus =
            [
                CreateValidServiceBusSubscriber("ServiceBus1"),
                CreateValidServiceBusSubscriber("ServiceBus2"),
            ],
            StorageQueue =
            [
                CreateValidStorageQueueSubscriber("StorageQueue1"),
                CreateValidStorageQueueSubscriber("StorageQueue2"),
            ],
            EventHub =
            [
                CreateValidEventHubSubscriber("EventHub1"),
                CreateValidEventHubSubscriber("EventHub2"),
            ],
        };

        settings
            .All.Select(s => s.Name)
            .ShouldBe([
                "Http1",
                "Http2",
                "ServiceBus1",
                "ServiceBus2",
                "StorageQueue1",
                "StorageQueue2",
                "EventHub1",
                "EventHub2",
            ]);
    }
}
