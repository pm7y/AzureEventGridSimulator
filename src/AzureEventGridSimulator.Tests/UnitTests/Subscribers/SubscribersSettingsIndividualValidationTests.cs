using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers;

[Trait("Category", "unit")]
public class SubscribersSettingsIndividualValidationTests
{
    [Fact]
    public void GivenInvalidHttpSubscriber_WhenValidated_ThenThrowsException()
    {
        var subscriber = new HttpSubscriberSettings { Name = "", Endpoint = "https://example.com" };

        Should.Throw<ArgumentException>(() => subscriber.Validate());
    }

    [Fact]
    public void GivenInvalidServiceBusSubscriber_WhenValidated_ThenThrowsException()
    {
        var subscriber = new ServiceBusSubscriberSettings { Name = "Test" };

        Should.Throw<ArgumentException>(() => subscriber.Validate());
    }

    [Fact]
    public void GivenInvalidStorageQueueSubscriber_WhenValidated_ThenThrowsException()
    {
        // Missing ConnectionString - should fail validation
        var subscriber = new StorageQueueSubscriberSettings
        {
            Name = "Test",
            QueueName = "test-queue",
        };

        Should.Throw<ArgumentException>(() => subscriber.Validate());
    }
}
