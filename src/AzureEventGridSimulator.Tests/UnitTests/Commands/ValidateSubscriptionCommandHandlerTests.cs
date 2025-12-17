using System;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Commands;

[Trait("Category", "unit")]
public class ValidateSubscriptionCommandHandlerTests
{
    private readonly ILogger<ValidateSubscriptionCommandHandler> _logger;
    private readonly ValidateSubscriptionCommandHandler _handler;

    public ValidateSubscriptionCommandHandlerTests()
    {
        _logger = Substitute.For<ILogger<ValidateSubscriptionCommandHandler>>();
        _handler = new ValidateSubscriptionCommandHandler(_logger);
    }

    [Fact]
    public async Task GivenMatchingValidationCode_WhenHandled_ThenReturnsTrue()
    {
        var subscriber = CreateHttpSubscriber("https://example.com/webhook");
        var topic = CreateTopicWithSubscriber(subscriber);
        var command = new ValidateSubscriptionCommand(topic, subscriber.ValidationCode);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task GivenMatchingValidationCode_WhenHandled_ThenSetsValidationStatusToSuccessful()
    {
        var subscriber = CreateHttpSubscriber("https://example.com/webhook");
        var topic = CreateTopicWithSubscriber(subscriber);
        var command = new ValidateSubscriptionCommand(topic, subscriber.ValidationCode);

        await _handler.Handle(command, CancellationToken.None);

        subscriber.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);
    }

    [Fact]
    public async Task GivenMatchingValidationCode_WhenHandled_ThenLogsInformation()
    {
        var subscriber = CreateHttpSubscriber("https://example.com/webhook");
        var topic = CreateTopicWithSubscriber(subscriber);
        var command = new ValidateSubscriptionCommand(topic, subscriber.ValidationCode);

        await _handler.Handle(command, CancellationToken.None);

        _logger
            .Received()
            .Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains("successfully validated")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public async Task GivenNonMatchingValidationCode_WhenHandled_ThenReturnsFalse()
    {
        var subscriber = CreateHttpSubscriber("https://example.com/webhook");
        var topic = CreateTopicWithSubscriber(subscriber);
        var wrongCode = Guid.NewGuid();
        var command = new ValidateSubscriptionCommand(topic, wrongCode);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GivenNonMatchingValidationCode_WhenHandled_ThenLogsWarning()
    {
        var subscriber = CreateHttpSubscriber("https://example.com/webhook");
        var topic = CreateTopicWithSubscriber(subscriber);
        var wrongCode = Guid.NewGuid();
        var command = new ValidateSubscriptionCommand(topic, wrongCode);

        await _handler.Handle(command, CancellationToken.None);

        _logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString().Contains("Validation failed")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>()
            );
    }

    [Fact]
    public async Task GivenNoHttpSubscribers_WhenHandled_ThenReturnsFalse()
    {
        var topic = new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
            Subscribers = new SubscribersSettings(),
        };
        var command = new ValidateSubscriptionCommand(topic, Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GivenOnlyServiceBusSubscribers_WhenHandled_ThenReturnsFalse()
    {
        var topic = new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
            Subscribers = new SubscribersSettings
            {
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
        var command = new ValidateSubscriptionCommand(topic, Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GivenMultipleSubscribers_WhenCorrectCodeProvided_ThenOnlyMatchingSubscriberValidated()
    {
        var subscriber1 = CreateHttpSubscriber("https://example.com/webhook1");
        var subscriber2 = CreateHttpSubscriber("https://example.com/webhook2");
        var topic = new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
            Subscribers = new SubscribersSettings { Http = new[] { subscriber1, subscriber2 } },
        };
        var command = new ValidateSubscriptionCommand(topic, subscriber1.ValidationCode);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.ShouldBeTrue();
        subscriber1.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);
        subscriber2.ValidationStatus.ShouldBe(default(SubscriptionValidationStatus));
    }

    [Fact]
    public async Task GivenMultipleSubscribers_WhenNoMatchingCode_ThenReturnsFalse()
    {
        var subscriber1 = CreateHttpSubscriber("https://example.com/webhook1");
        var subscriber2 = CreateHttpSubscriber("https://example.com/webhook2");
        var topic = new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
            Subscribers = new SubscribersSettings { Http = new[] { subscriber1, subscriber2 } },
        };
        var command = new ValidateSubscriptionCommand(topic, Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.ShouldBeFalse();
        subscriber1.ValidationStatus.ShouldBe(default(SubscriptionValidationStatus));
        subscriber2.ValidationStatus.ShouldBe(default(SubscriptionValidationStatus));
    }

    [Fact]
    public async Task GivenCancellationToken_WhenHandled_ThenCompletesSuccessfully()
    {
        var subscriber = CreateHttpSubscriber("https://example.com/webhook");
        var topic = CreateTopicWithSubscriber(subscriber);
        var command = new ValidateSubscriptionCommand(topic, subscriber.ValidationCode);
        using var cts = new CancellationTokenSource();

        var result = await _handler.Handle(command, cts.Token);

        result.ShouldBeTrue();
    }

    private static HttpSubscriberSettings CreateHttpSubscriber(string endpoint)
    {
        return new HttpSubscriberSettings
        {
            Name = $"Subscriber_{Guid.NewGuid():N}",
            Endpoint = endpoint,
        };
    }

    private static TopicSettings CreateTopicWithSubscriber(HttpSubscriberSettings subscriber)
    {
        return new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
            Subscribers = new SubscribersSettings { Http = new[] { subscriber } },
        };
    }
}
