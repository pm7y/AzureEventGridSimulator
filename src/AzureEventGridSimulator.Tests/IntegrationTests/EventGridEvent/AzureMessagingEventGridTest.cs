namespace AzureEventGridSimulator.Tests.IntegrationTests.EventGridEvent;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.EventGrid;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Converters;
using AzureEventGridSimulator.Tests.IntegrationTests.Infrastrucure;
using RichardSzalay.MockHttp;
using Shouldly;
using Xunit;

[Trait("Category", "integration-actual")]
[Trait("EventType", "EventGridEvent")]
public class AzureMessagingEventGridTest : IClassFixture<IntegrationContextFixture>
{
    private readonly IntegrationContextFixture _context;

    public AzureMessagingEventGridTest(IntegrationContextFixture context)
    {
        _context = context;
    }

    [Fact]
    public async Task GivenValidEvent_WhenUriContainsNonStandardPort_ThenItShouldBeAccepted()
    {
        var client = _context.CreateEventGridEventPublisherClient();

        var response = await client.SendEventAsync(new EventGridEvent("/the/subject", "The.Event.Type", "v1", new { Id = 1, Foo = "Bar" }));

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvents_WhenUriContainsNonStandardPort_TheyShouldBeAccepted()
    {
        var client = _context.CreateEventGridEventPublisherClient();

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
        var client = _context.CreateEventGridEventPublisherClient(sasKey: "TheWrongLocal+DevelopmentKey=");

        var exception = await Should.ThrowAsync<RequestFailedException>(async () =>
        {
            await client.SendEventAsync(new EventGridEvent("/the/subject", "The.Event.Type", "v1",
                                                           new { Id = 1, Foo = "Bar" }));
        });

        exception.Status.ShouldBe((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenAnEventThatIsTooLarge_WhenPublished_ThenItShouldBeDeclined()
    {
        var testEvent = new EventGridEvent("subject", "eventType", "1.0", new { Blah = 1 });
        var testEvent2 = new EventGridEvent("subject", "eventType", "1.0", new { Blah = "_".PadLeft(1049600, '_') });

        var client = _context.CreateEventGridEventPublisherClient();
        var exception = await Assert.ThrowsAsync<RequestFailedException>(() => client.SendEventsAsync(new[] { testEvent, testEvent2 }));

        exception.Status.ShouldBe((int)HttpStatusCode.RequestEntityTooLarge);
        exception.Message.ShouldContain(EventGridEventConverter.MaximumAllowedEventGridEventSizeErrorMesage);
    }

    [Fact]
    public async Task GivenAnEvent_WhenPublished_ThenItShouldBeBroadcastToHttpEndpoint()
    {
        _context.MockHttp.Expect(HttpMethod.Post, "http://http.eventgridevent/")
            .WithHeaders("content-type", "application/json")
            .WithHeaders(Constants.AegEventTypeHeader, Constants.NotificationEventType)
            .WithHeaders(Constants.AegSubscriptionNameHeader, "EVENTGRIDEVENTHTTPSUBSCRIBER")
            .WithHeaders(Constants.AegDataVersionHeader, "v1")
            .WithHeaders(Constants.AegMetadataVersionHeader, "1")
            .WithHeaders(Constants.AegDeliveryCountHeader, "0")
            .Respond(HttpStatusCode.OK);

        var client = _context.CreateEventGridEventPublisherClient();

        var ev = new EventGridEvent("/the/subject1", "The.Event.Type1", "v1", new { Id = 1, Foo = "Bar" });

        var response = await client.SendEventAsync(ev);

        response.Status.ShouldBe((int)HttpStatusCode.OK);

        _context.MockHttp.VerifyNoOutstandingRequest();
        _context.MockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GivenAnEvent_WhenPublished_ThenItShouldBeBroadcastToServiceBus()
    {
        var messages = Array.Empty<SendNotificationEventsToAzureServiceBusSubscriberCommandHandler<Domain.Entities.IEvent>.Message>();

        _context.MockHttp.Expect(HttpMethod.Post, "https://eventgrideventnamespace.servicebus.windows.net/EventGridEvent-Topic/messages")
            .WithHeaders("content-type", "application/vnd.microsoft.servicebus.json")
            .Respond(
                async req =>
                {
                    messages = await JsonSerializer.DeserializeAsync<SendNotificationEventsToAzureServiceBusSubscriberCommandHandler<Domain.Entities.IEvent>.Message[]>(req.Content.ReadAsStream());
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

        var expectedUserProperties = new Dictionary<string, string>
            {
                {Constants.AegEventTypeHeader, "Notification" },
                {Constants.AegSubscriptionNameHeader, "EVENTGRIDEVENTAZURESERVICEBUSSUBSCRIBER" },
                {Constants.AegDataVersionHeader, "v1" },
                {Constants.AegMetadataVersionHeader, "1" },
                {Constants.AegDeliveryCountHeader, "0" }
            };

        var client = _context.CreateEventGridEventPublisherClient();

        var ev = new EventGridEvent("/the/subject1", "The.Event.Type1", "v1", new { Id = 1, Foo = "Bar" });

        var response = await client.SendEventAsync(ev);

        response.Status.ShouldBe((int)HttpStatusCode.OK);

        _context.MockHttp.VerifyNoOutstandingRequest();
        _context.MockHttp.VerifyNoOutstandingExpectation();

        Assert.Single(messages);
        messages[0].UserProperties.ShouldBeEquivalentTo(expectedUserProperties);
    }
}
