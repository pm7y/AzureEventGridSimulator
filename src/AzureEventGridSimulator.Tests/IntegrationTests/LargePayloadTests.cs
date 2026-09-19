using System.Net;
using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     End-to-end tests for request bodies that are large but within the size limits. The request
///     body is read once, straight from the server's stream, which can't seek.
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class LargePayloadTests(IntegrationContextFixture factory)
{
    // Default limits: 1,049,600 bytes per event and 1,536,000 bytes overall
    private const int MaximumEventSizeInBytes = 1_049_600;
    private const int MaximumOverallMessageSizeInBytes = 1_536_000;

    [Fact]
    public async Task GivenBatchOfAbout100KB_WhenPublished_ThenAccepted()
    {
        // ATopicWithATestSubscriber: its subscribers are disabled, so nothing is delivered
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:60101"),
            }
        );
        client.DefaultRequestHeaders.Add(Constants.AegSasKeyHeader, "TheLocal+DevelopmentKey=");

        var events = Enumerable
            .Range(1, 10)
            .Select(i => new
            {
                id = $"large-payload-{i}",
                subject = "/test/large",
                eventType = "Large.Payload",
                eventTime = "2025-01-15T10:30:00Z",
                dataVersion = "1.0",
                data = new { index = i, padding = new string('x', 10_000) },
            })
            .ToArray();
        var json = JsonSerializer.Serialize(events);
        var bodySize = Encoding.UTF8.GetByteCount(json);

        // Big enough that a buffered body would have spilled past its 30 KB in-memory threshold
        bodySize.ShouldBeInRange(100_000, 110_000);
        bodySize.ShouldBeLessThan(MaximumOverallMessageSizeInBytes);
        events
            .Max(e => JsonSerializer.SerializeToUtf8Bytes(e).Length)
            .ShouldBeLessThan(MaximumEventSizeInBytes);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/events", content);

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);
    }
}
