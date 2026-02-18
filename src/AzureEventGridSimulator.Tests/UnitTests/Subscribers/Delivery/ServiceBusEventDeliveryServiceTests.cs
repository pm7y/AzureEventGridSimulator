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
public class ServiceBusEventDeliveryServiceTests
{
    private readonly EventSchemaFormatterFactory _formatterFactory;
    private readonly ILogger<ServiceBusEventDeliveryService> _logger;
    private readonly DeliveryPropertyResolver _propertyResolver;
    private readonly ServiceBusEventDeliveryService _service;

    public ServiceBusEventDeliveryServiceTests()
    {
        _logger = Substitute.For<ILogger<ServiceBusEventDeliveryService>>();
        _formatterFactory = new EventSchemaFormatterFactory(
            new EventGridSchemaFormatter(TimeProvider.System),
            new CloudEventSchemaFormatter()
        );
        _propertyResolver = new DeliveryPropertyResolver();
        _service = new ServiceBusEventDeliveryService(
            _logger,
            _formatterFactory,
            _propertyResolver
        );
    }

    private static ServiceBusSubscriberSettings CreateValidQueueSettings()
    {
        return new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            Queue = "my-queue",
        };
    }

    private static ServiceBusSubscriberSettings CreateValidTopicSettings()
    {
        return new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            Topic = "my-topic",
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
        var subscription = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            Queue = "my-queue",
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
    public void GivenQueueSubscription_WhenChecked_ThenIsTopicIsFalse()
    {
        var subscription = CreateValidQueueSettings();

        subscription.IsTopic.ShouldBeFalse();
        subscription.DestinationName.ShouldBe("my-queue");
    }

    [Fact]
    public void GivenTopicSubscription_WhenChecked_ThenIsTopicIsTrue()
    {
        var subscription = CreateValidTopicSettings();

        subscription.IsTopic.ShouldBeTrue();
        subscription.DestinationName.ShouldBe("my-topic");
    }

    [Fact]
    public void SubscriberType_ShouldBeServiceBus()
    {
        var subscription = CreateValidQueueSettings();

        subscription.SubscriberType.ShouldBe("serviceBus");
    }

    [Fact]
    public async Task GivenService_WhenDisposed_ThenResourcesCleanedUp()
    {
        // Create a new service instance for disposal testing
        var logger = Substitute.For<ILogger<ServiceBusEventDeliveryService>>();
        var service = new ServiceBusEventDeliveryService(
            logger,
            _formatterFactory,
            _propertyResolver
        );

        // Disposing should not throw
        await Should.NotThrowAsync(async () => await service.DisposeAsync());
    }

    [Fact]
    public void GivenSubscriptionWithDeliverySchema_WhenConfigured_ThenSchemaIsUsed()
    {
        var subscription = new ServiceBusSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            Queue = "my-queue",
            DeliverySchema = EventSchema.CloudEventV1_0,
        };

        subscription.DeliverySchema.ShouldBe(EventSchema.CloudEventV1_0);
    }

    [Fact]
    public void GivenSubscriptionWithoutDeliverySchema_WhenConfigured_ThenSchemaIsNull()
    {
        var subscription = CreateValidQueueSettings();

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
    public void GivenEventGridFormatter_WhenSerializing_ThenReturnsJsonArray()
    {
        var formatter = _formatterFactory.GetFormatter(EventSchema.EventGridSchema);
        var evt = CreateTestEvent();

        var json = formatter.Serialize(evt);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("test-event-id");
        json.TrimStart().ShouldStartWith("[");
        json.TrimEnd().ShouldEndWith("]");
    }

    [Fact]
    public void GivenCloudEventFormatter_WhenSerializing_ThenReturnsJsonArray()
    {
        var formatter = _formatterFactory.GetFormatter(EventSchema.CloudEventV1_0);
        var evt = CreateTestEvent();

        var json = formatter.Serialize(evt);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("test-event-id");
        json.TrimStart().ShouldStartWith("[");
        json.TrimEnd().ShouldEndWith("]");
    }

    [Fact]
    public void GivenEventGridFormatter_WhenGettingContentType_ThenReturnsApplicationJson()
    {
        var formatter = _formatterFactory.GetFormatter(EventSchema.EventGridSchema);

        formatter.ContentType.ShouldBe("application/json");
    }

    [Fact]
    public void GivenCloudEventFormatter_WhenGettingContentType_ThenReturnsCloudEventsContentType()
    {
        var formatter = _formatterFactory.GetFormatter(EventSchema.CloudEventV1_0);

        formatter.ContentType.ShouldContain("cloudevents");
    }

    [Fact]
    public void GivenPropertyResolver_WhenResolvingStaticProperty_ThenReturnsValue()
    {
        var properties = new Dictionary<string, DeliveryPropertySettings>
        {
            ["Label"] = new() { Type = "static", Value = "test-label" },
        };

        var resolved = _propertyResolver.ResolveProperties(properties, CreateTestEvent());

        resolved["Label"].ShouldBe("test-label");
    }

    [Fact]
    public void GivenPropertyResolver_WhenResolvingDynamicProperty_ThenReturnsEventValue()
    {
        var properties = new Dictionary<string, DeliveryPropertySettings>
        {
            ["Subject"] = new() { Type = "dynamic", Value = "Subject" },
        };

        var resolved = _propertyResolver.ResolveProperties(properties, CreateTestEvent());

        resolved["Subject"].ShouldBe("test/subject");
    }

    [Fact]
    public void GivenCloudEventFormatter_WhenSerializingSingle_ThenReturnsJsonWithoutArray()
    {
        var formatter = _formatterFactory.GetFormatter(EventSchema.CloudEventV1_0);
        var evt = CreateTestEvent();

        var json = formatter.SerializeSingle(evt);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("test-event-id");
        // Verify it's NOT an array (doesn't start with '[')
        json.TrimStart().ShouldStartWith("{");
        json.TrimEnd().ShouldEndWith("}");
    }

    [Fact]
    public void GivenEventGridFormatter_WhenSerializingSingle_ThenReturnsJsonWithoutArray()
    {
        var formatter = _formatterFactory.GetFormatter(EventSchema.EventGridSchema);
        var evt = CreateTestEvent();

        var json = formatter.SerializeSingle(evt);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("test-event-id");
        // Verify it's NOT an array (doesn't start with '[')
        json.TrimStart().ShouldStartWith("{");
        json.TrimEnd().ShouldEndWith("}");
    }
}
