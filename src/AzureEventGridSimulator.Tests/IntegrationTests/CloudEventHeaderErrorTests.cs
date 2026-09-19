using System.Net;
using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Domain.Services.Routing;
using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Middleware;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     End-to-end tests for CloudEvents parse errors. A missing or empty required ce-* header in
///     binary mode is reported with the InvalidCloudEventHeader detail code, and every parse error
///     message carries the report suffix exactly once. The fixture runs on the real clock, so its
///     tests match the suffix only up to the request ID.
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class CloudEventHeaderErrorTests(IntegrationContextFixture factory)
{
    // CloudEventsTopic in appsettings.test.json, configured with inputSchema CloudEventV1_0
    private const int CloudEventsTopicPort = 60104;

    private const string RequiredAttributesHint =
        "Please check required attributes at https://github.com/cloudevents/spec/blob/v1.0/spec.md#required-attributes";

    private HttpClient CreateClient()
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri($"https://localhost:{CloudEventsTopicPort}"),
            }
        );
        client.DefaultRequestHeaders.Add(Constants.AegSasKeyHeader, "TheLocal+DevelopmentKey=");
        return client;
    }

    private static HttpRequestMessage CreateBinaryModeRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/events")
        {
            Content = new StringContent("""{ "value": 1 }""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(Constants.CeSpecVersionHeader, "1.0");
        request.Headers.Add(Constants.CeTypeHeader, "com.example.test");
        request.Headers.Add(Constants.CeSourceHeader, "/test/source");
        request.Headers.Add(Constants.CeIdHeader, "binary-id-1");
        return request;
    }

    private static async Task<JsonElement> ReadErrorAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").Clone();
    }

    private static string ShouldBeBadRequestWith(JsonElement error, string detailCode)
    {
        error.GetProperty("code").GetString().ShouldBe("BadRequest");
        error.GetProperty("details")[0].GetProperty("code").GetString().ShouldBe(detailCode);
        return error.GetProperty("message").GetString().ShouldNotBeNull();
    }

    private static void ShouldHaveOneReportSuffix(string message)
    {
        message.Split("Report '").Length.ShouldBe(2, $"one report suffix expected in: {message}");
        message.ShouldEndWith("to our forums for assistance or raise a support ticket.");
    }

    [Theory]
    [InlineData(Constants.CeSpecVersionHeader)]
    [InlineData(Constants.CeTypeHeader)]
    [InlineData(Constants.CeSourceHeader)]
    [InlineData(Constants.CeIdHeader)]
    public async Task GivenBinaryModeEventWithoutARequiredHeader_WhenPublished_ThenRejectedWithInvalidCloudEventHeader(
        string header
    )
    {
        var client = CreateClient();
        using var request = CreateBinaryModeRequest();
        request.Headers.Remove(header);

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var message = ShouldBeBadRequestWith(
            await ReadErrorAsync(response),
            ErrorDetailCodes.InvalidCloudEventHeader
        );
        message.ShouldStartWith(
            $"{header} header is missing for the cloud event. {RequiredAttributesHint} Report '"
        );
        ShouldHaveOneReportSuffix(message);
    }

    // An empty header value doesn't survive the in-process client/server round trip (it arrives
    // as missing), so these tests run the middleware directly on a DefaultHttpContext. The fake
    // clock also pins the whole report suffix.
    [Theory]
    [InlineData(Constants.CeSpecVersionHeader)]
    [InlineData(Constants.CeTypeHeader)]
    [InlineData(Constants.CeSourceHeader)]
    [InlineData(Constants.CeIdHeader)]
    public async Task GivenBinaryModeEventWithAnEmptyRequiredHeader_WhenPublished_ThenRejectedWithInvalidCloudEventHeader(
        string header
    )
    {
        var now = new DateTimeOffset(2025, 1, 5, 14, 7, 9, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var settings = new SimulatorSettings
        {
            Topics = [new TopicSettings { Name = "HeaderTopic", Port = CloudEventsTopicPort }],
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
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<TimeProvider>(timeProvider)
                .BuildServiceProvider(),
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/events";
        context.Request.Host = new HostString("localhost", CloudEventsTopicPort);
        context.Request.ContentType = "application/json";
        context.Request.Headers[Constants.CeSpecVersionHeader] = "1.0";
        context.Request.Headers[Constants.CeTypeHeader] = "com.example.test";
        context.Request.Headers[Constants.CeSourceHeader] = "/test/source";
        context.Request.Headers[Constants.CeIdHeader] = "binary-id-1";
        context.Request.Headers[header] = "";
        context.Request.Body = new MemoryStream("""{ "value": 1 }"""u8.ToArray());
        context.Response.Body = new MemoryStream();

        await new EventGridMiddleware(_ => Task.CompletedTask).InvokeAsync(
            context,
            settings,
            new RequestRouter(settings),
            new SasKeyValidator(timeProvider, NullLogger<SasKeyValidator>.Instance),
            orchestrator,
            Substitute.For<IEventHistoryService>(),
            timeProvider,
            NullLogger<EventGridMiddleware>.Instance
        );

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var message = ShouldBeBadRequestWith(
            document.RootElement.GetProperty("error"),
            ErrorDetailCodes.InvalidCloudEventHeader
        );
        message.ShouldBe(
            $"{header} header is empty for the cloud event. {RequiredAttributesHint}"
                + $" Report '{context.Response.Headers["x-ms-request-id"]}:1:1/5/2025 2:07:09 PM (UTC)'"
                + " to our forums for assistance or raise a support ticket."
        );
    }

    [Fact]
    public async Task GivenStructuredEventWithoutType_WhenPublished_ThenRejectedNamingEventTypeWithOneReportSuffix()
    {
        var client = CreateClient();
        using var content = new StringContent(
            """{ "specversion": "1.0", "source": "/test/source", "id": "structured-id-1" }""",
            Encoding.UTF8,
            Constants.CloudEventsContentTypeBase
        );

        var response = await client.PostAsync("/api/events", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var message = ShouldBeBadRequestWith(
            await ReadErrorAsync(response),
            ErrorDetailCodes.InputJsonInvalid
        );
        message.ShouldStartWith(
            "This resource is configured for 'CloudEventV10' schema and requires 'eventType' property to be set. Report '"
        );
        ShouldHaveOneReportSuffix(message);
    }

    [Fact]
    public async Task GivenArrayPostedAsApplicationJson_WhenPublished_ThenRejectedAsStartArrayWithOneReportSuffix()
    {
        var client = CreateClient();
        using var content = new StringContent(
            """[{ "specversion": "1.0", "type": "com.example.test", "source": "/test/source", "id": "array-id-1" }]""",
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/api/events", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var message = ShouldBeBadRequestWith(
            await ReadErrorAsync(response),
            ErrorDetailCodes.InputJsonInvalid
        );
        message.ShouldStartWith(
            "This resource is configured to receive event in 'CloudEventV10' schema. "
                + "The JSON received does not conform to the expected schema. "
                + "Token Expected: StartObject, Actual Token Received: StartArray. Report '"
        );
        ShouldHaveOneReportSuffix(message);
    }
}
