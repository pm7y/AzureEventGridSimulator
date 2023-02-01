using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.EventGrid;
using AzureEventGridSimulator.Tests.ActualSimulatorTests;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
/// Simple tests to check that we can send an event via Azure.Messaging.EventGrid library.
/// </summary>
[Collection(nameof(ActualSimulatorFixtureCollection))]
[Trait("Category", "integration-actual")]
public class AzureMessagingEventGridTest : IClassFixture<IntegrationContextFixture>
{
    // ReSharper disable once NotAccessedField.Local
    private readonly IntegrationContextFixture _factory;

    public AzureMessagingEventGridTest(IntegrationContextFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GivenValidEvent_WhenUriContainsNonStandardPort_ThenItShouldBeAccepted()
    {
        var client = _factory.CreateEventGridPublisherClient();

        var response = await client.SendEventAsync(new EventGridEvent("/the/subject", "The.Event.Type", "v1", new { Id = 1, Foo = "Bar" }));

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvents_WhenUriContainsNonStandardPort_TheyShouldBeAccepted()
    {
        var client = _factory.CreateEventGridPublisherClient();

        var events = new[]
        {
            new EventGridEvent("/the/subject1", "The.Event.Type1", "v1", new { Id = 1, Foo = "Bar" }),
            new EventGridEvent("/the/subject2", "The.Event.Type2", "v1", new { Id = 2, Foo = "Baz" })
        };

        var response = await client.SendEventsAsync(events);

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvent_WhenKeyIsWrong_ThenItShouldNotBeAccepted()
    {
        var client = _factory.CreateEventGridPublisherClient(sasKey: "TheWrongLocal+DevelopmentKey=");

        var exception = await Should.ThrowAsync<RequestFailedException>(async () =>
        {
            await client.SendEventAsync(new EventGridEvent("/the/subject", "The.Event.Type", "v1",
                                                           new { Id = 1, Foo = "Bar" }));
        });

        exception.Status.ShouldBe((int)HttpStatusCode.Unauthorized);
    }
}
