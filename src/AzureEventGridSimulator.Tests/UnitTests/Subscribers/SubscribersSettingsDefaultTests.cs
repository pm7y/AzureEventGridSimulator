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
    public void GivenDefaultSettings_ThenAllArraysAreEmpty()
    {
        var settings = new SubscribersSettings();

        settings.Http.ShouldBeEmpty();
        settings.ServiceBus.ShouldBeEmpty();
        settings.StorageQueue.ShouldBeEmpty();
        settings.All.ShouldBeEmpty();
        settings.Any.ShouldBeFalse();
        settings.Count.ShouldBe(0);
    }

    [Fact]
    public void GivenUniqueNames_WhenValidated_ThenNoException()
    {
        var settings = new SubscribersSettings
        {
            Http = new[] { CreateValidHttpSubscriber("Http1"), CreateValidHttpSubscriber("Http2") },
            ServiceBus = new[]
            {
                CreateValidServiceBusSubscriber("ServiceBus1"),
                CreateValidServiceBusSubscriber("ServiceBus2"),
            },
            StorageQueue = new[]
            {
                CreateValidStorageQueueSubscriber("StorageQueue1"),
                CreateValidStorageQueueSubscriber("StorageQueue2"),
            },
        };

        Should.NotThrow(() => settings.Validate());
        settings.Count.ShouldBe(6);
        settings.Any.ShouldBeTrue();
    }
}
