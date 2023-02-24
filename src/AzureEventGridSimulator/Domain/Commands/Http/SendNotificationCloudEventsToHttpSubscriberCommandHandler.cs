namespace AzureEventGridSimulator.Domain.Commands;

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Converters;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.Logging;

public class SendNotificationCloudEventsToHttpSubscriberCommandHandler : SendNotificationEventsToHttpSubscriberCommandHandler<CloudEvent>
{
    private readonly JsonSerializerOptions _options;

    public SendNotificationCloudEventsToHttpSubscriberCommandHandler(
        ILogger<SendNotificationEventGridEventsToHttpSubscriberCommandHandler> logger,
        IHttpClientFactory httpClientFactory,
        EventConverter<CloudEvent> cloudEventConverter)
        : base(logger, httpClientFactory)
    {
        _options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        _options.Converters.Add(cloudEventConverter);
    }

    protected override HttpContent GetContent(HttpSubscriptionSettings settings, CloudEvent evt)
    {
        var content = JsonContent.Create(evt, new System.Net.Http.Headers.MediaTypeHeaderValue("application/cloudevents+json"), _options);
        content.Headers.Add(Constants.AegDataVersionHeader, string.Empty);
        content.Headers.Add(Constants.AegDeliveryCountHeader, "0"); // TODO implement re-tries
        content.Headers.Add(Constants.AegEventTypeHeader, Constants.NotificationEventType);
        content.Headers.Add(Constants.AegMetadataVersionHeader, "1");
        content.Headers.Add(Constants.AegSubscriptionNameHeader, settings.Name.ToUpperInvariant());

        return content;
    }
}
