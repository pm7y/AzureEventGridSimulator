using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Shouldly;
using Xunit;
using EventGridEvent = Azure.Messaging.EventGrid.EventGridEvent;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
/// Simple tests to check that we can send an event via Azure.Messaging.EventGrid library.
/// NOTE: These tests require (and automatically start) an actual instance of AzureEventGridSimulator.exe as there is no way to inject an HttpClient (from a WebApplicationFactory)
/// into Azure.Messaging.EventGrid.
/// </summary>
[Collection(nameof(ActualSimulatorFixtureCollection))]
[Trait("Category", "integration-actual")]
public class AzureMessagingEventGridTest
{
    // ReSharper disable once NotAccessedField.Local
    private readonly ActualSimulatorFixture _actualSimulatorFixture;

    private BinaryData _data = new BinaryData(Encoding.UTF8.GetBytes("##This is treated as binary data##"));

    public AzureMessagingEventGridTest(ActualSimulatorFixture actualSimulatorFixture)
    {
        _actualSimulatorFixture = actualSimulatorFixture;
    }

    [Fact]
    public async Task GivenValidEvent_WhenUriContainsNonStandardPort_ThenItShouldBeAccepted()
    {
        var client = new EventGridPublisherClient(
                                                  new Uri("https://localhost:60101/api/events"),
                                                  new AzureKeyCredential("TheLocal+DevelopmentKey="),
                                                  new EventGridPublisherClientOptions
                                                      { Retry = { Mode = RetryMode.Fixed, MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(5) } });

        var response = await client.SendEventAsync(new EventGridEvent("/the/subject", "The.Event.Type", "v1", new { Id = 1, Foo = "Bar" }));

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }


    [Fact]
    public async Task GivenValidCloudEvent_WhenUriContainsNonStandardPort_ThenItShouldBeAccepted()
    {
        var client = new EventGridPublisherClient(
                                                  new Uri("https://localhost:60101/api/events/cloudevent"),
                                                  new AzureKeyCredential("TheLocal+DevelopmentKey="),
                                                  new EventGridPublisherClientOptions
                                                      { Retry = { Mode = RetryMode.Fixed, MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(5) } });

        var myEvent = new CloudEvent("https://awesomesource.com/somestuff", "The.Event.Type", _data, "application/json");

        var response = await client.SendEventAsync(myEvent);

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvents_WhenUriContainsNonStandardPort_TheyShouldBeAccepted()
    {
        var client = new EventGridPublisherClient(
                                                  new Uri("https://localhost:60101/api/events"),
                                                  new AzureKeyCredential("TheLocal+DevelopmentKey="),
                                                  new EventGridPublisherClientOptions
                                                      { Retry = { Mode = RetryMode.Fixed, MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(5) } });

        var events = new[]
        {
            new EventGridEvent("/the/subject1", "The.Event.Type1", "v1", new { Id = 1, Foo = "Bar" }),
            new EventGridEvent("/the/subject2", "The.Event.Type2", "v1", new { Id = 2, Foo = "Baz" })
        };

        var response = await client.SendEventsAsync(events);

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidCloudEvents_WhenUriContainsNonStandardPort_TheyShouldBeAccepted()
    {
        var client = new EventGridPublisherClient(
                                                  new Uri("https://localhost:60101/api/events/cloudevent"),
                                                  new AzureKeyCredential("TheLocal+DevelopmentKey="),
                                                  new EventGridPublisherClientOptions
                                                  { Retry = { Mode = RetryMode.Fixed, MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(5) } });

        var events = new[]
        {
            new CloudEvent("https://awesomesource.com/somestuff1", "The.Event.Type1", _data, "application/json"),
            new CloudEvent("https://awesomesource.com/somestuff2", "The.Event.Type2", _data, "application/json")
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
                                                  new EventGridPublisherClientOptions
                                                      { Retry = { Mode = RetryMode.Fixed, MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(5) } });

        var exception = await Should.ThrowAsync<RequestFailedException>(async () =>
        {
            await client.SendEventAsync(new EventGridEvent("/the/subject", "The.Event.Type", "v1",
                                                           new { Id = 1, Foo = "Bar" }));
        });

        exception.Message.ShouldContain("actively refused");
        exception.Status.ShouldBe(0);
    }

    [Fact]
    public async Task GivenValidEvent_WhenKeyIsWrong_ThenItShouldNotBeAccepted()
    {
        var client = new EventGridPublisherClient(
                                                  new Uri("https://localhost:60101/api/events"),
                                                  new AzureKeyCredential("TheWrongLocal+DevelopmentKey="),
                                                  new EventGridPublisherClientOptions
                                                      { Retry = { Mode = RetryMode.Fixed, MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(5) } });

        var exception = await Should.ThrowAsync<RequestFailedException>(async () =>
        {
            await client.SendEventAsync(new EventGridEvent("/the/subject", "The.Event.Type", "v1",
                                                           new { Id = 1, Foo = "Bar" }));
        });

        exception.Status.ShouldBe((int)HttpStatusCode.Unauthorized);
    }
}
