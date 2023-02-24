namespace AzureEventGridSimulator.Domain.Commands;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Converters;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.Logging;

public class SendNotificationCloudEventsToAzureServiceBusSubscriberCommandHandler : SendNotificationEventsToAzureServiceBusSubscriberCommandHandler<CloudEvent>
{
    private readonly EventConverter<CloudEvent> _cloudEventConverter;

    public SendNotificationCloudEventsToAzureServiceBusSubscriberCommandHandler(
        ILogger<SendNotificationCloudEventsToAzureServiceBusSubscriberCommandHandler> logger,
        IHttpClientFactory httpClientFactory,
        ServiceBusMessageConverter<CloudEvent> serviceBusMessageConverter,
        EventConverter<CloudEvent> cloudEventConverter)
        : base(logger, httpClientFactory, serviceBusMessageConverter)
    {
        _cloudEventConverter = cloudEventConverter;
    }

    protected override BinaryData SerializeMessage(CloudEvent evt)
    {
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        options.Converters.Add(_cloudEventConverter);

        return new BinaryData(JsonSerializer.Serialize(evt, options));
    }

    protected override void AddAegProperties(ref ServiceBusMessage<CloudEvent> message, AzureServiceBusSubscriptionSettings subscription, CloudEvent evt)
    {
        message.UserProperties[Constants.AegEventTypeHeader] = Constants.NotificationEventType;
        message.UserProperties[Constants.AegSubscriptionNameHeader] = subscription.Name.ToUpperInvariant();
        message.UserProperties[Constants.AegDataVersionHeader] = string.Empty;
        message.UserProperties[Constants.AegMetadataVersionHeader] = "1";
        message.UserProperties[Constants.AegDeliveryCountHeader] = "0";
    }
}
