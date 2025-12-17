using System;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Commands;

[Trait("Category", "unit")]
public class SendNotificationEventsToSubscriberCommandHandlerTests
{
    private readonly ILogger<SendNotificationEventsToSubscriberCommandHandler> _logger;
    private readonly SendNotificationEventsToSubscriberCommandHandler _handler;

    public SendNotificationEventsToSubscriberCommandHandlerTests()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<ILogger<SendNotificationEventsToSubscriberCommandHandler>>();

        var serviceBusLogger = Substitute.For<ILogger<ServiceBusEventDeliveryService>>();
        var storageQueueLogger = Substitute.For<ILogger<StorageQueueEventDeliveryService>>();
        var propertyResolver = new DeliveryPropertyResolver();

        var formatterFactory = new EventSchemaFormatterFactory(
            new EventGridSchemaFormatter(),
            new CloudEventSchemaFormatter()
        );

        var serviceBusDeliveryService = new ServiceBusEventDeliveryService(
            serviceBusLogger,
            formatterFactory,
            propertyResolver
        );

        var storageQueueDeliveryService = new StorageQueueEventDeliveryService(
            storageQueueLogger,
            formatterFactory
        );

        _handler = new SendNotificationEventsToSubscriberCommandHandler(
            httpClientFactory,
            _logger,
            formatterFactory,
            serviceBusDeliveryService,
            storageQueueDeliveryService
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
                Arg.Is<object>(o => o.ToString().Contains("has no subscribers")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public async Task GivenAllSubscribersDisabled_WhenHandled_ThenLogsWarning()
    {
        var subscriber = CreateHttpSubscriber();
        subscriber.Disabled = true;
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
                Arg.Is<object>(o => o.ToString().Contains("has no enabled subscribers")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public async Task GivenEventFilteredByAllSubscribers_WhenHandled_ThenLogsWarningForFilteredEvent()
    {
        var subscriber = CreateHttpSubscriber();
        subscriber.Filter = new FilterSetting
        {
            IncludedEventTypes = new[] { "Some.Other.EventType" },
        };
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
                Arg.Is<object>(o => o.ToString().Contains("filtered out")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public async Task GivenEventGridEvent_WhenHandled_ThenEventIsEnriched()
    {
        var topic = CreateTopicWithoutSubscribers();
        topic.Name = "MyTestTopic";
        var events = CreateTestEvents();
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.EventGridSchema
        );

        await _handler.Handle(command, CancellationToken.None);

        events[0].EventGridEvent.Topic.ShouldContain("MyTestTopic");
        events[0].EventGridEvent.MetadataVersion.ShouldBe("1");
    }

    [Fact]
    public async Task GivenCloudEvent_WhenHandled_ThenSourceIsPreservedIfSet()
    {
        var topic = CreateTopicWithoutSubscribers();
        topic.Name = "MyTestTopic";
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

        events[0].CloudEvent.Source.ShouldBe("/original/source");
    }

    [Fact]
    public async Task GivenCloudEventWithoutSource_WhenHandled_ThenSourceIsSetToTopicPath()
    {
        var topic = CreateTopicWithoutSubscribers();
        topic.Name = "MyTestTopic";
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "Test.EventType",
            Source = null,
            Id = "test-id",
        };
        var events = new[] { SimulatorEvent.FromCloudEvent(cloudEvent) };
        var command = new SendNotificationEventsToSubscriberCommand(
            events,
            topic,
            EventSchema.CloudEventV1_0
        );

        await _handler.Handle(command, CancellationToken.None);

        events[0].CloudEvent.Source.ShouldContain("MyTestTopic");
    }

    [Fact]
    public async Task GivenCloudEventWithEmptySource_WhenHandled_ThenSourceIsSetToTopicPath()
    {
        var topic = CreateTopicWithoutSubscribers();
        topic.Name = "MyTestTopic";
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

        events[0].CloudEvent.Source.ShouldContain("MyTestTopic");
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
                Arg.Is<object>(o => o.ToString().Contains("2 event(s) received")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public async Task GivenDisabledHttpSubscriber_WhenHandled_ThenLogsWarningAndSkips()
    {
        var subscriber = CreateHttpSubscriber();
        subscriber.Disabled = true;
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
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
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
                Arg.Is<object>(o => o.ToString().Contains("EventGridSchema")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
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
                Arg.Is<object>(o => o.ToString().Contains("CloudEventV1_0")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public async Task GivenMultipleEvents_WhenHandled_ThenAllEventsAreEnriched()
    {
        var topic = CreateTopicWithoutSubscribers();
        topic.Name = "MyTestTopic";
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

        foreach (var evt in events)
        {
            evt.EventGridEvent.Topic.ShouldContain("MyTestTopic");
            evt.EventGridEvent.MetadataVersion.ShouldBe("1");
        }
    }

    private static HttpSubscriberSettings CreateHttpSubscriber()
    {
        return new HttpSubscriberSettings
        {
            Name = "TestHttpSubscriber",
            Endpoint = "https://example.com/webhook",
            DisableValidation = true,
            ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful,
        };
    }

    private static TopicSettings CreateTopicWithoutSubscribers()
    {
        return new TopicSettings
        {
            Name = "TestTopic",
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
            Subscribers = new SubscribersSettings { Http = new[] { subscriber } },
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
                Http = new[] { httpSubscriber },
                ServiceBus = new[]
                {
                    new ServiceBusSubscriberSettings
                    {
                        Name = "ServiceBusSub",
                        ConnectionString =
                            "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Key;SharedAccessKey=abc123",
                        Queue = "test-queue",
                    },
                },
            },
        };
    }

    private static SimulatorEvent[] CreateTestEvents()
    {
        return new[] { CreateTestEventGridEvent("test-id") };
    }

    private static SimulatorEvent CreateTestEventGridEvent(string id)
    {
        return SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = id,
                Subject = "/test/subject",
                EventType = "Test.EventType",
                EventTime = DateTime.UtcNow.ToString("o"),
                DataVersion = "1.0",
                Data = new { Property = "Value" },
            }
        );
    }
}
