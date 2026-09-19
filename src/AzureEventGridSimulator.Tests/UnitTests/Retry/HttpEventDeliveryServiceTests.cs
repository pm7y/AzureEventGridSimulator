using System.Net;
using System.Net.Http.Headers;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Retry;

[Trait("Category", "unit")]
public class HttpEventDeliveryServiceTests : IDisposable
{
    private readonly EventSchemaFormatterFactory _formatterFactory;
    private readonly List<HttpClient> _httpClients = [];
    private readonly ILogger<HttpEventDeliveryService> _logger;

    public HttpEventDeliveryServiceTests()
    {
        _logger = Substitute.For<ILogger<HttpEventDeliveryService>>();
        _formatterFactory = new EventSchemaFormatterFactory(
            new EventGridSchemaFormatter(TimeProvider.System),
            new CloudEventSchemaFormatter()
        );
    }

    public void Dispose()
    {
        foreach (var client in _httpClients)
        {
            client.Dispose();
        }

        _httpClients.Clear();
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
        var delivery = TestHelpers.CreatePendingDelivery();

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
        var delivery = TestHelpers.CreatePendingDelivery();

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.HttpError);
        result.HttpStatusCode.ShouldBe((int)statusCode);
    }

    [Fact]
    public async Task GivenTimeout_WhenDelivering_ThenReturnsTimeout()
    {
        // HTTP client timeout throws TaskCanceledException with a different cancellation token
        using var timeoutCts = new CancellationTokenSource();
        var timeoutException = new TaskCanceledException(
            "The request was cancelled due to timeout",
            new TimeoutException("The operation timed out"),
            timeoutCts.Token
        );
        var httpClientFactory = CreateMockHttpClientFactory(throwException: timeoutException);
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = TestHelpers.CreatePendingDelivery();

        // Pass a DIFFERENT cancellation token than the one in the exception
        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.Timeout);
        result.ErrorMessage.ShouldBe("Request timed out");
    }

    [Fact]
    public async Task GivenCancellation_WhenDelivering_ThenReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        var httpClientFactory = CreateMockHttpClientFactory(
            responseAction: () => cts.Cancel(), // Cancel during request
            throwException: new TaskCanceledException(null, null, cts.Token)
        );
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = TestHelpers.CreatePendingDelivery();

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

        // Wrong subscriber type
        var delivery = TestHelpers.CreatePendingDelivery(serviceBusSubscriber);

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.NetworkError);
        result.ErrorMessage.ShouldNotBeNullAnd().ShouldContain("Invalid subscriber type");
    }

    [Fact]
    public async Task GivenHttpSubscriber_WhenDelivering_ThenUsesTheSimulatorsNamedHttpClient()
    {
        // The named client carries the timeout and the optional certificate bypass
        var httpClientFactory = CreateMockHttpClientFactory();
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);

        await service.DeliverAsync(TestHelpers.CreatePendingDelivery(), CancellationToken.None);

        httpClientFactory.Received(1).CreateClient(Constants.HttpClientName);
    }

    [Fact]
    public async Task GivenMultipleAttempts_WhenDelivering_ThenIncludesDeliveryCountHeader()
    {
        string? capturedDeliveryCount = null;
        var httpClientFactory = CreateMockHttpClientFactory(captureHeaders: headers =>
        {
            if (headers.TryGetValues(Constants.AegDeliveryCountHeader, out var values))
            {
                capturedDeliveryCount = values.FirstOrDefault();
            }
        });
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = TestHelpers.CreatePendingDelivery();
        delivery.AttemptCount = 5;

        await service.DeliverAsync(delivery, CancellationToken.None);

        // AttemptCount is 5, delivery count header should be AttemptCount + 1 = 6
        capturedDeliveryCount.ShouldBe("6");
    }

    [Fact]
    public async Task GivenNetworkError_WhenDelivering_ThenReturnsNetworkError()
    {
        var httpClientFactory = CreateMockHttpClientFactory(
            throwException: new HttpRequestException("Connection refused")
        );
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = TestHelpers.CreatePendingDelivery();

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.NetworkError);
        result.ErrorMessage.ShouldNotBeNullAnd().ShouldContain("Connection refused");
    }

    [Fact]
    public async Task GivenDnsError_WhenDelivering_ThenReturnsNetworkError()
    {
        var httpClientFactory = CreateMockHttpClientFactory(
            throwException: new HttpRequestException("No such host is known")
        );
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var delivery = TestHelpers.CreatePendingDelivery();

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Outcome.ShouldBe(DeliveryOutcome.NetworkError);
        result.ErrorMessage.ShouldNotBeNullAnd().ShouldContain("No such host");
    }

    // The subscriber's deliverySchema wins, then the topic's outputSchema, then the input schema
    [Theory]
    [InlineData(null, null, EventSchema.EventGridSchema, EventSchema.EventGridSchema)]
    [InlineData(null, null, EventSchema.CloudEventV1_0, EventSchema.CloudEventV1_0)]
    [InlineData(
        null,
        EventSchema.CloudEventV1_0,
        EventSchema.EventGridSchema,
        EventSchema.CloudEventV1_0
    )]
    [InlineData(
        null,
        EventSchema.EventGridSchema,
        EventSchema.CloudEventV1_0,
        EventSchema.EventGridSchema
    )]
    [InlineData(
        EventSchema.CloudEventV1_0,
        EventSchema.EventGridSchema,
        EventSchema.EventGridSchema,
        EventSchema.CloudEventV1_0
    )]
    [InlineData(
        EventSchema.EventGridSchema,
        EventSchema.CloudEventV1_0,
        EventSchema.CloudEventV1_0,
        EventSchema.EventGridSchema
    )]
    public async Task GivenSchemaSettings_WhenDelivering_ThenTheMostSpecificSchemaIsUsed(
        EventSchema? subscriberDeliverySchema,
        EventSchema? topicOutputSchema,
        EventSchema inputSchema,
        EventSchema expectedSchema
    )
    {
        // Captured inside the handler, because the request is disposed once delivery returns
        string? mediaType = null;
        var hasDataVersionHeader = false;
        var httpClientFactory = CreateMockHttpClientFactory(captureRequest: request =>
        {
            mediaType = request.Content?.Headers.ContentType?.MediaType;
            hasDataVersionHeader = request.Headers.Contains(Constants.AegDataVersionHeader);
        });
        var service = new HttpEventDeliveryService(httpClientFactory, _formatterFactory, _logger);
        var subscriber = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com/webhook",
            DisableValidation = true,
            DeliverySchema = subscriberDeliverySchema,
        };
        var topic = new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TheLocal+DevelopmentKey=",
            OutputSchema = topicOutputSchema,
        };
        var delivery = TestHelpers.CreatePendingDelivery(
            subscriber,
            topic: topic,
            inputSchema: inputSchema
        );

        var result = await service.DeliverAsync(delivery, CancellationToken.None);

        result.Success.ShouldBeTrue();
        if (expectedSchema == EventSchema.CloudEventV1_0)
        {
            mediaType.ShouldBe("application/cloudevents-batch+json");
            hasDataVersionHeader.ShouldBeFalse();
        }
        else
        {
            mediaType.ShouldBe("application/json");
            hasDataVersionHeader.ShouldBeTrue();
        }
    }

    private IHttpClientFactory CreateMockHttpClientFactory(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        Exception? throwException = null,
        Action? responseAction = null,
        Action<HttpRequestHeaders>? captureHeaders = null,
        Action<HttpRequestMessage>? captureRequest = null
    )
    {
        var handler = new MockHttpMessageHandler(
            statusCode,
            throwException,
            responseAction,
            captureHeaders,
            captureRequest
        );
        var httpClient = new HttpClient(handler);
        _httpClients.Add(httpClient);

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        return factory;
    }

    private class MockHttpMessageHandler(
        HttpStatusCode statusCode,
        Exception? exception = null,
        Action? responseAction = null,
        Action<HttpRequestHeaders>? captureHeaders = null,
        Action<HttpRequestMessage>? captureRequest = null
    ) : HttpMessageHandler
    {
        private readonly List<HttpResponseMessage> _responses = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            captureHeaders?.Invoke(request.Headers);
            captureRequest?.Invoke(request);
            responseAction?.Invoke();

            if (exception != null)
            {
                throw exception;
            }

            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(""),
                ReasonPhrase = statusCode.ToString(),
            };
            _responses.Add(response);

            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var response in _responses)
                {
                    response.Dispose();
                }

                _responses.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
