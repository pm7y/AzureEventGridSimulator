namespace AzureEventGridSimulator.Domain.Commands;

using System.Net.Http;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.Logging;

public class SendNotificationEventGridEventsToAzureServiceBusSubscriberCommandHandler : SendNotificationEventsToAzureServiceBusSubscriberCommandHandler<EventGridEvent>
{
    public SendNotificationEventGridEventsToAzureServiceBusSubscriberCommandHandler(ILogger<SendNotificationEventGridEventsToAzureServiceBusSubscriberCommandHandler> logger, IHttpClientFactory httpClientFactory)
        : base(logger, httpClientFactory)
    {
    }

    protected override void AddAegProperties(ref Message message, AzureServiceBusSubscriptionSettings subscription, EventGridEvent evt)
    {
        message.UserProperties[Constants.AegEventTypeHeader] = Constants.NotificationEventType;
        message.UserProperties[Constants.AegSubscriptionNameHeader] = subscription.Name.ToUpperInvariant();
        message.UserProperties[Constants.AegDataVersionHeader] = evt.DataVersion;
        message.UserProperties[Constants.AegMetadataVersionHeader] = evt.MetadataVersion;
        message.UserProperties[Constants.AegDeliveryCountHeader] = "0";
    }
}
