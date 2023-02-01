using System;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Messaging.EventGrid;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
/// NOTE: These tests require (and automatically start) an actual instance of AzureEventGridSimulator.exe as WebApplicationFactory does not use a TCP connection (no port in use).
/// </summary>
[Collection(nameof(ActualSimulatorFixtureCollection))]
[Trait("Category", "integration-actual")]
public class AzureMessagingEventGridTest
{
    // ReSharper disable once NotAccessedField.Local
    private readonly ActualSimulatorFixture _actualSimulatorFixture;

    public AzureMessagingEventGridTest(ActualSimulatorFixture actualSimulatorFixture)
    {
        _actualSimulatorFixture = actualSimulatorFixture;
    }

    [Fact]
    public async Task GivenValidEvent_WhenUriContainsNonExistentPort_ThenItShouldNotBeAccepted()
    {
        var client = new EventGridPublisherClient(
                                                  new Uri("https://localhost:19999/api/events"),
                                                  new AzureKeyCredential("TheLocal+DevelopmentKey="),
                                                  new EventGridPublisherClientOptions
                                                      { Retry = { Mode = RetryMode.Fixed, MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(5) } });

        var exception = await Should.ThrowAsync<RequestFailedException>(async () =>
        {
            await client.SendEventAsync(new EventGridEvent("/the/subject", "The.Event.Type", "v1",
                                                           new { Id = 1, Foo = "Bar" }));
        });

        exception.Message.ShouldContain("refused");
        exception.Status.ShouldBe(0);
    }
}
