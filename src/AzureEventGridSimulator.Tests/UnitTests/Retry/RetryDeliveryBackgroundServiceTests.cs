using System.Net;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Domain.Services.Retry;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Retry;

/// <summary>
///     Drives the retry and dead-letter state machine one poll at a time through
///     ProcessDueDeliveriesAsync, so no timers are involved: the fake clock only moves when a test,
///     or the stub HTTP endpoint, moves it. The dead-letter reasons are read back from the JSON file
///     the real DeadLetterService writes, and are asserted as literals because they are output.
/// </summary>
[Trait("Category", "unit")]
public sealed class RetryDeliveryBackgroundServiceTests : IAsyncLifetime
{
    private const string EventId = "test-id-123";
    private const string SubscriberName = "TestSubscriber";
    private static readonly DateTimeOffset FixedTime = new(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly string _deadLetterFolder = Path.Combine(
        Path.GetTempPath(),
        $"retry-loop-tests-{Guid.NewGuid()}"
    );

    private readonly StubHttpMessageHandler _endpoint = new();
    private readonly EventHubEventDeliveryService _eventHub;
    private readonly IEventHistoryService _eventHistory = Substitute.For<IEventHistoryService>();
    private readonly HttpEventDeliveryService _http;
    private readonly HttpClient _httpClient;

    private readonly ILogger<RetryDeliveryBackgroundService> _logger = Substitute.For<
        ILogger<RetryDeliveryBackgroundService>
    >();

    private readonly InMemoryDeliveryQueue _queue;
    private readonly RetryScheduler _retryScheduler;
    private readonly RetryDeliveryBackgroundService _service;
    private readonly ServiceBusEventDeliveryService _serviceBus;
    private readonly StorageQueueEventDeliveryService _storageQueue;
    private readonly FakeTimeProvider _timeProvider = new(FixedTime);
    private bool? _deadLetterFileExistedWhenCompleted;

    public RetryDeliveryBackgroundServiceTests()
    {
        _queue = new InMemoryDeliveryQueue(
            _timeProvider,
            Substitute.For<ILogger<InMemoryDeliveryQueue>>()
        );
        _retryScheduler = new RetryScheduler(_timeProvider);

        // HttpEventDeliveryService over a stub handler, wired as HttpEventDeliveryServiceTests does
        _httpClient = new HttpClient(_endpoint);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_httpClient);
        var formatterFactory = new EventSchemaFormatterFactory(
            new EventGridSchemaFormatter(_timeProvider),
            new CloudEventSchemaFormatter()
        );
        var propertyResolver = new DeliveryPropertyResolver();

        _http = new HttpEventDeliveryService(
            httpClientFactory,
            formatterFactory,
            Substitute.For<ILogger<HttpEventDeliveryService>>()
        );
        _serviceBus = new ServiceBusEventDeliveryService(
            Substitute.For<ILogger<ServiceBusEventDeliveryService>>(),
            formatterFactory,
            propertyResolver
        );
        _storageQueue = new StorageQueueEventDeliveryService(
            Substitute.For<ILogger<StorageQueueEventDeliveryService>>(),
            formatterFactory
        );
        _eventHub = new EventHubEventDeliveryService(
            Substitute.For<ILogger<EventHubEventDeliveryService>>(),
            formatterFactory,
            propertyResolver
        );

        // Pins the order: the dead-letter file is written before the dashboard hears about it
        _eventHistory
            .When(h =>
                h.RecordDeliveryCompleted(
                    Arg.Any<string?>(),
                    Arg.Any<string?>(),
                    DeliveryStatus.DeadLettered,
                    Arg.Any<DateTimeOffset>()
                )
            )
            .Do(_ => _deadLetterFileExistedWhenCompleted = DeadLetterFiles().Length > 0);

        _service = CreateService();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _service.Dispose();
        await _serviceBus.DisposeAsync();
        await _storageQueue.DisposeAsync();
        await _eventHub.DisposeAsync();
        _httpClient.Dispose();
        _endpoint.Dispose();

        if (Directory.Exists(_deadLetterFolder))
        {
            Directory.Delete(_deadLetterFolder, true);
        }
    }

    [Fact]
    public async Task GivenExpiredDelivery_WhenProcessed_ThenDeadLetteredForTtlWithoutAnAttempt()
    {
        var subscriber = CreateHttpSubscriber(
            new RetryPolicySettings { EventTimeToLiveInMinutes = 60 }
        );
        var delivery = CreateDelivery(subscriber, enqueuedTime: FixedTime.AddMinutes(-61));

        await ProcessAsync(delivery);

        var deadLetter = ShouldHaveDeadLettered("EventTimeToLiveExpired");
        deadLetter.GetProperty("deliveryAttempts").GetInt32().ShouldBe(0);
        _endpoint.CallCount.ShouldBe(0);
        _eventHistory.DidNotReceiveWithAnyArgs().RecordDeliveryAttempt(default, default, default!);
        _logger.ShouldHaveLogged(
            LogLevel.Warning,
            $"Event {EventId} expired for subscriber '{SubscriberName}'. TTL exceeded"
        );
    }

    [Theory]
    [InlineData(3, 3, 3)]
    [InlineData(null, 30, 30)] // No retry policy: the default of 30 applies
    public async Task GivenDeliveryAtMaxAttempts_WhenProcessed_ThenDeadLetteredWithoutAnotherAttempt(
        int? maxDeliveryAttempts,
        int attemptCount,
        int expectedLoggedMaximum
    )
    {
        var retryPolicy = maxDeliveryAttempts is { } max
            ? new RetryPolicySettings { MaxDeliveryAttempts = max }
            : null;
        var delivery = CreateDelivery(CreateHttpSubscriber(retryPolicy), attemptCount);

        await ProcessAsync(delivery);

        var deadLetter = ShouldHaveDeadLettered("MaxDeliveryAttemptsExceeded");
        deadLetter.GetProperty("deliveryAttempts").GetInt32().ShouldBe(attemptCount);
        _endpoint.CallCount.ShouldBe(0);
        _eventHistory.DidNotReceiveWithAnyArgs().RecordDeliveryAttempt(default, default, default!);
        _logger.ShouldHaveLogged(
            LogLevel.Warning,
            $"Event {EventId} reached max delivery attempts ({expectedLoggedMaximum}) for subscriber '{SubscriberName}'"
        );
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "BadRequest")]
    [InlineData(HttpStatusCode.Unauthorized, "Unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "PayloadTooLarge")]
    public async Task GivenImmediateDeadLetterStatus_WhenDeliveryFails_ThenDeadLetteredWithoutRetry(
        HttpStatusCode statusCode,
        string expectedReason
    )
    {
        _endpoint.StatusCode = statusCode;
        var delivery = CreateDelivery(CreateHttpSubscriber());

        await ProcessAsync(delivery);

        var deadLetter = ShouldHaveDeadLettered(expectedReason);
        deadLetter.GetProperty("deliveryAttempts").GetInt32().ShouldBe(1);
        deadLetter.GetProperty("lastDeliveryOutcome").GetString().ShouldBe("HttpError");
        deadLetter.GetProperty("lastHttpStatusCode").GetInt32().ShouldBe((int)statusCode);
        _endpoint.CallCount.ShouldBe(1);
        ShouldHaveRecordedAttempt(1, DeliveryOutcome.HttpError, (int)statusCode);
        _logger.ShouldHaveLogged(
            LogLevel.Warning,
            $"Event {EventId} immediately dead-lettered due to HTTP {(int)statusCode} from '{SubscriberName}'"
        );
    }

    [Fact]
    public async Task GivenImmediateDeadLetterStatusAndRetryDisabled_WhenDeliveryFails_ThenTheStatusReasonWins()
    {
        _endpoint.StatusCode = HttpStatusCode.BadRequest;
        var subscriber = CreateHttpSubscriber(new RetryPolicySettings { Enabled = false });

        await ProcessAsync(CreateDelivery(subscriber));

        ShouldHaveDeadLettered("BadRequest");
    }

    [Fact]
    public async Task GivenRetryDisabled_WhenDeliveryFails_ThenDeadLetteredAsRetryDisabled()
    {
        _endpoint.StatusCode = HttpStatusCode.InternalServerError;
        var subscriber = CreateHttpSubscriber(new RetryPolicySettings { Enabled = false });

        await ProcessAsync(CreateDelivery(subscriber));

        var deadLetter = ShouldHaveDeadLettered("RetryDisabled_DeliveryFailed");
        deadLetter.GetProperty("deliveryAttempts").GetInt32().ShouldBe(1);
        deadLetter.GetProperty("lastHttpStatusCode").GetInt32().ShouldBe(500);
        _endpoint.CallCount.ShouldBe(1);
        ShouldHaveRecordedAttempt(1, DeliveryOutcome.HttpError, 500);
        _logger.ShouldHaveLogged(
            LogLevel.Warning,
            $"Event {EventId} delivery failed and retry is disabled for '{SubscriberName}'. Dead-lettering"
        );
    }

    [Fact]
    public async Task GivenLastAllowedAttempt_WhenDeliveryFails_ThenDeadLetteredForMaxAttempts()
    {
        _endpoint.StatusCode = HttpStatusCode.InternalServerError;
        var subscriber = CreateHttpSubscriber(new RetryPolicySettings { MaxDeliveryAttempts = 2 });

        await ProcessAsync(CreateDelivery(subscriber, 1));

        var deadLetter = ShouldHaveDeadLettered("MaxDeliveryAttemptsExceeded");
        deadLetter.GetProperty("deliveryAttempts").GetInt32().ShouldBe(2);
        deadLetter.GetProperty("lastHttpStatusCode").GetInt32().ShouldBe(500);
        _endpoint.CallCount.ShouldBe(1);
        ShouldHaveRecordedAttempt(2, DeliveryOutcome.HttpError, 500);
        _logger.ShouldHaveLogged(
            LogLevel.Warning,
            $"Event {EventId} reached max delivery attempts after failure for '{SubscriberName}'"
        );
    }

    [Fact]
    public async Task GivenTtlRunsOutDuringAnAttempt_WhenDeliveryFails_ThenDeadLetteredForTtl()
    {
        // Not expired when the attempt starts, but the endpoint takes two minutes to fail
        _endpoint.StatusCode = HttpStatusCode.InternalServerError;
        _endpoint.OnSend = () => _timeProvider.Advance(TimeSpan.FromMinutes(2));
        var subscriber = CreateHttpSubscriber(
            new RetryPolicySettings { EventTimeToLiveInMinutes = 60 }
        );
        var delivery = CreateDelivery(subscriber, enqueuedTime: FixedTime.AddMinutes(-59));

        await ProcessAsync(delivery);

        var deadLetter = ShouldHaveDeadLettered("EventTimeToLiveExpired", FixedTime.AddMinutes(2));
        deadLetter.GetProperty("deliveryAttempts").GetInt32().ShouldBe(1);
        _endpoint.CallCount.ShouldBe(1);
        _logger.ShouldHaveLogged(
            LogLevel.Warning,
            $"Event {EventId} TTL expired during retry for '{SubscriberName}'"
        );
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, 0, 10)]
    [InlineData(HttpStatusCode.InternalServerError, 3, 300)]
    [InlineData(HttpStatusCode.NotFound, 0, 300)]
    [InlineData(HttpStatusCode.RequestTimeout, 0, 120)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 0, 30)]
    public async Task GivenRetryableFailure_WhenProcessed_ThenRequeuedForTheSchedulersNextRetryTime(
        HttpStatusCode statusCode,
        int previousAttempts,
        int expectedDelaySeconds
    )
    {
        _endpoint.StatusCode = statusCode;
        var delivery = CreateDelivery(CreateHttpSubscriber(), previousAttempts);

        await ProcessAsync(delivery);

        var attemptNumber = previousAttempts + 1;
        _queue.Count.ShouldBe(1);
        delivery.AttemptCount.ShouldBe(attemptNumber);
        delivery.NextAttemptTime.ShouldBe(
            _retryScheduler.GetNextRetryTime(attemptNumber, (int)statusCode)
        );
        delivery.NextAttemptTime.ShouldBe(FixedTime.AddSeconds(expectedDelaySeconds));
        DeadLetterFiles().ShouldBeEmpty();
        ShouldHaveRecordedAttempt(attemptNumber, DeliveryOutcome.HttpError, (int)statusCode);
        _eventHistory
            .DidNotReceiveWithAnyArgs()
            .RecordDeliveryCompleted(default, default, default, default);
        _logger.ShouldHaveLogged(
            LogLevel.Information,
            $"(attempt {attemptNumber}/30) for '{SubscriberName}'"
        );
    }

    [Fact]
    public async Task GivenRetryPolicy_WhenRequeued_ThenTheLogShowsThePolicysMaximum()
    {
        _endpoint.StatusCode = HttpStatusCode.InternalServerError;
        var subscriber = CreateHttpSubscriber(new RetryPolicySettings { MaxDeliveryAttempts = 5 });

        await ProcessAsync(CreateDelivery(subscriber));

        _queue.Count.ShouldBe(1);
        _logger.ShouldHaveLogged(LogLevel.Information, $"(attempt 1/5) for '{SubscriberName}'");
    }

    [Fact]
    public async Task GivenRequeuedDelivery_WhenTheRetryFallsDue_ThenItIsRedeliveredAndCompleted()
    {
        _endpoint.StatusCode = HttpStatusCode.ServiceUnavailable;
        var delivery = CreateDelivery(CreateHttpSubscriber());
        await ProcessAsync(delivery);

        // Not due yet: another poll at the same time does nothing
        _endpoint.StatusCode = HttpStatusCode.OK;
        await _service.ProcessDueDeliveriesAsync(CancellationToken.None);
        _endpoint.CallCount.ShouldBe(1);
        _queue.Count.ShouldBe(1);

        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        await _service.ProcessDueDeliveriesAsync(CancellationToken.None);

        _endpoint.CallCount.ShouldBe(2);
        _queue.Count.ShouldBe(0);
        delivery.AttemptCount.ShouldBe(2);
        ShouldHaveRecordedAttempt(2, DeliveryOutcome.Success, 200, FixedTime.AddSeconds(30));
        _eventHistory
            .Received(1)
            .RecordDeliveryCompleted(
                EventId,
                SubscriberName,
                DeliveryStatus.Delivered,
                FixedTime.AddSeconds(30)
            );
    }

    [Fact]
    public async Task GivenSuccessfulDelivery_WhenProcessed_ThenRecordedAsDelivered()
    {
        _endpoint.StatusCode = HttpStatusCode.OK;
        var delivery = CreateDelivery(CreateHttpSubscriber());

        await ProcessAsync(delivery);

        _endpoint.CallCount.ShouldBe(1);
        _queue.Count.ShouldBe(0);
        delivery.AttemptCount.ShouldBe(1);
        ShouldHaveRecordedAttempt(1, DeliveryOutcome.Success, 200);
        _eventHistory
            .Received(1)
            .RecordDeliveryCompleted(EventId, SubscriberName, DeliveryStatus.Delivered, FixedTime);
        _eventHistory
            .DidNotReceive()
            .RecordDeliveryCompleted(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                DeliveryStatus.DeadLettered,
                Arg.Any<DateTimeOffset>()
            );
        DeadLetterFiles().ShouldBeEmpty();
    }

    // Each broker client throws before any network call because there is no connection string,
    // and the failure surfaces as the broker's *Error outcome
    [Theory]
    [InlineData("serviceBus", DeliveryOutcome.ServiceBusError)]
    [InlineData("storageQueue", DeliveryOutcome.StorageQueueError)]
    [InlineData("eventHub", DeliveryOutcome.EventHubError)]
    public async Task GivenBrokerDeliveryThatThrows_WhenProcessed_ThenTheAttemptHasTheBrokerErrorOutcome(
        string subscriberType,
        DeliveryOutcome expectedOutcome
    )
    {
        var retryDisabled = new RetryPolicySettings { Enabled = false };
        ISubscriberSettings subscriber = subscriberType switch
        {
            "serviceBus" => new ServiceBusSubscriberSettings
            {
                Name = SubscriberName,
                Queue = "my-queue",
                RetryPolicy = retryDisabled,
                DeadLetter = DeadLetterToTempFolder(),
            },
            "storageQueue" => new StorageQueueSubscriberSettings
            {
                Name = SubscriberName,
                QueueName = "my-queue",
                RetryPolicy = retryDisabled,
                DeadLetter = DeadLetterToTempFolder(),
            },
            _ => new EventHubSubscriberSettings
            {
                Name = SubscriberName,
                EventHubName = "my-event-hub",
                RetryPolicy = retryDisabled,
                DeadLetter = DeadLetterToTempFolder(),
            },
        };

        await ProcessAsync(CreateDelivery(subscriber));

        _eventHistory
            .Received(1)
            .RecordDeliveryAttempt(
                EventId,
                SubscriberName,
                Arg.Is<DeliveryAttempt>(a =>
                    a.AttemptNumber == 1
                    && a.Outcome == expectedOutcome
                    && a.HttpStatusCode == null
                    && !string.IsNullOrWhiteSpace(a.ErrorMessage)
                )
            );
        var deadLetter = ShouldHaveDeadLettered("RetryDisabled_DeliveryFailed");
        deadLetter
            .GetProperty("lastDeliveryOutcome")
            .GetString()
            .ShouldBe(expectedOutcome.ToString());
        deadLetter.TryGetProperty("lastHttpStatusCode", out _).ShouldBeFalse();
        _endpoint.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task GivenUnknownSubscriberType_WhenProcessed_ThenTheAttemptIsANetworkErrorAndIsRetried()
    {
        var delivery = CreateDelivery(new UnknownSubscriberSettings());

        await ProcessAsync(delivery);

        _eventHistory
            .Received(1)
            .RecordDeliveryAttempt(
                EventId,
                SubscriberName,
                Arg.Is<DeliveryAttempt>(a =>
                    a.Outcome == DeliveryOutcome.NetworkError
                    && a.ErrorMessage == "Unknown subscriber type"
                    && a.HttpStatusCode == null
                )
            );
        _queue.Count.ShouldBe(1);
        delivery.NextAttemptTime.ShouldBe(FixedTime.AddSeconds(10));
        _endpoint.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task GivenDeadLetteringDisabled_WhenDeadLettered_ThenCompletionIsRecordedWithoutAFile()
    {
        _endpoint.StatusCode = HttpStatusCode.BadRequest;
        var subscriber = CreateHttpSubscriber(deadLetter: false);

        await ProcessAsync(CreateDelivery(subscriber));

        DeadLetterFiles().ShouldBeEmpty();
        _queue.Count.ShouldBe(0);
        _eventHistory
            .Received(1)
            .RecordDeliveryCompleted(
                EventId,
                SubscriberName,
                DeliveryStatus.DeadLettered,
                FixedTime
            );
    }

    [Fact]
    public async Task GivenDeliveryNotYetDue_WhenProcessed_ThenItIsLeftInTheQueue()
    {
        var delivery = CreateDelivery(CreateHttpSubscriber());
        delivery.NextAttemptTime = FixedTime.AddSeconds(1);

        await ProcessAsync(delivery);

        _queue.Count.ShouldBe(1);
        delivery.AttemptCount.ShouldBe(0);
        _endpoint.CallCount.ShouldBe(0);
        _eventHistory.ReceivedCalls().ShouldBeEmpty();
    }

    private RetryDeliveryBackgroundService CreateService()
    {
        return new RetryDeliveryBackgroundService(
            _queue,
            _http,
            _serviceBus,
            _storageQueue,
            _eventHub,
            new DeadLetterService(Substitute.For<ILogger<DeadLetterService>>()),
            _eventHistory,
            _retryScheduler,
            _timeProvider,
            _logger
        );
    }

    private async Task ProcessAsync(PendingDelivery delivery)
    {
        _queue.Enqueue(delivery);
        await _service.ProcessDueDeliveriesAsync(CancellationToken.None);
    }

    private DeadLetterSettings DeadLetterToTempFolder()
    {
        return new DeadLetterSettings { Enabled = true, FolderPath = _deadLetterFolder };
    }

    private HttpSubscriberSettings CreateHttpSubscriber(
        RetryPolicySettings? retryPolicy = null,
        bool deadLetter = true
    )
    {
        return new HttpSubscriberSettings
        {
            Name = SubscriberName,
            Endpoint = "https://example.com/webhook",
            DisableValidation = true,
            ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful,
            RetryPolicy = retryPolicy,
            DeadLetter = deadLetter ? DeadLetterToTempFolder() : null,
        };
    }

    // Stamps both times from the fake clock, as SendNotificationEventsToSubscriberCommandHandler
    // does, so the delivery is due now and its TTL is measured against the fake clock
    private static PendingDelivery CreateDelivery(
        ISubscriberSettings subscriber,
        int attemptCount = 0,
        DateTimeOffset? enqueuedTime = null
    )
    {
        return new PendingDelivery
        {
            Event = TestHelpers.CreateSimulatorEventFromEventGrid(id: EventId),
            Subscriber = subscriber,
            Topic = TestHelpers.CreateValidTopicSettings(),
            InputSchema = EventSchema.EventGridSchema,
            EnqueuedTime = enqueuedTime ?? FixedTime,
            NextAttemptTime = FixedTime,
            AttemptCount = attemptCount,
        };
    }

    private string[] DeadLetterFiles()
    {
        return Directory.Exists(_deadLetterFolder)
            ? Directory.GetFiles(_deadLetterFolder, "*.json", SearchOption.AllDirectories)
            : [];
    }

    private JsonElement ShouldHaveDeadLettered(
        string expectedReason,
        DateTimeOffset? expectedCompletedAt = null
    )
    {
        var file = DeadLetterFiles().ShouldHaveSingleItem();
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var deadLetter = document.RootElement.Clone();

        deadLetter.GetProperty("deadLetterReason").GetString().ShouldBe(expectedReason);
        _eventHistory
            .Received(1)
            .RecordDeliveryCompleted(
                EventId,
                SubscriberName,
                DeliveryStatus.DeadLettered,
                expectedCompletedAt ?? FixedTime
            );
        _deadLetterFileExistedWhenCompleted.ShouldBe(true);
        _queue.Count.ShouldBe(0);

        return deadLetter;
    }

    private void ShouldHaveRecordedAttempt(
        int attemptNumber,
        DeliveryOutcome outcome,
        int httpStatusCode,
        DateTimeOffset? attemptTime = null
    )
    {
        _eventHistory
            .Received(1)
            .RecordDeliveryAttempt(
                EventId,
                SubscriberName,
                new DeliveryAttempt(
                    attemptNumber,
                    outcome,
                    attemptTime ?? FixedTime,
                    httpStatusCode,
                    outcome == DeliveryOutcome.Success
                        ? null
                        : ((HttpStatusCode)httpStatusCode).ToString()
                )
            );
    }

    /// <summary>
    ///     An HTTP endpoint that answers every request with <see cref="StatusCode" />, after running
    ///     <see cref="OnSend" /> (which can move the fake clock to simulate a slow endpoint).
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        public Action? OnSend { get; set; }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            OnSend?.Invoke();

            return Task.FromResult(
                new HttpResponseMessage(StatusCode)
                {
                    Content = new StringContent(""),
                    ReasonPhrase = StatusCode.ToString(),
                }
            );
        }
    }

    /// <summary>
    ///     A subscriber type the retry loop has no delivery service for.
    /// </summary>
    private sealed class UnknownSubscriberSettings : ISubscriberSettings
    {
        public string Name => SubscriberName;

        public FilterSetting? Filter => null;

        public bool Disabled => false;

        public EventSchema? DeliverySchema => null;

        public string SubscriberType => "unknown";

        public RetryPolicySettings? RetryPolicy => null;

        public DeadLetterSettings? DeadLetter => null;

        public void Validate() { }
    }
}
