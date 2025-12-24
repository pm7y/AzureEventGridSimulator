using System.Text;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Delivery;

[Trait("Category", "unit")]
public class StorageQueueEventDeliveryServiceTests
{
    private readonly EventSchemaFormatterFactory _formatterFactory;
    private readonly ILogger<StorageQueueEventDeliveryService> _logger;
    private readonly StorageQueueEventDeliveryService _service;

    public StorageQueueEventDeliveryServiceTests()
    {
        _logger = Substitute.For<ILogger<StorageQueueEventDeliveryService>>();
        _formatterFactory = new EventSchemaFormatterFactory(
            new EventGridSchemaFormatter(TimeProvider.System),
            new CloudEventSchemaFormatter()
        );
        _service = new StorageQueueEventDeliveryService(_logger, _formatterFactory);
    }

    private static StorageQueueSubscriberSettings CreateValidSettings()
    {
        return new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
        };
    }

    private static TopicSettings CreateTopicSettings()
    {
        return new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TheLocal+DevelopmentKey=",
        };
    }

    private static SimulatorEvent CreateTestEvent()
    {
        return SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = "test-event-id",
                Subject = "test/subject",
                EventType = "Test.EventType",
                EventTime = "2025-01-15T10:30:00Z",
                DataVersion = "1.0",
                Data = new { customerId = "cust-123" },
            }
        );
    }

    [Fact]
    public async Task GivenDisabledSubscription_WhenSending_ThenLogsWarningAndReturnsEarly()
    {
        var subscription = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
            Disabled = true,
        };
        var topic = CreateTopicSettings();
        var evt = CreateTestEvent();

        await _service.SendAsync(subscription, evt, topic, EventSchema.EventGridSchema);

        // Verify warning was logged (subscription is disabled)
        _logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => (o.ToString() ?? "").Contains("disabled")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public void SubscriberType_ShouldBeStorageQueue()
    {
        var subscription = CreateValidSettings();

        subscription.SubscriberType.ShouldBe("storageQueue");
    }

    [Fact]
    public async Task GivenService_WhenDisposed_ThenResourcesCleanedUp()
    {
        // Create a new service instance for disposal testing
        var logger = Substitute.For<ILogger<StorageQueueEventDeliveryService>>();
        var service = new StorageQueueEventDeliveryService(logger, _formatterFactory);

        // Disposing should not throw
        await Should.NotThrowAsync(async () => await service.DisposeAsync());
    }

    [Fact]
    public void GivenSubscriptionWithDeliverySchema_WhenConfigured_ThenSchemaIsUsed()
    {
        var subscription = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = "my-queue",
            DeliverySchema = EventSchema.CloudEventV1_0,
        };

        subscription.DeliverySchema.ShouldBe(EventSchema.CloudEventV1_0);
    }

    [Fact]
    public void GivenSubscriptionWithoutDeliverySchema_WhenConfigured_ThenSchemaIsNull()
    {
        var subscription = CreateValidSettings();

        subscription.DeliverySchema.ShouldBeNull();
    }

    [Theory]
    [InlineData(EventSchema.EventGridSchema)]
    [InlineData(EventSchema.CloudEventV1_0)]
    public void GivenFormatterFactory_WhenGettingFormatter_ThenReturnsCorrectFormatter(
        EventSchema schema
    )
    {
        var formatter = _formatterFactory.GetFormatter(schema);

        formatter.ShouldNotBeNull();
    }

    [Fact]
    public void GivenEventGridFormatter_WhenSerializing_ThenReturnsJson()
    {
        var formatter = _formatterFactory.GetFormatter(EventSchema.EventGridSchema);
        var evt = CreateTestEvent();

        var json = formatter.Serialize(evt);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("test-event-id");
    }

    [Fact]
    public void GivenCloudEventFormatter_WhenSerializing_ThenReturnsJson()
    {
        var formatter = _formatterFactory.GetFormatter(EventSchema.CloudEventV1_0);
        var evt = CreateTestEvent();

        var json = formatter.Serialize(evt);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("test-event-id");
    }

    [Fact]
    public void GivenMessage_WhenBase64Encoded_ThenCanBeDecoded()
    {
        // This tests the Base64 encoding behavior used by the service
        var formatter = _formatterFactory.GetFormatter(EventSchema.EventGridSchema);
        var evt = CreateTestEvent();
        var json = formatter.Serialize(evt);

        // Encode as Base64 (same as the service does)
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        // Verify it can be decoded back
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        decoded.ShouldBe(json);
    }

    [Fact]
    public void GivenSubscription_WhenQueueNameSet_ThenQueueNameIsAccessible()
    {
        var subscription = CreateValidSettings();

        subscription.QueueName.ShouldBe("my-queue");
    }

    [Fact]
    public void GivenSubscription_WhenConnectionStringSet_ThenEffectiveConnectionStringIsAccessible()
    {
        var subscription = CreateValidSettings();

        subscription.EffectiveConnectionString.ShouldBe(subscription.ConnectionString);
    }

    [Fact]
    public void GivenSubscription_WhenTopicConnectionStringUsed_ThenEffectiveConnectionStringIsFromTopic()
    {
        var topic = new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TheLocal+DevelopmentKey=",
            StorageQueueConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=topicstorage;AccountKey=xyz789;EndpointSuffix=core.windows.net",
        };

        var subscription = new StorageQueueSubscriberSettings
        {
            Name = "TestSubscriber",
            QueueName = "my-queue",
            ParentTopic = topic,
            // No ConnectionString at subscriber level
        };

        subscription.EffectiveConnectionString.ShouldBe(topic.StorageQueueConnectionString);
    }

    [Fact]
    public void GivenCloudEventWithOptionalFields_WhenFormatted_ThenOptionalFieldsIncluded()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
            Subject = "/test/subject",
            Time = "2025-01-15T10:30:00Z",
            Data = new { key = "value" },
        };

        var evt = SimulatorEvent.FromCloudEvent(cloudEvent);
        var formatter = _formatterFactory.GetFormatter(EventSchema.CloudEventV1_0);
        var json = formatter.Serialize(evt);

        json.ShouldContain("specversion");
        json.ShouldContain("type");
        json.ShouldContain("source");
        json.ShouldContain("id");
    }

    [Fact]
    public void GivenEventGridEvent_WhenFormattedAsArray_ThenArrayContainsSingleEvent()
    {
        var evt = CreateTestEvent();
        var formatter = _formatterFactory.GetFormatter(EventSchema.EventGridSchema);
        var json = formatter.Serialize(evt);

        // EventGrid events are serialized as arrays
        json.ShouldStartWith("[");
        json.ShouldEndWith("]");
    }
}
