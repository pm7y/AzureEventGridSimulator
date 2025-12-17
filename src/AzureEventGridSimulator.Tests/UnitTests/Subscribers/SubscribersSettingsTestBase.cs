using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers;

public abstract class SubscribersSettingsTestBase
{
    protected static HttpSubscriberSettings CreateValidHttpSubscriber(string name)
    {
        return new HttpSubscriberSettings { Name = name, Endpoint = "https://example.com/webhook" };
    }

    protected static ServiceBusSubscriberSettings CreateValidServiceBusSubscriber(string name)
    {
        return new ServiceBusSubscriberSettings
        {
            Name = name,
            ConnectionString =
                "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            Queue = "test-queue",
        };
    }

    protected static StorageQueueSubscriberSettings CreateValidStorageQueueSubscriber(string name)
    {
        return new StorageQueueSubscriberSettings
        {
            Name = name,
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "test-queue",
        };
    }
}
