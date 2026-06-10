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
///     End-to-end coverage of the simulator's core promise: a published event
///     that matches a subscriber's filter is delivered to that subscriber, and
///     a non-matching event is not. Outbound HTTP is captured by the fixture's
///     CapturingHttpMessageHandler, so no network access is involved.
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class EventDeliveryFlowTests(IntegrationContextFixture factory)
{
    private const string DeliveryCatcherHost = "delivery-catcher.test";

    private HttpClient CreateTopicClient()
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost:60102"),
            }
        );

        client.DefaultRequestHeaders.Add(Constants.AegSasKeyHeader, "TheLocal+DevelopmentKey=");
        client.DefaultRequestHeaders.Add(
            Constants.AegEventTypeHeader,
            Constants.NotificationEventType
        );

        return client;
    }

    private async Task PublishEvent(string eventId, string eventType)
    {
        var client = CreateTopicClient();
        var testEvent = new EventGridEvent("/test/subject", eventType, "1.0", new { Value = 1 })
        {
            Id = eventId,
        };
        var json = JsonSerializer.Serialize(new[] { testEvent });

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/events", content);

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);
    }

    private async Task<CapturedRequest> WaitForDelivery(string eventId)
    {
        // Delivery is asynchronous (the background service polls every second),
        // so poll the captured requests with a generous timeout.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var delivered = factory.OutboundHttp.Requests.FirstOrDefault(r =>
                r.Url.Contains(DeliveryCatcherHost, StringComparison.OrdinalIgnoreCase)
                && r.Body.Contains(eventId, StringComparison.Ordinal)
            );

            if (delivered != null)
            {
                return delivered;
            }

            await Task.Delay(100);
        }

        throw new ShouldAssertException(
            $"Event {eventId} was not delivered to {DeliveryCatcherHost} within the timeout."
        );
    }

    [Fact]
    public async Task GivenEventMatchingSubscriberFilter_WhenPublished_ThenDeliveredToSubscriber()
    {
        var eventId = Guid.NewGuid().ToString();

        await PublishEvent(eventId, "Deliver.Me");

        var delivered = await WaitForDelivery(eventId);

        delivered.Method.ShouldBe("POST");
        delivered.Url.ShouldStartWith("https://delivery-catcher.test/events");
        delivered.Headers[Constants.AegEventTypeHeader].ShouldBe(Constants.NotificationEventType);
        delivered.Headers[Constants.AegSubscriptionNameHeader].ShouldBe("DELIVERYCATCHER");
        delivered.Headers[Constants.AegDeliveryCountHeader].ShouldBe("1");

        // The delivered payload is the event in EventGrid schema (array form)
        using var doc = JsonDocument.Parse(delivered.Body);
        doc.RootElement.GetArrayLength().ShouldBe(1);
        doc.RootElement[0].GetProperty("id").GetString().ShouldBe(eventId);
        doc.RootElement[0].GetProperty("eventType").GetString().ShouldBe("Deliver.Me");
    }

    [Fact]
    public async Task GivenEventNotMatchingSubscriberFilter_WhenPublished_ThenNotDelivered()
    {
        var filteredOutId = Guid.NewGuid().ToString();
        var matchingId = Guid.NewGuid().ToString();

        // The non-matching event is accepted by the topic but filtered out at
        // enqueue time; the matching event published afterwards acts as a
        // barrier - once it has been delivered, the filtered event has had
        // every opportunity to appear.
        await PublishEvent(filteredOutId, "Ignore.Me");
        await PublishEvent(matchingId, "Deliver.Me");

        await WaitForDelivery(matchingId);

        // Scope to the filtered subscriber: the unfiltered handshaker subscribers
        // on the same topic legitimately receive the event.
        factory
            .OutboundHttp.Requests.Any(r =>
                r.Url.Contains(DeliveryCatcherHost, StringComparison.OrdinalIgnoreCase)
                && r.Body.Contains(filteredOutId, StringComparison.Ordinal)
            )
            .ShouldBeFalse("the filtered-out event must never reach the filtered subscriber");
    }
}
