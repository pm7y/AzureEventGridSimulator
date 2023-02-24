﻿namespace AzureEventGridSimulator.Tests.IntegrationTests.CloudEvent;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Converters;
using AzureEventGridSimulator.Tests.IntegrationTests.Infrastrucure;
using Newtonsoft.Json.Linq;
using RichardSzalay.MockHttp;
using Shouldly;
using Xunit;

[Trait("Category", "integration-actual")]
[Trait("EventType", "CloudEvent")]
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
        var client = _context.CreateCloudEventPublisherClient();

        var response = await client.SendEventAsync(new CloudEvent("/the/subject", "The.Event.Type", new { Id = 1, Foo = "Bar" }));

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvents_WhenUriContainsNonStandardPort_TheyShouldBeAccepted()
    {
        var client = _context.CreateCloudEventPublisherClient();

        var events = new[]
        {
                new CloudEvent("/the/subject1", "The.Event.Type1", new { Id = 1, Foo = "Bar" }),
                new CloudEvent("/the/subject2", "The.Event.Type2", new { Id = 2, Foo = "Baz" })
            };

        var response = await client.SendEventsAsync(events);

        response.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenValidEvent_WhenKeyIsWrong_ThenItShouldNotBeAccepted()
    {
        var client = _context.CreateCloudEventPublisherClient(sasKey: "TheWrongLocal+DevelopmentKey=");

        var exception = await Should.ThrowAsync<RequestFailedException>(async () =>
        {
            await client.SendEventAsync(new CloudEvent("/the/subject", "The.Event.Type", new { Id = 1, Foo = "Bar" }));
        });

        exception.Status.ShouldBe((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenAnEventThatIsTooLarge_WhenPublished_ThenItShouldBeDeclined()
    {
        var testEvent = new CloudEvent("subject", "eventType", new { Blah = 1 });
        var testEvent2 = new CloudEvent("subject", "eventType", new { Blah = "_".PadLeft(1049600, '_') });

        var client = _context.CreateCloudEventPublisherClient();
        var exception = await Assert.ThrowsAsync<RequestFailedException>(() => client.SendEventsAsync(new[] { testEvent, testEvent2 }));

        exception.Status.ShouldBe((int)HttpStatusCode.RequestEntityTooLarge);
        exception.Message.ShouldContain(EventGridEventConverter.MaximumAllowedEventGridEventSizeErrorMesage);
    }

    [Fact]
    public async Task GivenAnEvent_WhenPublished_ThenItShouldBeBroadcastToHttpEndpoint()
    {
        _context.MockHttp.Expect(HttpMethod.Post, "http://http.cloudevent/")
            .WithHeaders("content-type", "application/cloudevents+json")
            .WithHeaders(Constants.AegEventTypeHeader, Constants.NotificationEventType)
            .WithHeaders(Constants.AegSubscriptionNameHeader, "CLOUDEVENTHTTPSUBSCRIBER")
            .WithHeaders(Constants.AegDataVersionHeader, "")
            .WithHeaders(Constants.AegMetadataVersionHeader, "1")
            .WithHeaders(Constants.AegDeliveryCountHeader, "0")
            .Respond(HttpStatusCode.OK);

        var client = _context.CreateCloudEventPublisherClient();

        var ev = new CloudEvent("subject", "eventType", new { Blah = 1 });

        var response = await client.SendEventAsync(ev);

        response.Status.ShouldBe((int)HttpStatusCode.OK);

        _context.MockHttp.VerifyNoOutstandingRequest();
        _context.MockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GivenAnEvent_WhenPublished_ThenItShouldBeBroadcastToServiceBus()
    {
        var messages = Array.Empty<ServiceBusMessage<Domain.Entities.CloudEvent>>();

        _context.MockHttp.Expect(HttpMethod.Post, "https://cloudeventnamespace.servicebus.windows.net/CloudEvent-Topic/messages")
            .WithHeaders("content-type", "application/vnd.microsoft.servicebus.json")
            .Respond(
                async req =>
                {
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new ServiceBusMessageConverter<Domain.Entities.CloudEvent>(new CloudEventConverter()));

                    messages = await JsonSerializer.DeserializeAsync<ServiceBusMessage<Domain.Entities.CloudEvent>[]>(req.Content.ReadAsStream(), options);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

        var expectedUserProperties = new Dictionary<string, string>
            {
                {Constants.AegEventTypeHeader, "Notification" },
                {Constants.AegSubscriptionNameHeader, "CLOUDEVENTAZURESERVICEBUSSUBSCRIBER" },
                {Constants.AegDataVersionHeader, string.Empty },
                {Constants.AegMetadataVersionHeader, "1" },
                {Constants.AegDeliveryCountHeader, "0" }
            };

        var client = _context.CreateCloudEventPublisherClient();

        var ev = new CloudEvent("subject", "eventType", new { Blah = 1 });

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
        var ev = new CloudEvent("subject", "event\"Type", new { Blah = 1, Date = DateTimeOffset.Now })
        {
            Time = DateTimeOffset.UtcNow
        };

        ev.ExtensionAttributes.Add("sample", "SampleValue");

        var expected = JToken.Parse(JsonSerializer.Serialize(ev));
        JToken actual = null;

        _context.MockHttp.Expect(HttpMethod.Post, "http://http.cloudevent/")
            .WithHeaders("content-type", "application/cloudevents+json")
            .Respond(async req =>
            {
                actual = JToken.Parse(await req.Content.ReadAsStringAsync());
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var client = _context.CreateCloudEventPublisherClient();
        var response = await client.SendEventAsync(ev);

        response.Status.ShouldBe((int)HttpStatusCode.OK);
        actual.ShouldNotBeNull();
        JToken.DeepEquals(expected, actual).ShouldBeTrue();
    }

    [Fact]
    public async Task GivenAnEvent_WhenPhublishedToServivceBusSubscriber_ThenItShouldBeSerialized()
    {
        var expected = JToken.Parse(@"
[{
        ""Body"": {
            ""id"": ""1619251d-e0be-43e6-868c-f2457875a416"",
            ""source"": ""subject"",
            ""type"": ""event\u0022Type"",
            ""data"": {
                ""Blah"": 1,
                ""Date"": ""2023-02-24T08:23:12\u002B00:00""
            },
            ""time"": ""2023-02-24T08:25:32+00:00"",
            ""specversion"": ""1.0"",
            ""sample"": ""SampleValue""
        },
        ""BrokerProperties"": {
            ""MessageId"": ""1619251d-e0be-43e6-868c-f2457875a416""
        },
        ""UserProperties"": {
            ""aeg-event-type"": ""Notification"",
            ""aeg-subscription-name"": ""CLOUDEVENTAZURESERVICEBUSSUBSCRIBER"",
            ""aeg-data-version"": """",
            ""aeg-metadata-version"": ""1"",
            ""aeg-delivery-count"": ""0""
        }
    }
]");

        var ev = new CloudEvent("subject", "event\"Type", new { Blah = 1, Date = new DateTimeOffset(2023, 2, 24, 8, 23, 12, TimeSpan.Zero) })
        {
            Id = "1619251d-e0be-43e6-868c-f2457875a416",
            Time = new DateTimeOffset(2023, 2, 24, 8, 25, 32, TimeSpan.Zero)
        };

        ev.ExtensionAttributes.Add("sample", "SampleValue");

        JToken actual = null;
        _context.MockHttp.Expect(HttpMethod.Post, "https://cloudeventnamespace.servicebus.windows.net/CloudEvent-Topic/messages")
            .WithHeaders("content-type", "application/vnd.microsoft.servicebus.json")
            .Respond(async req =>
            {
                actual = JToken.Parse(await req.Content.ReadAsStringAsync());
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var client = _context.CreateCloudEventPublisherClient();
        var response = await client.SendEventAsync(ev);

        response.Status.ShouldBe((int)HttpStatusCode.OK);
        actual.ShouldNotBeNull();
        JToken.DeepEquals(expected, actual).ShouldBeTrue();
    }
}
