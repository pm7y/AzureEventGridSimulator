using System.Net;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Messaging.EventGrid;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
///     A smoke test of the compiled simulator. The Azure.Messaging.EventGrid SDK publishes over a
///     real socket and HTTPS to the process that <see cref="ActualSimulatorFixture" /> starts, so
///     it covers what the in-process test server skips: the apphost, Kestrel's HTTPS binding with
///     the development certificate, and the topics the process reads from its appsettings files.
///     The SDK tests themselves are in IntegrationTests/AzureMessagingEventGridSdkTests, which hand
///     the test server's handler to the SDK's HttpClientTransport. CI runs this class on the
///     ubuntu leg only, after <c>dotnet dev-certs https</c>.
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
}
