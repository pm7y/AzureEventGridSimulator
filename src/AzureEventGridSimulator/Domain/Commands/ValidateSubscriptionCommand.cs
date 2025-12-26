using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Domain.Commands;

public class ValidateSubscriptionCommand(TopicSettings topic, Guid validationCode) : IRequest<bool>
{
    public TopicSettings Topic { get; } = topic;

    public Guid ValidationCode { get; } = validationCode;
}
