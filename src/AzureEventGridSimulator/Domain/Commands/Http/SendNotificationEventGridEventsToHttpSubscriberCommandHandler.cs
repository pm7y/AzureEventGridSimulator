namespace AzureEventGridSimulator.Domain.Commands;
using System.Net.Http;
using System.Net.Http.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.Logging;

public class SendNotificationEventGridEventsToHttpSubscriberCommandHandler : SendNotificationEventsToHttpSubscriberCommandHandler<EventGridEvent>
{
    public SendNotificationEventGridEventsToHttpSubscriberCommandHandler(ILogger<SendNotificationEventGridEventsToHttpSubscriberCommandHandler> logger, IHttpClientFactory httpClientFactory)
        : base(logger, httpClientFactory)
    {
    }

    protected override HttpContent GetContent(HttpSubscriptionSettings settings, EventGridEvent evt)
    {
        var content = JsonContent.Create(evt, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
        content.Headers.Add(Constants.AegEventTypeHeader, Constants.NotificationEventType);
        content.Headers.Add(Constants.AegSubscriptionNameHeader, settings.Name.ToUpperInvariant());
        content.Headers.Add(Constants.AegDataVersionHeader, evt.DataVersion);
        content.Headers.Add(Constants.AegMetadataVersionHeader, evt.MetadataVersion);
        content.Headers.Add(Constants.AegDeliveryCountHeader, "0"); // TODO implement re-tries

        return content;
    }
}
