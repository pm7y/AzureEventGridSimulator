using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.ResourceManager;
using Azure.ResourceManager.EventGrid;
using Azure.ResourceManager.EventGrid.Models;
using AzureEventGridSimulator.Tests.Helpers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
///     Verifies that the real Azure.ResourceManager.EventGrid (ARM management) client can manage
///     event subscriptions against the simulator at runtime, simply by repointing the client at the
///     simulator's management port. This is the trust anchor: the same NuGet library the Backend
///     uses must work unmodified against the simulator.
/// </summary>
[Collection(nameof(ActualSimulatorFixtureCollection))]
[Trait("Category", "integration-actual")]
public class AzureResourceManagerEventGridTest
{
    private const string ManagementEndpoint = "https://localhost:60100/";
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";

    private const string TopicResourceId =
        $"/subscriptions/{SubscriptionId}/resourceGroups/aegs/providers/Microsoft.EventGrid/topics/ManagementTopic";

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
                new Uri(ManagementEndpoint),
                "https://management.azure.com"
            ),
            Transport = new HttpClientTransport(handler),
        };
        options.Retry.MaxRetries = 0;

        var armClient = new ArmClient(new FakeTokenCredential(), SubscriptionId, options);
        var topic = armClient.GetEventGridTopicResource(new ResourceIdentifier(TopicResourceId));
        return topic.GetTopicEventSubscriptions();
    }

    private static EventGridSubscriptionData WebHookSubscription(string endpoint, string eventType)
    {
        var data = new EventGridSubscriptionData
        {
            Destination = new WebHookEventSubscriptionDestination { Endpoint = new Uri(endpoint) },
            Filter = new EventSubscriptionFilter(),
        };
        data.Filter.IncludedEventTypes.Add(eventType);
        return data;
    }

    [Fact]
    public async Task GivenWebHookSubscription_WhenCreated_ThenItCanBeFetchedWithItsProperties()
    {
        var subscriptions = CreateSubscriptionCollection();
        const string name = "created-sub";
        const string endpoint = "https://runtime-sink.test/created";

        var created = await subscriptions.CreateOrUpdateAsync(
            WaitUntil.Completed,
            name,
            WebHookSubscription(endpoint, "Runtime.Created")
        );

        created.Value.Data.Name.ShouldBe(name);

        var fetched = await subscriptions.GetAsync(name);
        var destination =
            fetched.Value.Data.Destination.ShouldBeOfType<WebHookEventSubscriptionDestination>();
        destination.Endpoint.ShouldBe(new Uri(endpoint));
        fetched.Value.Data.Filter.IncludedEventTypes.ShouldContain("Runtime.Created");
    }

    [Fact]
    public async Task GivenCreatedSubscription_WhenListing_ThenItIsReturned()
    {
        var subscriptions = CreateSubscriptionCollection();
        const string name = "listed-sub";

        await subscriptions.CreateOrUpdateAsync(
            WaitUntil.Completed,
            name,
            WebHookSubscription("https://runtime-sink.test/listed", "Runtime.Listed")
        );

        var names = new List<string>();
        await foreach (var subscription in subscriptions.GetAllAsync())
        {
            names.Add(subscription.Data.Name);
        }

        names.ShouldContain(name);
    }

    [Fact]
    public async Task GivenCreatedSubscription_WhenDeleted_ThenItNoLongerExists()
    {
        var subscriptions = CreateSubscriptionCollection();
        const string name = "deleted-sub";

        await subscriptions.CreateOrUpdateAsync(
            WaitUntil.Completed,
            name,
            WebHookSubscription("https://runtime-sink.test/deleted", "Runtime.Deleted")
        );

        var subscription = await subscriptions.GetAsync(name);
        await subscription.Value.DeleteAsync(WaitUntil.Completed);

        var exists = await subscriptions.GetIfExistsAsync(name);
        exists.HasValue.ShouldBeFalse();
    }
}
