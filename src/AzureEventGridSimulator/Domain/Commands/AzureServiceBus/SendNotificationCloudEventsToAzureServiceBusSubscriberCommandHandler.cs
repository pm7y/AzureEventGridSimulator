namespace AzureEventGridSimulator.Domain.Commands;

using System.Net.Http;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.Logging;

public class SendNotificationCloudEventsToAzureServiceBusSubscriberCommandHandler : SendNotificationEventsToAzureServiceBusSubscriberCommandHandler<CloudEvent>
{
    public SendNotificationCloudEventsToAzureServiceBusSubscriberCommandHandler(ILogger<SendNotificationCloudEventsToAzureServiceBusSubscriberCommandHandler> logger, IHttpClientFactory httpClientFactory)
        : base(logger, httpClientFactory)
    {
    }

    protected override void AddAegProperties(ref Message message, AzureServiceBusSubscriptionSettings subscription, CloudEvent evt)
    {
        message.UserProperties[Constants.AegEventTypeHeader] = Constants.NotificationEventType;
        message.UserProperties[Constants.AegSubscriptionNameHeader] = subscription.Name.ToUpperInvariant();
        message.UserProperties[Constants.AegDataVersionHeader] = string.Empty;
        message.UserProperties[Constants.AegMetadataVersionHeader] = "1";
        message.UserProperties[Constants.AegDeliveryCountHeader] = "0";
    }
}
