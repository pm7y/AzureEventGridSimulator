namespace AzureEventGridSimulator.Tests.IntegrationTests.EventGridEvent;

using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.EventGrid;
using AzureEventGridSimulator.Domain.Converters;
using AzureEventGridSimulator.Tests.IntegrationTests.Infrastrucure;
using Shouldly;
using Xunit;

/// <summary>
/// These test use a WebApplicationFactory based instance of the simulator
/// and an HttpClient to send events to the simulator.
/// Note: this is a WIP.
/// </summary>
[Trait("Category", "integration")]
public class BasicTests : IClassFixture<IntegrationContextFixture>
{
    private readonly EventGridPublisherClient _client;

    public BasicTests(IntegrationContextFixture factory)
    {
        _client = factory.CreateEventGridEventPublisherClient();
    }

    [Fact]
    public async Task GivenAValidEvent_WhenPublished_ThenItShouldBeAccepted()
    {
        // Arrange
        var testEvent = new EventGridEvent("subject", "eventType", "1.0", new { Blah = 1 });

        // Act
        using var response = await _client.SendEventAsync(testEvent);

        // Assert
        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenAnEventThatIsTooLarge_WhenPublished_ThenItShouldBeDeclined()
    {
        // Arrange
        var testEvent = new EventGridEvent("subject", "eventType", "1.0", new { Blah = 1 });
        var testEvent2 = new EventGridEvent("subject", "eventType", "1.0", new { Blah = "_".PadLeft(1049600, '_') });

        // Act

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() => _client.SendEventsAsync(new[] { testEvent, testEvent2 }));

        // Assert
        exception.Status.ShouldBe((int)HttpStatusCode.RequestEntityTooLarge);
        exception.Message.ShouldContain(EventGridEventConverter.MaximumAllowedEventGridEventSizeErrorMesage);
    }
}
