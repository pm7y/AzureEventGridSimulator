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
using Newtonsoft.Json.Linq;
using RichardSzalay.MockHttp;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

[Trait("Category", "integration-actual")]
[Trait("EventType", "EventGridEvent")]
public class AzureMessagingEventGridTest : IClassFixture<IntegrationContextFixture>
{
    private readonly IntegrationContextFixture _context;
    private readonly ITestOutputHelper _testOutputHelper;

    public AzureMessagingEventGridTest(IntegrationContextFixture context, ITestOutputHelper testOutputHelper)
    {
        _context = context;
        _testOutputHelper = testOutputHelper;
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
        var messages = Array.Empty<ServiceBusMessage<Domain.Entities.EventGridEvent>>();

        _context.MockHttp.Expect(HttpMethod.Post, "https://eventgrideventnamespace.servicebus.windows.net/EventGridEvent-Topic/messages")
            .WithHeaders("content-type", "application/vnd.microsoft.servicebus.json")
            .Respond(
                async req =>
                {
                    messages = await JsonSerializer.DeserializeAsync<ServiceBusMessage<Domain.Entities.EventGridEvent>[]>(req.Content.ReadAsStream());
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

    [Fact]
    public async Task GivenAnEvent_WhenPhublishedToHttpSubscriber_ThenItShouldBeSerialized()
    {
        var expected = JToken.Parse(@"
            {
              ""id"": ""660322f4-7b56-49e4-be3b-79752c740a3d"",
              ""subject"": ""/the/subject1"",
              ""data"": {
                ""Id"": 1,
                ""Foo"": ""Bar""
              },
              ""eventType"": ""The.Event.Type1"",
              ""eventTime"": ""24/02/2023 8:25:32 AM +00:00"",
              ""dataVersion"": ""v1"",
              ""metadataVersion"": ""1"",
              ""topic"": ""/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/eventGridSimulator/providers/Microsoft.EventGrid/topics/EventGridEvent""
            }");

        var ev = new EventGridEvent("/the/subject1", "The.Event.Type1", "v1", new { Id = 1, Foo = "Bar" });
        ev.Id = "660322f4-7b56-49e4-be3b-79752c740a3d";
        ev.EventTime = new DateTimeOffset(2023, 2, 24, 8, 25, 32, TimeSpan.Zero);

        JToken actual = null;
        _context.MockHttp.Expect(HttpMethod.Post, "http://http.eventgridevent/")
            .WithHeaders("content-type", "application/json")
            .Respond(async req =>
            {
                actual = JToken.Parse(await req.Content.ReadAsStringAsync());
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var client = _context.CreateEventGridEventPublisherClient();
        var response = await client.SendEventAsync(ev);

        response.Status.ShouldBe((int)HttpStatusCode.OK);
        actual.ShouldNotBeNull();
        JToken.DeepEquals(expected, actual).ShouldBeTrue("Actual JSON is not equivalent to expected");
    }

    [Fact]
    public async Task GivenAnEvent_WhenPhublishedToServivceBusSubscriber_ThenItShouldBeSerialized()
    {
        var expected = JToken.Parse(@"
            [
                {
                ""Body"": {
                    ""topic"": ""/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/eventGridSimulator/providers/Microsoft.EventGrid/topics/EventGridEvent"",
                    ""subject"": ""/the/subject1"",
                    ""eventType"": ""The.Event.Type1"",
                    ""eventTime"": ""2023-02-24T16:25:32+08:00"",
                    ""id"": ""2349251d-e0be-43e6-868c-f2457875a416"",
                    ""data"": {
                    ""Id"": 1,
                    ""Foo"": ""Bar""
                    },
                    ""dataVersion"": ""v1"",
                    ""metadataVersion"": ""1""
                },
                ""BrokerProperties"": {
                    ""MessageId"": ""2349251d-e0be-43e6-868c-f2457875a416""
                },
                ""UserProperties"": {
                    ""aeg-event-type"": ""Notification"",
                    ""aeg-subscription-name"": ""EVENTGRIDEVENTAZURESERVICEBUSSUBSCRIBER"",
                    ""aeg-data-version"": ""v1"",
                    ""aeg-metadata-version"": ""1"",
                    ""aeg-delivery-count"": ""0""
                }
                }
            ]");

        var ev = new EventGridEvent("/the/subject1", "The.Event.Type1", "v1", new { Id = 1, Foo = "Bar" })
        {
            Id = "2349251d-e0be-43e6-868c-f2457875a416",
            EventTime = new DateTimeOffset(2023, 2, 24, 8, 25, 32, TimeSpan.Zero)
        };

        JToken actual = null;
        _context.MockHttp.Expect(HttpMethod.Post, "https://EventGridEventNamespace.servicebus.windows.net/EventGridEvent-Topic/messages")
            .WithHeaders("content-type", "application/vnd.microsoft.servicebus.json")
            .Respond(async req =>
            {
                actual = JToken.Parse(await req.Content.ReadAsStringAsync());
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var client = _context.CreateEventGridEventPublisherClient();
        var response = await client.SendEventAsync(ev);

        response.Status.ShouldBe((int)HttpStatusCode.OK);
        actual.ShouldNotBeNull();
        JToken.DeepEquals(expected, actual).ShouldBeTrue("Actual JSON is not equivalent to expected");
    }
}
