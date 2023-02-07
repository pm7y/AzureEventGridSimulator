namespace AzureEventGridSimulator.Domain.Commands;

using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;

public class SendNotificationCloudEventsToSubscriberCommandHandler : SendNotificationEventsToSubscriberCommandHandler<CloudEvent>
{
    public SendNotificationCloudEventsToSubscriberCommandHandler(ILogger<SendNotificationCloudEventsToSubscriberCommandHandler> logger, IMediator mediator)
        : base(logger, mediator)
    {
    }

    protected override void Prepare(TopicSettings topic, CloudEvent[] events)
    {
        // no-op
    }
}
