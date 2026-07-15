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
            TimeProvider.System,
            _logger
        );
    }

    [Fact]
    public async Task GivenNoSubscribers_WhenHandled_ThenLogsWarning()
    {
        var topic = CreateTopicWithoutSubscribers();
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => string.Concat(o).Contains("has no subscribers")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public async Task GivenAllSubscribersDisabled_WhenHandled_ThenLogsWarning()
    {
        var subscriber = CreateHttpSubscriber(true);
        var topic = CreateTopicWithHttpSubscriber(subscriber);
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => string.Concat(o).Contains("has no enabled subscribers")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public async Task GivenEventFilteredByAllSubscribers_WhenHandled_ThenLogsWarningForFilteredEvent()
    {
        var subscriber = CreateHttpSubscriber(
            filter: new FilterSetting { IncludedEventTypes = new[] { "Some.Other.EventType" } }
        );
        var topic = CreateTopicWithHttpSubscriber(subscriber);
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => string.Concat(o).Contains("filtered out")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public async Task GivenEventGridEvent_WhenHandled_ThenEventIsEnriched()
    {
        var topic = CreateTopicWithoutSubscribers("MyTestTopic");
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
        var topic = CreateTopicWithoutSubscribers("MyTestTopic");
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
        var topic = CreateTopicWithoutSubscribers("MyTestTopic");
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
        var topic = CreateTopicWithoutSubscribers();
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

        _logger
            .Received()
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => string.Concat(o).Contains("2 event(s) received")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public async Task GivenDisabledHttpSubscriber_WhenHandled_ThenLogsDebugAndSkips()
    {
        var subscriber = CreateHttpSubscriber(true);
        var topic = CreateTopicWithMixedSubscribers(subscriber);
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger
            .Received()
            .Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => string.Concat(o).Contains("Skipping disabled subscriber")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public async Task GivenEventGridSchema_WhenHandled_ThenSchemaIsLoggedCorrectly()
    {
        var topic = CreateTopicWithoutSubscribers();
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger
            .Received()
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => string.Concat(o).Contains("EventGridSchema")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public async Task GivenCloudEventSchema_WhenHandled_ThenSchemaIsLoggedCorrectly()
    {
        var topic = CreateTopicWithoutSubscribers();
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "Test.EventType",
            Source = "/test/source",
            Id = "test-id",
        };
        var events = new[] { SimulatorEvent.FromCloudEvent(cloudEvent) };
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.CloudEventV1_0
        );

        await _handler.Handle(command, CancellationToken.None);

        _logger
            .Received()
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => string.Concat(o).Contains("CloudEventV1_0")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public async Task GivenMultipleEvents_WhenHandled_ThenAllEventsAreEnriched()
    {
        var topic = CreateTopicWithoutSubscribers("MyTestTopic");
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

    private static HttpSubscriberSettings CreateHttpSubscriber(
        bool disabled = false,
        FilterSetting? filter = null
    )
    {
        return new HttpSubscriberSettings
        {
            Name = "TestHttpSubscriber",
            Endpoint = "https://example.com/webhook",
            DisableValidation = true,
            ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful,
            Disabled = disabled,
            Filter = filter,
        };
    }

    private static TopicSettings CreateTopicWithoutSubscribers(string name = "TestTopic")
    {
        return new TopicSettings
        {
            Name = name,
            Port = 60101,
            Key = "TestKey",
            Subscribers = new SubscribersSettings(),
        };
    }

    private static TopicSettings CreateTopicWithHttpSubscriber(HttpSubscriberSettings subscriber)
    {
        return new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
            Subscribers = new SubscribersSettings { Http = [subscriber] },
        };
    }

    private static TopicSettings CreateTopicWithMixedSubscribers(
        HttpSubscriberSettings httpSubscriber
    )
    {
        return new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
            Subscribers = new SubscribersSettings
            {
                Http = [httpSubscriber],
                ServiceBus =
                [
                    new ServiceBusSubscriberSettings
                    {
                        Name = "ServiceBusSub",
                        ConnectionString =
                            "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Key;SharedAccessKey=abc123",
                        Queue = "test-queue",
                    },
                ],
            },
        };
    }

    private static SimulatorEvent[] CreateTestEvents()
    {
        return [CreateTestEventGridEvent("test-id")];
    }

    private static SimulatorEvent CreateTestEventGridEvent(string id)
    {
        return SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = id,
                Subject = "/test/subject",
                EventType = "Test.EventType",
                EventTime = DateTimeOffset.UtcNow.ToString("o"),
                DataVersion = "1.0",
                Data = new { Property = "Value" },
            }
        );
    }
}
