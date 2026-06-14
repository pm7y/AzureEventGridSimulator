using System.Diagnostics;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Messaging.EventGrid;
using Azure.ResourceManager;
using Azure.ResourceManager.EventGrid;
using Azure.ResourceManager.EventGrid.Models;
using AzureEventGridSimulator.Tests.Helpers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
///     End-to-end proof that a subscription created at runtime through the real ARM client actually
///     participates in delivery: an event published to the topic is delivered to the runtime-created
///     webhook, and delivery stops once the subscription is deleted.
/// </summary>
[Collection(nameof(ActualSimulatorFixtureCollection))]
[Trait("Category", "integration-actual")]
public class RuntimeSubscriptionDeliveryTest
{
    private const string TopicName = "RuntimeDeliveryTopic";
    private const int TopicPort = 60104;
    private const int SinkPort = 60110;
    private const string EventType = "Delivery.RuntimeTest";
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";

    private static readonly string TopicResourceId =
        $"/subscriptions/{SubscriptionId}/resourceGroups/aegs"
        + $"/providers/Microsoft.EventGrid/topics/{TopicName}";

    [Fact]
    public async Task GivenRuntimeWebHookSubscription_WhenEventPublished_ThenItIsDeliveredUntilDeleted()
    {
        await using var sink = WebhookSink.Start(SinkPort);
        var subscriptions = CreateSubscriptionCollection();
        const string name = "delivery-sub";

        var data = new EventGridSubscriptionData
        {
            Destination = new WebHookEventSubscriptionDestination
            {
                Endpoint = new Uri(sink.Endpoint),
            },
            Filter = new EventSubscriptionFilter(),
        };
        data.Filter.IncludedEventTypes.Add(EventType);

        await subscriptions.CreateOrUpdateAsync(WaitUntil.Completed, name, data);

        var publisher = CreatePublisherClient();
        await publisher.SendEventAsync(
            new EventGridEvent("/runtime", EventType, "v1", new { hello = "world" })
        );

        await WaitForAsync(
            () => sink.ReceivedEvents.Count >= 1,
            TimeSpan.FromSeconds(15),
            "event to be delivered to the runtime-created subscription"
        );

        // Delete the subscription, then publish again; the event must not be delivered.
        var subscription = await subscriptions.GetAsync(name);
        await subscription.Value.DeleteAsync(WaitUntil.Completed);

        await publisher.SendEventAsync(
            new EventGridEvent("/runtime", EventType, "v1", new { hello = "again" })
        );
        await Task.Delay(TimeSpan.FromSeconds(3));

        sink.ReceivedEvents.Count.ShouldBe(1);
    }

    private static TopicEventSubscriptionCollection CreateSubscriptionCollection()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        var options = new ArmClientOptions
        {
            Environment = new ArmEnvironment(
                new Uri("https://localhost:60100/"),
                "https://management.azure.com"
            ),
            Transport = new HttpClientTransport(handler),
        };
        options.Retry.MaxRetries = 0;

        var armClient = new ArmClient(new FakeTokenCredential(), SubscriptionId, options);
        return armClient
            .GetEventGridTopicResource(new ResourceIdentifier(TopicResourceId))
            .GetTopicEventSubscriptions();
    }

    private static EventGridPublisherClient CreatePublisherClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        var options = new EventGridPublisherClientOptions
        {
            Transport = new HttpClientTransport(handler),
            Retry =
            {
                Mode = RetryMode.Fixed,
                MaxRetries = 0,
                NetworkTimeout = TimeSpan.FromSeconds(5),
            },
        };

        return new EventGridPublisherClient(
            new Uri($"https://localhost:{TopicPort}/api/events"),
            new AzureKeyCredential("TheLocal+DevelopmentKey="),
            options
        );
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout, string because)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Timed out waiting for {because}.");
    }
}
