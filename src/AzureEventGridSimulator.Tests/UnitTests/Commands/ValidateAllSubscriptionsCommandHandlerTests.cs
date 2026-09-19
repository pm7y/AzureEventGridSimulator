using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Commands;

/// <summary>
///     Covers the startup handshake that decides whether HTTP subscribers ever
///     receive events: the outgoing SubscriptionValidation request, each way it
///     can fail, and which subscribers are skipped.
/// </summary>
[Trait("Category", "unit")]
public sealed class ValidateAllSubscriptionsCommandHandlerTests : IDisposable
{
    private const string ValidationHost = "10.0.0.5";
    private const int TopicPort = 60101;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ValidateAllSubscriptionsCommandHandler> _logger;
    private readonly StubHttpMessageHandler _subscriber = new();

    public ValidateAllSubscriptionsCommandHandlerTests()
    {
        _logger = Substitute.For<ILogger<ValidateAllSubscriptionsCommandHandler>>();

        // The handler disposes the client it creates, so hand out a new one on every call
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory
            .CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient(_subscriber, disposeHandler: false));
    }

    public void Dispose()
    {
        _subscriber.Dispose();
    }

    [Fact]
    public async Task GivenSubscriberEchoesValidationCode_WhenHandled_ThenValidationSuccessful()
    {
        _subscriber.Respond = EchoValidationCode;
        var subscriber = CreateHttpSubscriber("Echo");

        await HandleAsync(CreateTopic(subscriber));

        subscriber.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);
        _subscriber.Requests.Count.ShouldBe(1);
        VerifyLogged(LogLevel.Information, "Successfully validated subscriber 'Echo'");
        VerifyNothingLogged(LogLevel.Error);
    }

    [Fact]
    public async Task GivenSubscriberReturnsServerError_WhenHandled_ThenValidationFailed()
    {
        // Even a correct echo doesn't count when it comes with an error status code
        _subscriber.Respond = request =>
        {
            var response = EchoValidationCode(request);
            response.StatusCode = HttpStatusCode.InternalServerError;
            return response;
        };
        var subscriber = CreateHttpSubscriber("Broken");

        await HandleAsync(CreateTopic(subscriber));

        subscriber.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationFailed);
        VerifyLogged(LogLevel.Error, "Failed to validate subscriber 'Broken'");
        VerifyLogged(
            LogLevel.Information,
            $"manual validation url: https://{ValidationHost}:{TopicPort}/validate?id={subscriber.ValidationCode}"
        );
    }

    [Fact]
    public async Task GivenSubscriberEchoesWrongCode_WhenHandled_ThenValidationFailed()
    {
        _subscriber.Respond = _ =>
            JsonResponse($$"""{"validationResponse":"{{Guid.NewGuid()}}"}""");
        var subscriber = CreateHttpSubscriber("WrongCode");

        await HandleAsync(CreateTopic(subscriber));

        subscriber.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationFailed);
    }

    [Fact]
    public async Task GivenSubscriberReturnsEmptyBody_WhenHandled_ThenValidationFailed()
    {
        _subscriber.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("", Encoding.UTF8),
        };
        var subscriber = CreateHttpSubscriber("NoEcho");

        await HandleAsync(CreateTopic(subscriber));

        subscriber.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationFailed);
        VerifyLogged(LogLevel.Error, "Failed to validate subscriber 'NoEcho'");
    }

    [Fact]
    public async Task GivenHttpClientTimesOut_WhenHandled_ThenValidationFailed()
    {
        // HttpClient reports its own timeout as a TaskCanceledException even though the
        // caller's token was never cancelled: that is a failed validation, not a shutdown
        _subscriber.Respond = _ =>
            throw new TaskCanceledException(
                "The request was canceled due to the configured HttpClient.Timeout.",
                new TimeoutException()
            );
        var subscriber = CreateHttpSubscriber("Slow");

        await HandleAsync(CreateTopic(subscriber));

        subscriber.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationFailed);
        VerifyLogged(LogLevel.Error, "Failed to validate subscriber 'Slow'");
    }

    [Fact]
    public async Task GivenShutdownWhileWaitingForSubscriber_WhenHandled_ThenStopsWithoutReportingFailure()
    {
        // Ctrl-C arrives while a subscriber that never answers is being validated
        using var shutdown = new CancellationTokenSource();
        _subscriber.WaitBeforeResponding = cancellationToken =>
        {
            shutdown.Cancel();
            return Task.Delay(Timeout.Infinite, cancellationToken);
        };
        var hanging = CreateHttpSubscriber("Hanging", "https://hanging.test/events");
        var next = CreateHttpSubscriber("Next", "https://next.test/events");

        await Should.NotThrowAsync(() => HandleAsync(shutdown.Token, CreateTopic(hanging, next)));

        hanging.ValidationStatus.ShouldNotBe(SubscriptionValidationStatus.ValidationFailed);
        next.ValidationStatus.ShouldNotBe(SubscriptionValidationStatus.ValidationFailed);
        VerifyNothingLogged(LogLevel.Error);
    }

    [Fact]
    public async Task GivenOneSubscriberFails_WhenHandled_ThenTheNextSubscriberIsStillValidated()
    {
        _subscriber.Respond = request =>
            request.Uri?.Host == "broken.test"
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : EchoValidationCode(request);
        var broken = CreateHttpSubscriber("Broken", "https://broken.test/events");
        var healthy = CreateHttpSubscriber("Healthy", "https://healthy.test/events");

        await HandleAsync(CreateTopic(broken, healthy));

        broken.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationFailed);
        healthy.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);
        _subscriber.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GivenSubscriber_WhenHandled_ThenSendsSubscriptionValidationEventToItsEndpoint()
    {
        _subscriber.Respond = EchoValidationCode;
        var subscriber = CreateHttpSubscriber("MySubscriber", "https://subscriber.test/api/events");

        await HandleAsync(CreateTopic(subscriber));

        var request = _subscriber.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.ShouldBe(new Uri("https://subscriber.test/api/events"));
        request.ContentType.ShouldBe("application/json; charset=utf-8");

        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetArrayLength().ShouldBe(1);
        var evt = body.RootElement[0];
        evt.GetProperty("eventType")
            .GetString()
            .ShouldBe("Microsoft.EventGrid.SubscriptionValidationEvent");
        evt.GetProperty("dataVersion").GetString().ShouldBe("1");
        evt.GetProperty("metadataVersion").GetString().ShouldBe("1");

        var data = evt.GetProperty("data");
        data.GetProperty("validationCode").GetGuid().ShouldBe(subscriber.ValidationCode);
        data.GetProperty("validationUrl")
            .GetString()
            .ShouldBe(
                $"https://{ValidationHost}:{TopicPort}/validate?id={subscriber.ValidationCode}"
            );
    }

    [Fact]
    public async Task GivenSubscriber_WhenHandled_ThenSendsEventGridValidationHeaders()
    {
        _subscriber.Respond = EchoValidationCode;
        var subscriber = CreateHttpSubscriber("MySubscriber");

        await HandleAsync(CreateTopic(subscriber));

        var headers = _subscriber.Requests.ShouldHaveSingleItem().Headers;
        headers[Constants.AegEventTypeHeader].ShouldBe(Constants.ValidationEventType);
        headers[Constants.AegSubscriptionNameHeader].ShouldBe("MYSUBSCRIBER");
        headers[Constants.AegDataVersionHeader].ShouldBe("1");
        headers[Constants.AegMetadataVersionHeader].ShouldBe("1");
        headers[Constants.AegDeliveryCountHeader].ShouldBe("0");
    }

    [Fact]
    public async Task GivenSubscriber_WhenHandled_ThenUsesTheSimulatorsNamedHttpClient()
    {
        // The named client carries the timeout and the optional certificate bypass
        _subscriber.Respond = EchoValidationCode;

        await HandleAsync(CreateTopic(CreateHttpSubscriber("MySubscriber")));

        _httpClientFactory.Received(1).CreateClient(Constants.HttpClientName);
    }

    [Fact]
    public async Task GivenDisabledTopic_WhenHandled_ThenItsSubscribersAreNotValidated()
    {
        _subscriber.Respond = EchoValidationCode;
        var onDisabledTopic = CreateHttpSubscriber("OnDisabled", "https://on-disabled.test/events");
        var onEnabledTopic = CreateHttpSubscriber("OnEnabled", "https://on-enabled.test/events");
        var disabledTopic = new TopicSettings
        {
            Name = "DisabledTopic",
            Port = TopicPort + 1,
            Disabled = true,
            Subscribers = new SubscribersSettings { Http = [onDisabledTopic] },
        };

        await HandleAsync(disabledTopic, CreateTopic(onEnabledTopic));

        _subscriber.Requests.ShouldHaveSingleItem().Uri?.Host.ShouldBe("on-enabled.test");
        onDisabledTopic.ValidationStatus.ShouldBe(default(SubscriptionValidationStatus));
        onEnabledTopic.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);
    }

    [Fact]
    public async Task GivenDisabledSubscriber_WhenHandled_ThenItIsNotValidated()
    {
        _subscriber.Respond = EchoValidationCode;
        var disabled = new HttpSubscriberSettings
        {
            Name = "Disabled",
            Endpoint = "https://disabled.test/events",
            Disabled = true,
        };
        var enabled = CreateHttpSubscriber("Enabled", "https://enabled.test/events");

        await HandleAsync(CreateTopic(disabled, enabled));

        _subscriber.Requests.ShouldHaveSingleItem().Uri?.Host.ShouldBe("enabled.test");
        disabled.ValidationStatus.ShouldBe(default(SubscriptionValidationStatus));
        enabled.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);
    }

    [Fact]
    public async Task GivenSubscriberWithValidationDisabled_WhenHandled_ThenItIsNotValidated()
    {
        _subscriber.Respond = EchoValidationCode;
        var noValidation = new HttpSubscriberSettings
        {
            Name = "NoValidation",
            Endpoint = "https://no-validation.test/events",
            DisableValidation = true,
        };
        var enabled = CreateHttpSubscriber("Enabled", "https://enabled.test/events");

        await HandleAsync(CreateTopic(noValidation, enabled));

        _subscriber.Requests.ShouldHaveSingleItem().Uri?.Host.ShouldBe("enabled.test");
        noValidation.ValidationStatus.ShouldBe(default(SubscriptionValidationStatus));
        enabled.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);
    }

    private Task HandleAsync(params TopicSettings[] topics)
    {
        return HandleAsync(CancellationToken.None, topics);
    }

    private async Task HandleAsync(
        CancellationToken cancellationToken,
        params TopicSettings[] topics
    )
    {
        var handler = new ValidateAllSubscriptionsCommandHandler(
            _logger,
            _httpClientFactory,
            new SimulatorSettings { Topics = topics },
            CreateValidationIpAddressProvider(),
            TimeProvider.System
        );

        await handler.Handle(new ValidateAllSubscriptionsCommand(), cancellationToken);
    }

    private void VerifyLogged(LogLevel level, string expectedText)
    {
        _logger
            .Received()
            .Log(
                level,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o != null && string.Concat(o).Contains(expectedText)),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    private void VerifyNothingLogged(LogLevel level)
    {
        _logger
            .DidNotReceive()
            .Log(
                level,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    private static ValidationIpAddressProvider CreateValidationIpAddressProvider()
    {
        // Pin the validationUrl host instead of reading this machine's network interfaces
        return new ValidationIpAddressProvider(() =>
            [
                new ValidationIpAddressProvider.NetworkInterfaceInfo(
                    OperationalStatus.Up,
                    NetworkInterfaceType.Ethernet,
                    [IPAddress.Parse(ValidationHost)],
                    [IPAddress.Parse("10.0.0.1")]
                ),
            ]
        );
    }

    private static HttpSubscriberSettings CreateHttpSubscriber(
        string name,
        string endpoint = "https://subscriber.test/events"
    )
    {
        return new HttpSubscriberSettings { Name = name, Endpoint = endpoint };
    }

    private static TopicSettings CreateTopic(params HttpSubscriberSettings[] subscribers)
    {
        return new TopicSettings
        {
            Name = "TestTopic",
            Port = TopicPort,
            Subscribers = new SubscribersSettings { Http = subscribers },
        };
    }

    private static HttpResponseMessage EchoValidationCode(RecordedRequest request)
    {
        // Behave like a well-implemented subscriber: echo the validation code back
        using var body = JsonDocument.Parse(request.Body);
        var validationCode = body.RootElement[0]
            .GetProperty("data")
            .GetProperty("validationCode")
            .GetGuid();

        return JsonResponse($$"""{"validationResponse":"{{validationCode}}"}""");
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>
    ///     An outbound request, recorded before the handler disposes its content.
    /// </summary>
    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri? Uri,
        string? ContentType,
        string Body,
        IReadOnlyDictionary<string, string> Headers
    );

    /// <summary>
    ///     Stands in for the subscriber: records every request and answers it with
    ///     <see cref="Respond" />.
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<RecordedRequest> _requests = [];

        public Func<RecordedRequest, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        /// <summary>
        ///     Optionally keeps the request waiting, e.g. to act as a subscriber that never answers.
        /// </summary>
        public Func<CancellationToken, Task>? WaitBeforeResponding { get; set; }

        public IReadOnlyList<RecordedRequest> Requests => _requests;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var body =
                request.Content == null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken);

            var recorded = new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Content?.Headers.ContentType?.ToString(),
                body,
                request.Headers.ToDictionary(
                    h => h.Key,
                    h => string.Join(",", h.Value),
                    StringComparer.OrdinalIgnoreCase
                )
            );
            _requests.Add(recorded);

            if (WaitBeforeResponding != null)
            {
                await WaitBeforeResponding(cancellationToken);
            }

            return Respond(recorded);
        }
    }
}
