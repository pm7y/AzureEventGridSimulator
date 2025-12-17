using System.Net;
using System.Net.Http.Headers;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Retry;

[Trait("Category", "unit")]
public class HttpEventDeliveryServiceTests
{
    private readonly EventSchemaFormatterFactory _formatterFactory;
    private readonly ILogger<HttpEventDeliveryService> _logger;

    public HttpEventDeliveryServiceTests()
    {
        _logger = Substitute.For<ILogger<HttpEventDeliveryService>>();
        _formatterFactory = new EventSchemaFormatterFactory(
            new EventGridSchemaFormatter(),
            new CloudEventSchemaFormatter()
        );
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task GivenSuccessStatusCode_WhenDelivering_ThenReturnsSuccess(
        HttpStatusCode statusCode
    )
    {
        var httpClientFactory = CreateMockHttpClientFactory(statusCode);
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = CreatePendingDelivery();

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Outcome.ShouldBe(DeliveryOutcome.Success);
        result.HttpStatusCode.ShouldBe((int)statusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task GivenErrorStatusCode_WhenDelivering_ThenReturnsHttpError(
        HttpStatusCode statusCode
    )
    {
        var httpClientFactory = CreateMockHttpClientFactory(statusCode);
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = CreatePendingDelivery();

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.HttpError);
        result.HttpStatusCode.ShouldBe((int)statusCode);
    }

    [Fact]
    public async Task GivenTimeout_WhenDelivering_ThenReturnsTimeout()
    {
        // HTTP client timeout throws TaskCanceledException with a different cancellation token
        var timeoutCts = new CancellationTokenSource();
        var timeoutException = new TaskCanceledException(
            "The request was cancelled due to timeout",
            new TimeoutException("The operation timed out"),
            timeoutCts.Token
        );
        var httpClientFactory = CreateMockHttpClientFactory(throwException: timeoutException);
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = CreatePendingDelivery();

        // Pass a DIFFERENT cancellation token than the one in the exception
        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.Timeout);
        result.ErrorMessage.ShouldBe("Request timed out");
    }

    [Fact]
    public async Task GivenCancellation_WhenDelivering_ThenReturnsCancelled()
    {
        var cts = new CancellationTokenSource();
        var httpClientFactory = CreateMockHttpClientFactory(
            responseAction: () => cts.Cancel(), // Cancel during request
            throwException: new TaskCanceledException(null, null, cts.Token)
        );
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = CreatePendingDelivery();

        var result = await service.DeliverAsync(delivery, cts.Token);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.Cancelled);
        result.ErrorMessage.ShouldBe("Delivery cancelled");
    }

    [Fact]
    public async Task GivenNonHttpSubscriber_WhenDelivering_ThenReturnsError()
    {
        var httpClientFactory = CreateMockHttpClientFactory();
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);

        var serviceBusSubscriber = new ServiceBusSubscriberSettings
        {
            Name = "TestServiceBus",
            ConnectionString =
                "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Key;SharedAccessKey=abc",
            Queue = "test-queue",
        };

        var delivery = new PendingDelivery
        {
            Event = CreateTestEvent(),
            Subscriber = serviceBusSubscriber, // Wrong subscriber type
            Topic = CreateTopicSettings(),
            InputSchema = EventSchema.EventGridSchema,
        };

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.NetworkError);
        result.ErrorMessage.ShouldContain("Invalid subscriber type");
    }

    [Fact]
    public async Task GivenMultipleAttempts_WhenDelivering_ThenIncludesDeliveryCountHeader()
    {
        string capturedDeliveryCount = null;
        var httpClientFactory = CreateMockHttpClientFactory(captureHeaders: headers =>
        {
            if (headers.TryGetValues(Constants.AegDeliveryCountHeader, out var values))
            {
                capturedDeliveryCount = values.FirstOrDefault();
            }
        });
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = CreatePendingDelivery();
        delivery.AttemptCount = 5;

        await service.DeliverAsync(delivery, CancellationToken.None);

        capturedDeliveryCount.ShouldBe("5");
    }

    [Fact]
    public async Task GivenNetworkError_WhenDelivering_ThenReturnsNetworkError()
    {
        var httpClientFactory = CreateMockHttpClientFactory(
            throwException: new HttpRequestException("Connection refused")
        );
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = CreatePendingDelivery();

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.NetworkError);
        result.ErrorMessage.ShouldContain("Connection refused");
    }

    [Fact]
    public async Task GivenDnsError_WhenDelivering_ThenReturnsNetworkError()
    {
        var httpClientFactory = CreateMockHttpClientFactory(
            throwException: new HttpRequestException("No such host is known")
        );
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = CreatePendingDelivery();

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.NetworkError);
        result.ErrorMessage.ShouldContain("No such host");
    }

    private static IHttpClientFactory CreateMockHttpClientFactory(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        Exception throwException = null,
        Action responseAction = null,
        Action<HttpRequestHeaders> captureHeaders = null
    )
    {
        var handler = new MockHttpMessageHandler(
            statusCode,
            throwException,
            responseAction,
            captureHeaders
        );
        var httpClient = new HttpClient(handler);

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        return factory;
    }

    private static PendingDelivery CreatePendingDelivery()
    {
        var subscriber = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com/webhook",
            DisableValidation = true,
            ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful,
        };

        return new PendingDelivery
        {
            Event = CreateTestEvent(),
            Subscriber = subscriber,
            Topic = CreateTopicSettings(),
            InputSchema = EventSchema.EventGridSchema,
        };
    }

    private static TopicSettings CreateTopicSettings()
    {
        return new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
        };
    }

    private static SimulatorEvent CreateTestEvent()
    {
        return SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = Guid.NewGuid().ToString(),
                Subject = "test/subject",
                EventType = "Test.EventType",
                EventTime = DateTime.UtcNow.ToString("o"),
                DataVersion = "1.0",
                Data = new { test = "data" },
            }
        );
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Action<HttpRequestHeaders> _captureHeaders;
        private readonly Exception _exception;
        private readonly Action _responseAction;
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(
            HttpStatusCode statusCode,
            Exception exception = null,
            Action responseAction = null,
            Action<HttpRequestHeaders> captureHeaders = null
        )
        {
            _statusCode = statusCode;
            _exception = exception;
            _responseAction = responseAction;
            _captureHeaders = captureHeaders;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            _captureHeaders?.Invoke(request.Headers);
            _responseAction?.Invoke();

            if (_exception != null)
            {
                throw _exception;
            }

            return Task.FromResult(
                new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(""),
                    ReasonPhrase = _statusCode.ToString(),
                }
            );
        }
    }
}
