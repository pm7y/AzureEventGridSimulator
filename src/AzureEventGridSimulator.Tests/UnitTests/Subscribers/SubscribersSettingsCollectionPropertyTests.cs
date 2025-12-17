using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers;

[Trait("Category", "unit")]
public class SubscribersSettingsCollectionPropertyTests : SubscribersSettingsTestBase
{
    [Fact]
    public void GivenMixedSubscribers_ThenAllReturnsAllTypes()
    {
        var settings = new SubscribersSettings
        {
            Http = new[] { CreateValidHttpSubscriber("Http1") },
            ServiceBus = new[] { CreateValidServiceBusSubscriber("ServiceBus1") },
            StorageQueue = new[] { CreateValidStorageQueueSubscriber("StorageQueue1") },
        };

        settings.All.ShouldContain(s => s.Name == "Http1");
        settings.All.ShouldContain(s => s.Name == "ServiceBus1");
        settings.All.ShouldContain(s => s.Name == "StorageQueue1");
        settings.Count.ShouldBe(3);
    }

    [Fact]
    public void GivenNullArrays_ThenAllReturnsEmpty()
    {
        var settings = new SubscribersSettings
        {
            Http = null,
            ServiceBus = null,
            StorageQueue = null,
        };

        settings.All.ShouldBeEmpty();
        settings.Any.ShouldBeFalse();
    }

    [Fact]
    public void GivenHttpSubscribers_ThenHttpSubscribersReturnsOnlyHttp()
    {
        var settings = new SubscribersSettings
        {
            Http = new[] { CreateValidHttpSubscriber("Http1") },
            ServiceBus = new[] { CreateValidServiceBusSubscriber("ServiceBus1") },
        };

        settings.HttpSubscribers.ShouldHaveSingleItem();
        settings.HttpSubscribers.ShouldContain(s => s.Name == "Http1");
    }

    [Fact]
    public void GivenServiceBusSubscribers_ThenServiceBusSubscribersReturnsOnlyServiceBus()
    {
        var settings = new SubscribersSettings
        {
            Http = new[] { CreateValidHttpSubscriber("Http1") },
            ServiceBus = new[] { CreateValidServiceBusSubscriber("ServiceBus1") },
        };

        settings.ServiceBusSubscribers.ShouldHaveSingleItem();
        settings.ServiceBusSubscribers.ShouldContain(s => s.Name == "ServiceBus1");
    }

    [Fact]
    public void GivenStorageQueueSubscribers_ThenStorageQueueSubscribersReturnsOnlyStorageQueue()
    {
        var settings = new SubscribersSettings
        {
            Http = new[] { CreateValidHttpSubscriber("Http1") },
            StorageQueue = new[] { CreateValidStorageQueueSubscriber("StorageQueue1") },
        };

        settings.StorageQueueSubscribers.ShouldHaveSingleItem();
        settings.StorageQueueSubscribers.ShouldContain(s => s.Name == "StorageQueue1");
    }

    [Fact]
    public void GivenNullHttpArray_ThenHttpSubscribersReturnsEmpty()
    {
        var settings = new SubscribersSettings { Http = null };

        settings.HttpSubscribers.ShouldBeEmpty();
    }

    [Fact]
    public void GivenNullServiceBusArray_ThenServiceBusSubscribersReturnsEmpty()
    {
        var settings = new SubscribersSettings { ServiceBus = null };

        settings.ServiceBusSubscribers.ShouldBeEmpty();
    }

    [Fact]
    public void GivenNullStorageQueueArray_ThenStorageQueueSubscribersReturnsEmpty()
    {
        var settings = new SubscribersSettings { StorageQueue = null };

        settings.StorageQueueSubscribers.ShouldBeEmpty();
    }
}
