using System.Net;
using Azure;
using Azure.Core.Pipeline;
using Azure.Messaging.EventGrid;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     Publishes events through the Azure.Messaging.EventGrid SDK. The SDK's HttpClientTransport
///     accepts any HttpMessageHandler, so its requests go to the in-process test server instead of
///     a socket, and no simulator process, certificate or free port is needed. The simulator picks
///     the topic from the request's port, so the URI still names the topic's port.
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class AzureMessagingEventGridSdkTests(IntegrationContextFixture factory)
{
    // ATopicWithATestSubscriber in appsettings.test.json
    private static readonly Uri TopicEndpoint = new("https://localhost:60101/api/events");

    private EventGridPublisherClient CreateClient(string key)
    {
        return new EventGridPublisherClient(
            TopicEndpoint,
            new AzureKeyCredential(key),
            new EventGridPublisherClientOptions
            {
                Transport = new HttpClientTransport(factory.Server.CreateHandler()),
                Retry = { MaxRetries = 0 },
            }
        );
    }

    [Fact]
    public async Task GivenValidEvent_WhenUriContainsNonStandardPort_ThenItShouldBeAccepted()
    {
        var client = CreateClient("TheLocal+DevelopmentKey=");

        var response = await client.SendEventAsync(
            new EventGridEvent("/the/subject", "The.Event.Type", "v1", new { Id = 1, Foo = "Bar" })
        );

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvents_WhenUriContainsNonStandardPort_TheyShouldBeAccepted()
    {
        var client = CreateClient("TheLocal+DevelopmentKey=");

        var events = new[]
        {
            new EventGridEvent(
                "/the/subject1",
                "The.Event.Type1",
                "v1",
                new { Id = 1, Foo = "Bar" }
            ),
            new EventGridEvent(
                "/the/subject2",
                "The.Event.Type2",
                "v1",
                new { Id = 2, Foo = "Baz" }
            ),
        };

        var response = await client.SendEventsAsync(events);

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvent_WhenKeyIsWrong_ThenItShouldNotBeAccepted()
    {
        var client = CreateClient("TheWrongLocal+DevelopmentKey=");

        var exception = await Should.ThrowAsync<RequestFailedException>(async () =>
        {
            await client.SendEventAsync(
                new EventGridEvent(
                    "/the/subject",
                    "The.Event.Type",
                    "v1",
                    new { Id = 1, Foo = "Bar" }
                )
            );
        });

        exception.Status.ShouldBe((int)HttpStatusCode.Unauthorized);
    }
}
