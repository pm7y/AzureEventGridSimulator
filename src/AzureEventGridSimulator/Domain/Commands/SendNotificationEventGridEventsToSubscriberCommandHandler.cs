namespace AzureEventGridSimulator.Domain.Commands;

using System;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;

public class SendNotificationEventGridEventsToSubscriberCommandHandler : SendNotificationEventsToSubscriberCommandHandler<EventGridEvent>
{
    public SendNotificationEventGridEventsToSubscriberCommandHandler(ILogger<SendNotificationEventGridEventsToSubscriberCommandHandler> logger, IMediator mediator)
        : base(logger, mediator)
    {
    }

    protected override void Prepare(TopicSettings topic, EventGridEvent[] events)
    {
        foreach (var eventGridEvent in events)
        {
            eventGridEvent.Topic = $"/subscriptions/{Guid.Empty:D}/resourceGroups/eventGridSimulator/providers/Microsoft.EventGrid/topics/{topic.Name}";
            eventGridEvent.MetadataVersion = "1";
        }
    }
}
