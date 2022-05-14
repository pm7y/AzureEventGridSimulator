using System;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;

namespace AzureEventGridSimulator.Domain.Commands;

public class ValidateSubscriptionCommand : IRequest<bool>
{
    public ValidateSubscriptionCommand(TopicSettings topic, Guid validationCode)
    {
        ValidationCode = validationCode;
        Topic = topic;
    }

    public TopicSettings Topic { get; }

    public Guid ValidationCode { get; }
}
