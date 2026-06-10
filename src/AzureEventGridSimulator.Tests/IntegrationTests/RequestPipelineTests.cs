using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Messaging.EventGrid;
using AzureEventGridSimulator.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     End-to-end tests for the request ingestion pipeline: routing semantics
///     (method/path handling) and size-limit enforcement.
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class RequestPipelineTests(IntegrationContextFixture factory)
{
    private HttpClient CreateClient(bool withSasKey = true)
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:60101"),
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
    }

    [Fact]
    public async Task GivenGetToApiEvents_WhenSent_ThenMethodNotAllowed()
    {
        var client = CreateClient(withSasKey: false);

        var response = await client.GetAsync("/api/events");

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task GivenUnknownPath_WhenRequested_ThenNotFound()
    {
        var client = CreateClient(withSasKey: false);

        var response = await client.GetAsync("/some/unknown/path");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenEmptyJsonArray_WhenPosted_ThenBadRequest()
    {
        var client = CreateClient();

        using var content = new StringContent("[]", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/events", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
