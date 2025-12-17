using System.Net;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Messaging.EventGrid;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
/// Simple tests to check that we can send an event via Azure.Messaging.EventGrid library.
/// NOTE: These tests require (and automatically start) an actual instance of
/// AzureEventGridSimulator.exe as there is no way to inject an HttpClient (from a
/// WebApplicationFactory)
/// into Azure.Messaging.EventGrid.
/// </summary>
[Collection(nameof(ActualSimulatorFixtureCollection))]
[Trait("Category", "integration-actual")]
public class AzureMessagingEventGridTest
{
    private static EventGridPublisherClientOptions CreateClientOptions()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        return new EventGridPublisherClientOptions
        {
            Transport = new HttpClientTransport(handler),
            Retry =
            {
                Mode = RetryMode.Fixed,
                MaxRetries = 0,
                NetworkTimeout = TimeSpan.FromSeconds(5),
            },
        };
    }

    [Fact]
    public async Task GivenValidEvent_WhenUriContainsNonStandardPort_ThenItShouldBeAccepted()
    {
        var client = new EventGridPublisherClient(
            new Uri("https://localhost:60101/api/events"),
            new AzureKeyCredential("TheLocal+DevelopmentKey="),
            CreateClientOptions()
        );

        var response = await client.SendEventAsync(
            new EventGridEvent("/the/subject", "The.Event.Type", "v1", new { Id = 1, Foo = "Bar" })
        );

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvents_WhenUriContainsNonStandardPort_TheyShouldBeAccepted()
    {
        var client = new EventGridPublisherClient(
            new Uri("https://localhost:60101/api/events"),
            new AzureKeyCredential("TheLocal+DevelopmentKey="),
            CreateClientOptions()
        );

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
    public async Task GivenValidEvent_WhenUriContainsNonExistentPort_ThenItShouldNotBeAccepted()
    {
        var client = new EventGridPublisherClient(
            new Uri("https://localhost:19999/api/events"),
            new AzureKeyCredential("TheLocal+DevelopmentKey="),
            CreateClientOptions()
        );

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

        exception.Message.ShouldContain("refused");
        exception.Status.ShouldBe(0);
    }

    [Fact]
    public async Task GivenValidEvent_WhenKeyIsWrong_ThenItShouldNotBeAccepted()
    {
        var client = new EventGridPublisherClient(
            new Uri("https://localhost:60101/api/events"),
            new AzureKeyCredential("TheWrongLocal+DevelopmentKey="),
            CreateClientOptions()
        );

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
