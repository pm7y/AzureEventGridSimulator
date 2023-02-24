using System;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;

namespace AzureEventGridSimulator.Domain.Commands.Http;

public class ValidateHttpSubscriptionCommand : IRequest<bool>
{
    public ValidateHttpSubscriptionCommand(TopicSettings topic, Guid validationCode)
    {
        ValidationCode = validationCode;
        Topic = topic;
    }

    public TopicSettings Topic { get; }

    public Guid ValidationCode { get; }
}
