using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Domain.Services.Retry;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Commands;

[Trait("Category", "unit")]
public class SendNotificationEventsToSubscriberCommandHandlerTests
{
    // In the past, so it can't be mistaken for PendingDelivery's DateTimeOffset.UtcNow defaults
    private static readonly DateTimeOffset FixedTime = new(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly IDeliveryQueue _deliveryQueue;
    private readonly IEventHistoryService _eventHistoryService;
    private readonly SendNotificationEventsToSubscriberCommandHandler _handler;
    private readonly ILogger<SendNotificationEventsToSubscriberCommandHandler> _logger;

    public SendNotificationEventsToSubscriberCommandHandlerTests()
    {
        _logger = Substitute.For<ILogger<SendNotificationEventsToSubscriberCommandHandler>>();
        _deliveryQueue = Substitute.For<IDeliveryQueue>();
        _eventHistoryService = Substitute.For<IEventHistoryService>();

        _handler = new SendNotificationEventsToSubscriberCommandHandler(
            _deliveryQueue,
            _eventHistoryService,
            new FakeTimeProvider(FixedTime),
            _logger
        );
    }

    [Fact]
    public async Task GivenNoSubscribers_WhenHandled_ThenLogsWarningAndEnqueuesNothing()
    {
        var topic = CreateTopic();
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger.ShouldHaveLogged(LogLevel.Warning, "has no subscribers");
        AssertNothingEnqueued();
    }

    [Fact]
    public async Task GivenAllSubscribersDisabled_WhenHandled_ThenLogsWarningAndEnqueuesNothing()
    {
        var subscriber = CreateHttpSubscriber(disabled: true);
        var topic = CreateTopic(http: [subscriber]);
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger.ShouldHaveLogged(LogLevel.Warning, "has no enabled subscribers");
        AssertNothingEnqueued();
    }

    [Fact]
    public async Task GivenEventFilteredByAllSubscribers_WhenHandled_ThenLogsWarningAndEnqueuesNothing()
    {
        var subscriber = CreateHttpSubscriber(
            filter: new FilterSetting { IncludedEventTypes = ["Some.Other.EventType"] }
        );
        var topic = CreateTopic(http: [subscriber]);
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger.ShouldHaveLogged(LogLevel.Warning, "filtered out");
        AssertNothingEnqueued();
    }

    [Fact]
    public async Task GivenEventGridEvent_WhenHandled_ThenEventIsEnriched()
    {
        var topic = CreateTopic("MyTestTopic");
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        events[0]
            .EventGridEvent.ShouldNotBeNullAnd()
            .Topic.ShouldNotBeNullAnd()
            .ShouldContain("MyTestTopic");
        events[0].EventGridEvent.ShouldNotBeNullAnd().MetadataVersion.ShouldBe("1");
    }

    [Fact]
    public async Task GivenCloudEvent_WhenHandled_ThenSourceIsPreservedIfSet()
    {
        var topic = CreateTopic("MyTestTopic");
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "Test.EventType",
            Source = "/original/source",
            Id = "test-id",
        };
        var events = new[] { SimulatorEvent.FromCloudEvent(cloudEvent) };
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.CloudEventV1_0
        );

        await _handler.Handle(command, CancellationToken.None);

        events[0].CloudEvent.ShouldNotBeNullAnd().Source.ShouldBe("/original/source");
    }

    [Fact]
    public async Task GivenCloudEventWithEmptySource_WhenHandled_ThenSourceIsSetToTopicPath()
    {
        var topic = CreateTopic("MyTestTopic");
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "Test.EventType",
            Source = "",
            Id = "test-id",
        };
        var events = new[] { SimulatorEvent.FromCloudEvent(cloudEvent) };
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.CloudEventV1_0
        );

        await _handler.Handle(command, CancellationToken.None);

        events[0]
            .CloudEvent.ShouldNotBeNullAnd()
            .Source.ShouldNotBeNullAnd()
            .ShouldContain("MyTestTopic");
    }

    [Fact]
    public async Task GivenEvents_WhenHandled_ThenLogsEventCount()
    {
        var topic = CreateTopic();
        var events = new[]
        {
            CreateTestEventGridEvent("event-1"),
            CreateTestEventGridEvent("event-2"),
        };
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger.ShouldHaveLogged(LogLevel.Information, "2 event(s) received");
    }

    [Fact]
    public async Task GivenDisabledHttpSubscriber_WhenHandled_ThenSkipsItAndEnqueuesForTheEnabledServiceBusSubscriber()
    {
        var httpSubscriber = CreateHttpSubscriber(disabled: true);
        var serviceBusSubscriber = CreateServiceBusSubscriber();
        var topic = CreateTopic(http: [httpSubscriber], serviceBus: [serviceBusSubscriber]);
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger.ShouldHaveLogged(LogLevel.Debug, "Skipping disabled subscriber");
        _deliveryQueue.Received(1).Enqueue(Arg.Any<PendingDelivery>());
        AssertEnqueuedOnce(serviceBusSubscriber, "test-id");
        AssertNotEnqueued(httpSubscriber);
    }

    [Fact]
    public async Task GivenEventGridSchema_WhenHandled_ThenSchemaIsLoggedCorrectly()
    {
        var topic = CreateTopic();
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger.ShouldHaveLogged(LogLevel.Information, "EventGridSchema");
    }

    [Fact]
    public async Task GivenCloudEventSchema_WhenHandled_ThenSchemaIsLoggedCorrectly()
    {
        var topic = CreateTopic();
        var events = new[] { CreateTestCloudEvent("test-id") };
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.CloudEventV1_0
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger.ShouldHaveLogged(LogLevel.Information, "CloudEventV1_0");
    }

    [Fact]
    public async Task GivenMultipleEvents_WhenHandled_ThenAllEventsAreEnriched()
    {
        var topic = CreateTopic("MyTestTopic");
        var events = new[]
        {
            CreateTestEventGridEvent("event-1"),
            CreateTestEventGridEvent("event-2"),
            CreateTestEventGridEvent("event-3"),
        };
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        foreach (
            var eventGridEvent in events.Select(evt => evt.EventGridEvent.ShouldNotBeNullAnd())
        )
        {
            eventGridEvent.Topic.ShouldNotBeNullAnd().ShouldContain("MyTestTopic");
            eventGridEvent.MetadataVersion.ShouldBe("1");
        }
    }

    [Theory]
    [InlineData(SubscriptionValidationStatus.ValidationEventSent)]
    [InlineData(SubscriptionValidationStatus.ValidationFailed)]
    public async Task GivenHttpSubscriberThatHasNotPassedValidation_WhenHandled_ThenEventIsNotEnqueued(
        SubscriptionValidationStatus validationStatus
    )
    {
        var subscriber = CreateHttpSubscriber(
            disableValidation: false,
            validationStatus: validationStatus
        );
        var topic = CreateTopic(http: [subscriber]);
        var command = new SendNotificationEventsToSubscriberCommand(
            CreateTestEvents(),
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger.ShouldHaveLogged(LogLevel.Warning, "still pending validation");
        AssertNothingEnqueued();
    }

    [Fact]
    public async Task GivenHttpSubscriberThatPassedValidation_WhenHandled_ThenEventIsEnqueued()
    {
        var subscriber = CreateHttpSubscriber(
            disableValidation: false,
            validationStatus: SubscriptionValidationStatus.ValidationSuccessful
        );
        var topic = CreateTopic(http: [subscriber]);
        var command = new SendNotificationEventsToSubscriberCommand(
            CreateTestEvents(),
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _deliveryQueue.Received(1).Enqueue(Arg.Any<PendingDelivery>());
        AssertEnqueuedOnce(subscriber, "test-id");
    }

    [Fact]
    public async Task GivenHttpSubscriberWithValidationDisabled_WhenHandled_ThenEventIsEnqueuedWithoutValidation()
    {
        // The startup validation sweep skips these subscribers, so their status is never set
        var subscriber = CreateHttpSubscriber(disableValidation: true, validationStatus: default);
        var topic = CreateTopic(http: [subscriber]);
        var command = new SendNotificationEventsToSubscriberCommand(
            CreateTestEvents(),
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _deliveryQueue.Received(1).Enqueue(Arg.Any<PendingDelivery>());
        AssertEnqueuedOnce(subscriber, "test-id");
        _logger.ShouldNotHaveLogged(LogLevel.Warning, "still pending validation");
    }

    [Fact]
    public async Task GivenFilterThatRejectsOneOfTwoSubscribers_WhenHandled_ThenOnlyTheOtherSubscriberGetsTheEvent()
    {
        var rejectingSubscriber = CreateHttpSubscriber(
            "RejectingSubscriber",
            filter: new FilterSetting { IncludedEventTypes = ["Some.Other.EventType"] }
        );
        var acceptingSubscriber = CreateHttpSubscriber(
            "AcceptingSubscriber",
            filter: new FilterSetting { IncludedEventTypes = ["Test.EventType"] }
        );
        var topic = CreateTopic(http: [rejectingSubscriber, acceptingSubscriber]);
        var command = new SendNotificationEventsToSubscriberCommand(
            CreateTestEvents(),
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _deliveryQueue.Received(1).Enqueue(Arg.Any<PendingDelivery>());
        AssertEnqueuedOnce(acceptingSubscriber, "test-id");
        AssertNotEnqueued(rejectingSubscriber);
        _logger.ShouldNotHaveLogged(LogLevel.Warning, "All subscribers of topic");
    }

    [Fact]
    public async Task GivenFilterThatRejectsOneOfTwoEvents_WhenHandled_ThenOnlyTheAcceptedEventIsEnqueued()
    {
        var subscriber = CreateHttpSubscriber(
            filter: new FilterSetting { IncludedEventTypes = ["Test.EventType"] }
        );
        var topic = CreateTopic(http: [subscriber]);
        var command = new SendNotificationEventsToSubscriberCommand(
            [
                CreateTestEventGridEvent("accepted-event"),
                CreateTestEventGridEvent("rejected-event", "Some.Other.EventType"),
            ],
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _deliveryQueue.Received(1).Enqueue(Arg.Any<PendingDelivery>());
        AssertEnqueuedOnce(subscriber, "accepted-event");
        _eventHistoryService
            .DidNotReceive()
            .RecordDeliveryQueued("rejected-event", Arg.Any<ISubscriberSettings>());
    }

    [Fact]
    public async Task GivenHttpAndServiceBusSubscribers_WhenHandled_ThenEachSubscriberGetsEveryEvent()
    {
        var httpSubscriber = CreateHttpSubscriber();
        var serviceBusSubscriber = CreateServiceBusSubscriber();
        var topic = CreateTopic(http: [httpSubscriber], serviceBus: [serviceBusSubscriber]);
        var events = new[]
        {
            CreateTestEventGridEvent("event-1"),
            CreateTestEventGridEvent("event-2"),
        };
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _deliveryQueue.Received(4).Enqueue(Arg.Any<PendingDelivery>());
        AssertEnqueuedOnce(httpSubscriber, "event-1");
        AssertEnqueuedOnce(httpSubscriber, "event-2");
        AssertEnqueuedOnce(serviceBusSubscriber, "event-1");
        AssertEnqueuedOnce(serviceBusSubscriber, "event-2");
        _eventHistoryService
            .Received(1)
            .RecordEventReceived(events[0], topic, EventSchema.EventGridSchema);
        _eventHistoryService
            .Received(1)
            .RecordEventReceived(events[1], topic, EventSchema.EventGridSchema);
    }

    [Fact]
    public async Task GivenEnabledSubscriber_WhenHandled_ThenPendingDeliveryIsStampedWithProviderTime()
    {
        var subscriber = CreateServiceBusSubscriber();
        var topic = CreateTopic(serviceBus: [subscriber]);
        var command = new SendNotificationEventsToSubscriberCommand(
            [CreateTestCloudEvent("cloud-event-id")],
            topic,
            EventSchema.CloudEventV1_0
        );
        var enqueued = new List<PendingDelivery>();
        _deliveryQueue
            .When(q => q.Enqueue(Arg.Any<PendingDelivery>()))
            .Do(call => enqueued.Add(call.Arg<PendingDelivery>()));

        await _handler.Handle(command, CancellationToken.None);

        var delivery = enqueued.ShouldHaveSingleItem();
        delivery.EnqueuedTime.ShouldBe(FixedTime);
        delivery.NextAttemptTime.ShouldBe(FixedTime);
        delivery.Subscriber.ShouldBeSameAs(subscriber);
        delivery.Topic.ShouldBeSameAs(topic);
        delivery.InputSchema.ShouldBe(EventSchema.CloudEventV1_0);
        delivery.Event.Id.ShouldBe("cloud-event-id");
        delivery.AttemptCount.ShouldBe(0);
    }

    private void AssertEnqueuedOnce(ISubscriberSettings subscriber, string eventId)
    {
        _deliveryQueue
            .Received(1)
            .Enqueue(
                Arg.Is<PendingDelivery>(d =>
                    ReferenceEquals(d.Subscriber, subscriber) && d.Event.Id == eventId
                )
            );
        _eventHistoryService.Received(1).RecordDeliveryQueued(eventId, subscriber);
    }

    private void AssertNotEnqueued(ISubscriberSettings subscriber)
    {
        _deliveryQueue
            .DidNotReceive()
            .Enqueue(Arg.Is<PendingDelivery>(d => ReferenceEquals(d.Subscriber, subscriber)));
        _eventHistoryService.DidNotReceive().RecordDeliveryQueued(Arg.Any<string?>(), subscriber);
    }

    private void AssertNothingEnqueued()
    {
        _deliveryQueue.DidNotReceive().Enqueue(Arg.Any<PendingDelivery>());
        _eventHistoryService
            .DidNotReceive()
            .RecordDeliveryQueued(Arg.Any<string?>(), Arg.Any<ISubscriberSettings>());
    }

    private static HttpSubscriberSettings CreateHttpSubscriber(
        string name = "TestHttpSubscriber",
        bool disabled = false,
        FilterSetting? filter = null,
        bool disableValidation = true,
        SubscriptionValidationStatus validationStatus =
            SubscriptionValidationStatus.ValidationSuccessful
    )
    {
        return new HttpSubscriberSettings
        {
            Name = name,
            Endpoint = "https://example.com/webhook",
            DisableValidation = disableValidation,
            ValidationStatus = validationStatus,
            Disabled = disabled,
            Filter = filter,
        };
    }

    private static ServiceBusSubscriberSettings CreateServiceBusSubscriber()
    {
        return new ServiceBusSubscriberSettings
        {
            Name = "ServiceBusSub",
            ConnectionString =
                "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Key;SharedAccessKey=abc123",
            Queue = "test-queue",
        };
    }

    private static TopicSettings CreateTopic(
        string name = "TestTopic",
        HttpSubscriberSettings[]? http = null,
        ServiceBusSubscriberSettings[]? serviceBus = null
    )
    {
        return new TopicSettings
        {
            Name = name,
            Port = 60101,
            Key = "TestKey",
            Subscribers = new SubscribersSettings { Http = http, ServiceBus = serviceBus },
        };
    }

    private static SimulatorEvent[] CreateTestEvents()
    {
        return [CreateTestEventGridEvent("test-id")];
    }

    private static SimulatorEvent CreateTestEventGridEvent(
        string id,
        string eventType = "Test.EventType"
    )
    {
        return SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = id,
                Subject = "/test/subject",
                EventType = eventType,
                EventTime = DateTimeOffset.UtcNow.ToString("o"),
                DataVersion = "1.0",
                Data = new { Property = "Value" },
            }
        );
    }

    private static SimulatorEvent CreateTestCloudEvent(string id)
    {
        return SimulatorEvent.FromCloudEvent(
            new CloudEvent
            {
                SpecVersion = "1.0",
                Type = "Test.EventType",
                Source = "/test/source",
                Id = id,
            }
        );
    }
}
