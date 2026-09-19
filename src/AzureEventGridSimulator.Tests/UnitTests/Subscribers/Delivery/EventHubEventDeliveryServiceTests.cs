using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
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

    [Fact]
    public async Task GivenDisabledSubscription_WhenDelivering_ThenReturnsEventHubError()
    {
        var subscription = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            EventHubName = "my-event-hub",
            Disabled = true,
        };
        var delivery = TestHelpers.CreatePendingDelivery(subscription);

        var result = await _service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.EventHubError);
        result.ErrorMessage.ShouldNotBeNullAnd().ShouldContain("disabled");
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
        var delivery = TestHelpers.CreatePendingDelivery(httpSubscriber);

        var result = await _service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.EventHubError);
        result.ErrorMessage.ShouldNotBeNullAnd().ShouldContain("Invalid subscriber type");
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
        var subscription = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            EventHubName = "my-event-hub",
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
        var evt = TestHelpers.CreateSimulatorEventFromEventGrid(
            id: "test-event-id",
            subject: "test/subject",
            data: new { customerId = "cust-123" }
        );

        var json = formatter.Serialize(evt);

        json.ShouldNotBeNullOrEmpty();
        json.ShouldContain("test-event-id");
    }

    [Fact]
    public void GivenCloudEventFormatter_WhenSerializing_ThenReturnsJson()
    {
        var formatter = _formatterFactory.GetFormatter(EventSchema.CloudEventV1_0);
        var evt = TestHelpers.CreateSimulatorEventFromEventGrid(
            id: "test-event-id",
            subject: "test/subject",
            data: new { customerId = "cust-123" }
        );

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

        var resolved = _propertyResolver.ResolveProperties(
            properties,
            TestHelpers.CreateSimulatorEventFromEventGrid(
                id: "test-event-id",
                subject: "test/subject",
                data: new { customerId = "cust-123" }
            )
        );

        resolved["Label"].ShouldBe("test-label");
    }

    [Fact]
    public void GivenPropertyResolver_WhenResolvingDynamicProperty_ThenReturnsEventValue()
    {
        var properties = new Dictionary<string, DeliveryPropertySettings>
        {
            ["Subject"] = new() { Type = "dynamic", Value = "Subject" },
        };

        var resolved = _propertyResolver.ResolveProperties(
            properties,
            TestHelpers.CreateSimulatorEventFromEventGrid(
                id: "test-event-id",
                subject: "test/subject",
                data: new { customerId = "cust-123" }
            )
        );

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

    [Theory]
    [InlineData(
        "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=KeyLastSecret=",
        "KeyLastSecret"
    )]
    [InlineData(
        "SharedAccessKey=KeyFirstSecret=;Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=Root",
        "KeyFirstSecret"
    )]
    [InlineData(
        "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessSignature=SharedAccessSignature sr=sb%3a%2f%2fmy-namespace.servicebus.windows.net%2fmy-event-hub&sig=SasSecret%3d&se=4102444800&skn=Root",
        "SasSecret"
    )]
    public async Task GivenConnectionStringWithASecret_WhenProducerIsCreated_ThenTheSecretIsNotLogged(
        string connectionString,
        string secret
    )
    {
        var logger = Substitute.For<ILogger<EventHubEventDeliveryService>>();
        await using var service = new EventHubEventDeliveryService(
            logger,
            _formatterFactory,
            _propertyResolver
        );
        var subscription = new EventHubSubscriberSettings
        {
            Name = "TestSubscriber",
            ConnectionString = connectionString,
            EventHubName = "my-event-hub",
        };

        // The producer is created (and logged) before the send, which the cancelled token
        // then stops before it reaches the network
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await service.DeliverAsync(
            TestHelpers.CreatePendingDelivery(subscription),
            cancelled.Token
        );

        logger
            .Received(1)
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o =>
                    o != null
                    && string.Concat(o).Contains("Creating Event Hub producer client")
                    && string.Concat(o).Contains("my-namespace.servicebus.windows.net")
                    && string.Concat(o).Contains("***REDACTED***")
                ),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
        logger
            .DidNotReceive()
            .Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o != null && string.Concat(o).Contains(secret)),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }
}
