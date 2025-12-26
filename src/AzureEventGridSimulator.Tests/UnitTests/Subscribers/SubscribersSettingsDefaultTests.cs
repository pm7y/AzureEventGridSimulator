using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers;

[Trait("Category", "unit")]
public class SubscribersSettingsDefaultTests : SubscribersSettingsTestBase
{
    [Fact]
    public void GivenEmptySettings_WhenValidated_ThenNoException()
    {
        var settings = new SubscribersSettings();

        Should.NotThrow(() => settings.Validate());
    }

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
        settings.Any.ShouldBeFalse();
        settings.Count.ShouldBe(0);
    }

    [Fact]
    public void GivenUniqueNames_WhenValidated_ThenNoException()
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
        };

        Should.NotThrow(() => settings.Validate());
        settings.Count.ShouldBe(6);
        settings.Any.ShouldBeTrue();
    }
}
