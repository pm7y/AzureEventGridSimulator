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
public class EventHubEventDeliveryServiceTests
{
    private readonly EventSchemaFormatterFactory _formatterFactory;
    private readonly ILogger<EventHubEventDeliveryService> _logger;
    private readonly DeliveryPropertyResolver _propertyResolver;
    private readonly EventHubEventDeliveryService _service;

    public EventHubEventDeliveryServiceTests()
    {
        _logger = Substitute.For<ILogger<EventHubEventDeliveryService>>();
        _formatterFactory = new EventSchemaFormatterFactory(
            new EventGridSchemaFormatter(TimeProvider.System),
            new CloudEventSchemaFormatter()
        );
        _propertyResolver = new DeliveryPropertyResolver();
        _service = new EventHubEventDeliveryService(_logger, _formatterFactory, _propertyResolver);
    }

    private static EventHubSubscriberSettings CreateValidSettings()
    {
        return new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            EventHubName = "my-event-hub",
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

    private static PendingDelivery CreatePendingDelivery(
        EventHubSubscriberSettings subscriber = null,
        SimulatorEvent evt = null,
        TopicSettings topic = null
    )
    {
        return new PendingDelivery
        {
            Event = evt ?? CreateTestEvent(),
            Subscriber = subscriber ?? CreateValidSettings(),
            Topic = topic ?? CreateTopicSettings(),
            InputSchema = EventSchema.EventGridSchema,
        };
    }

    [Fact]
    public async Task GivenDisabledSubscription_WhenDelivering_ThenReturnsEventHubError()
    {
        var subscription = CreateValidSettings();
        subscription.Disabled = true;
        var delivery = CreatePendingDelivery(subscription);

        var result = await _service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.EventHubError);
        result.ErrorMessage.ShouldContain("disabled");
    }

    [Fact]
    public async Task GivenInvalidSubscriberType_WhenDelivering_ThenReturnsEventHubError()
    {
        // Create a delivery with wrong subscriber type
        var httpSubscriber = new HttpSubscriberSettings
        {
            Name = "WrongType",
            Endpoint = "https://example.com",
        };
        var evt = CreateTestEvent();
        var topic = CreateTopicSettings();
        var delivery = new PendingDelivery
        {
            Event = evt,
            Subscriber = httpSubscriber,
            Topic = topic,
            InputSchema = EventSchema.EventGridSchema,
        };

        var result = await _service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.EventHubError);
        result.ErrorMessage.ShouldContain("Invalid subscriber type");
    }

    [Fact]
    public void SubscriberType_ShouldBeEventHub()
    {
        var subscription = CreateValidSettings();

        subscription.SubscriberType.ShouldBe("eventHub");
    }

    [Fact]
    public async Task GivenService_WhenDisposed_ThenResourcesCleanedUp()
    {
        var logger = Substitute.For<ILogger<EventHubEventDeliveryService>>();
        var service = new EventHubEventDeliveryService(
            logger,
            _formatterFactory,
            _propertyResolver
        );

        await Should.NotThrowAsync(async () => await service.DisposeAsync());
    }

    [Fact]
    public void GivenSubscriptionWithDeliverySchema_WhenConfigured_ThenSchemaIsUsed()
    {
        var subscription = CreateValidSettings();
        subscription.DeliverySchema = EventSchema.CloudEventV1_0;

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
    public void GivenSubscriber_WhenGettingEffectiveConnectionString_ThenReturnsConnectionString()
    {
        var subscription = CreateValidSettings();

        subscription.EffectiveConnectionString.ShouldBe(subscription.ConnectionString);
    }

    [Fact]
    public void GivenSubscriber_WhenGettingEventHubName_ThenReturnsEventHubName()
    {
        var subscription = CreateValidSettings();

        subscription.EventHubName.ShouldBe("my-event-hub");
    }
}
