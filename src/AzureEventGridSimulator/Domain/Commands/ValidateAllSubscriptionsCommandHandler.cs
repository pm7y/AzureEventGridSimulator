using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using JetBrains.Annotations;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
[UsedImplicitly]
public class ValidateAllSubscriptionsCommandHandler(
    SimulatorSettings simulatorSettings,
    SubscriptionValidationSender validationSender
) : IRequestHandler<ValidateAllSubscriptionsCommand>
{
    public async Task Handle(
        ValidateAllSubscriptionsCommand request,
        CancellationToken cancellationToken
    )
    {
        foreach (var enabledTopic in simulatorSettings.Topics.Where(o => !o.Disabled))
        {
            // Only HTTP subscribers need validation (Service Bus subscribers don't use webhook validation)
            foreach (
                var subscriber in enabledTopic.Subscribers.HttpSubscribers.Where(o =>
                    !o.DisableValidation && !o.Disabled
                )
            )
            {
                await validationSender.ValidateAsync(enabledTopic, subscriber, cancellationToken);
            }
        }
    }
}
