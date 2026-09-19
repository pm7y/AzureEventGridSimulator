using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Messaging.EventGrid;
using AzureEventGridSimulator.Controllers;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Domain.Services.Routing;
using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Middleware;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     End-to-end tests for the request ingestion pipeline: routing semantics
///     (method/path handling), size-limit enforcement and the Azure error envelope.
///     The fixture runs on the real clock, so only the presence of the report suffix is checked
///     here; its exact format is pinned in HttpContextExtensionsTests.
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class RequestPipelineTests(IntegrationContextFixture factory)
{
    private HttpClient CreateClient(bool withSasKey = true, int port = 60101)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri($"https://localhost:{port}"),
            }
        );

        if (withSasKey)
        {
            client.DefaultRequestHeaders.Add(Constants.AegSasKeyHeader, "TheLocal+DevelopmentKey=");
            client.DefaultRequestHeaders.Add(
                Constants.AegEventTypeHeader,
                Constants.NotificationEventType
            );
        }

        return client;
    }

    private static async Task<JsonElement> ReadErrorAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").Clone();
    }

    private static void ShouldHaveErrorCodes(JsonElement error, string code, string detailCode)
    {
        error.GetProperty("code").GetString().ShouldBe(code);
        error.GetProperty("details")[0].GetProperty("code").GetString().ShouldBe(detailCode);
    }

    private static StringContent CreateValidEventContent()
    {
        var testEvent = new EventGridEvent("subject", "eventType", "1.0", new { Blah = 1 });
        var json = JsonSerializer.Serialize(new[] { testEvent });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    [Theory]
    [InlineData("/API/EVENTS")]
    [InlineData("/api/events/")]
    public async Task GivenValidEvent_WhenPostedToCaseInsensitiveOrTrailingSlashPath_ThenAccepted(
        string path
    )
    {
        var client = CreateClient();

        using var content = CreateValidEventContent();
        var response = await client.PostAsync(path, content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvent_WhenPostedToDisabledTopicPort_ThenNotFound()
    {
        // DisabledTopic (port 60103) is configured with "disabled": true
        var client = CreateClient(port: 60103);

        using var content = CreateValidEventContent();
        var response = await client.PostAsync("/api/events", content);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var error = await ReadErrorAsync(response);
        ShouldHaveErrorCodes(error, "NotFound", ErrorDetailCodes.ResourceNotFound);
        error.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("Report '");
    }

    [Fact]
    public async Task GivenOversizedPayload_WhenPosted_ThenRejectedWith413()
    {
        var client = CreateClient();

        // Default overall limit is 1,536,000 bytes; this body is comfortably over it
        using var content = new StringContent(
            new string('x', 1_600_000),
            Encoding.UTF8,
            "application/json"
        );
        var response = await client.PostAsync("/api/events", content);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        var error = await ReadErrorAsync(response);
        ShouldHaveErrorCodes(error, "RequestEntityTooLarge", ErrorDetailCodes.PayloadTooLarge);

        // Azure doesn't add the report suffix to a 413
        error.GetProperty("message").GetString().ShouldNotBeNull().ShouldNotContain("Report '");
    }

    [Fact]
    public async Task GivenGetToApiEvents_WhenSent_ThenMethodNotAllowed()
    {
        var client = CreateClient(withSasKey: false);

        var response = await client.GetAsync("/api/events");

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        var error = await ReadErrorAsync(response);
        ShouldHaveErrorCodes(error, "MethodNotAllowed", ErrorDetailCodes.MethodNotAllowed);
        error.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("Report '");
    }

    [Fact]
    public async Task GivenUnknownPath_WhenRequested_ThenNotFound()
    {
        var client = CreateClient(withSasKey: false);

        var response = await client.GetAsync("/some/unknown/path");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var error = await ReadErrorAsync(response);
        ShouldHaveErrorCodes(error, "NotFound", ErrorDetailCodes.ResourceNotFound);
        error
            .GetProperty("message")
            .GetString()
            .ShouldNotBeNull()
            .ShouldStartWith(
                "No HTTP resource was found that matches the request URI 'https%3A%2F%2Flocalhost%3A60101%2Fsome%2Funknown%2Fpath'. Report '"
            );
    }

    [Fact]
    public async Task GivenEmptyJsonArray_WhenPosted_ThenBadRequest()
    {
        var client = CreateClient();

        using var content = new StringContent("[]", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/events", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await ReadErrorAsync(response);
        ShouldHaveErrorCodes(error, "BadRequest", ErrorDetailCodes.InputJsonInvalid);
        error.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("Report '");
    }

    [Fact]
    public async Task GivenRejectedPublish_WhenMiddlewareRecordsIt_ThenRejectedAtComesFromTimeProvider()
    {
        // The fixture runs on the real clock, so the middleware is invoked directly here with a
        // fake one
        var now = new DateTimeOffset(2025, 1, 5, 14, 7, 9, TimeSpan.Zero);
        var settings = new SimulatorSettings
        {
            Topics = [new TopicSettings { Name = "ClockTopic", Port = 60101 }],
        };
        var detector = new EventSchemaDetector();
        var orchestrator = new EventValidationOrchestrator(
            detector,
            new EventSchemaParserFactory(
                new EventGridSchemaParser(),
                new CloudEventSchemaParser(detector)
            ),
            new RequestBodyValidator(NullLogger<RequestBodyValidator>.Instance),
            new ContentTypeValidator(NullLogger<ContentTypeValidator>.Instance),
            settings,
            NullLogger<EventValidationOrchestrator>.Instance
        );
        var timeProvider = new FakeTimeProvider(now);
        var eventHistory = Substitute.For<IEventHistoryService>();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/events";
        context.Request.Host = new HostString("localhost", 60101);
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream("[]"u8.ToArray());
        context.Response.Body = new MemoryStream();
        var middleware = new EventGridMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(
            context,
            settings,
            new RequestRouter(settings),
            new SasKeyValidator(timeProvider, NullLogger<SasKeyValidator>.Instance),
            orchestrator,
            eventHistory,
            timeProvider,
            NullLogger<EventGridMiddleware>.Instance
        );

        context.Response.StatusCode.ShouldBe(400);
        eventHistory
            .Received(1)
            .RecordEventRejected(
                Arg.Is<RejectedEventRecord>(r =>
                    r.RejectedAt == now && r.TopicName == "ClockTopic" && r.RawBody == "[]"
                )
            );
    }

    // TestServer leaves Connection.LocalPort at 0, so through the fixture the socket port and the
    // Host header's port are always the same. These tests call SubscriptionValidationController
    // directly with the two set to different ports.
    private static SubscriptionValidationController CreateValidationController(
        IMediator mediator,
        int localPort,
        int hostPort
    )
    {
        var settings = new SimulatorSettings
        {
            Topics =
            [
                new TopicSettings { Name = "HostHeaderTopic", Port = 60101 },
                new TopicSettings { Name = "ConnectionTopic", Port = 60102 },
                new TopicSettings
                {
                    Name = "DisabledTopic",
                    Port = 60103,
                    Disabled = true,
                },
            ],
        };
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/validate";
        context.Request.Host = new HostString("localhost", hostPort);
        context.Connection.LocalPort = localPort;
        context.Response.Body = new MemoryStream();

        return new SubscriptionValidationController(new RequestRouter(settings), mediator)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
    }

    [Fact]
    public async Task GivenHostHeaderNamingAnotherTopic_WhenSubscriptionIsValidated_ThenTopicIsSelectedByConnectionPort()
    {
        var mediator = Substitute.For<IMediator>();
        mediator
            .Send(Arg.Any<ValidateSubscriptionCommand>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var controller = CreateValidationController(mediator, localPort: 60102, hostPort: 60101);
        var id = Guid.NewGuid();

        var result = await controller.Get(id);

        result.ShouldBeOfType<OkObjectResult>();
        await mediator
            .Received(1)
            .Send(
                Arg.Is<ValidateSubscriptionCommand>(c =>
                    c.Topic.Name == "ConnectionTopic" && c.ValidationCode == id
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task GivenConnectionOnDisabledTopicPort_WhenSubscriptionIsValidated_ThenNotFoundEvenThoughHostHeaderNamesEnabledTopic()
    {
        var mediator = Substitute.For<IMediator>();
        var controller = CreateValidationController(mediator, localPort: 60103, hostPort: 60101);

        var result = await controller.Get(Guid.NewGuid());

        result.ShouldBeOfType<EmptyResult>();
        var response = controller.HttpContext.Response;
        response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(response.Body);
        ShouldHaveErrorCodes(
            document.RootElement.GetProperty("error"),
            "NotFound",
            ErrorDetailCodes.ResourceNotFound
        );
        mediator.ReceivedCalls().ShouldBeEmpty();
    }
}
