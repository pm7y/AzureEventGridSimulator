using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     Pins the Azure-parity headers on OPTIONS and 405 responses from /api/events. Against Azure
///     the Postman parity suite only checks these headers are present (Azure's values haven't been
///     recorded), and it compares Allow in any order, so the exact values, including the order of
///     the Allow methods, are pinned here.
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class ParityHeaderTests(IntegrationContextFixture factory)
{
    // ATopicWithATestSubscriber in appsettings.test.json (no inputSchema)
    private const int EventGridTopicPort = 60101;

    // CloudEventsTopic in appsettings.test.json (inputSchema CloudEventV1_0)
    private const int CloudEventsTopicPort = 60104;

    private HttpClient CreateClient(int port)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri($"https://localhost:{port}"),
            }
        );
    }

    private async Task<HttpResponseMessage> SendAsync(int port, HttpMethod method, string path)
    {
        var client = CreateClient(port);
        using var request = new HttpRequestMessage(method, path);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task GivenOptionsToApiEvents_WhenSent_ThenParityHeadersAreReturned()
    {
        using var response = await SendAsync(EventGridTopicPort, HttpMethod.Options, "/api/events");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.Allow.ShouldBe(["POST", "OPTIONS"]);
        response.Headers.GetValues("api-supported-versions").ShouldBe(["2018-01-01"]);
        response.Headers.GetValues("aeg-input-event-schema").ShouldBe(["EventGridEvent"]);
        Guid.TryParse(response.Headers.GetValues("x-ms-request-id").Single(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GivenOptionsToApiEvents_WhenTopicInputSchemaIsCloudEvents_ThenInputEventSchemaIsCloudEventV10()
    {
        using var response = await SendAsync(
            CloudEventsTopicPort,
            HttpMethod.Options,
            "/api/events"
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("aeg-input-event-schema").ShouldBe(["CloudEventV10"]);
    }

    [Theory]
    [InlineData("GET", "/api/events")]
    [InlineData("DELETE", "/api/events/")]
    public async Task GivenNonPostMethodToApiEvents_WhenSent_ThenMethodNotAllowedWithAllowHeader(
        string method,
        string path
    )
    {
        using var response = await SendAsync(EventGridTopicPort, new HttpMethod(method), path);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.Allow.ShouldBe(["OPTIONS", "POST"]);
    }
}
