using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Domain.Commands;

public class ValidateSubscriptionCommandHandler(
    TimeProvider timeProvider,
    ILogger<ValidateSubscriptionCommandHandler> logger
) : IRequestHandler<ValidateSubscriptionCommand, bool>
{
    public Task<bool> Handle(
        ValidateSubscriptionCommand request,
        CancellationToken cancellationToken
    )
    {
        // Only HTTP subscribers need validation (Service Bus doesn't use webhook validation)
        var subscriber = request.Topic.Subscribers.HttpSubscribers.FirstOrDefault(s =>
            s.ValidationCode == request.ValidationCode
        );

        if (
            subscriber != null
            && subscriber.ValidationCode == request.ValidationCode
            && !subscriber.ValidationPeriodExpired(timeProvider.GetUtcNow())
        )
        {
            subscriber.ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful;
            logger.LogInformation(
                "Subscription {SubscriptionName} on topic {TopicName} was successfully validated",
                subscriber.Name,
                request.Topic.Name
            );

            return Task.FromResult(true);
        }

        logger.LogWarning(
            "Validation failed for code {ValidationCode} on topic {TopicName}",
            request.ValidationCode,
            request.Topic?.Name
        );
        return Task.FromResult(false);
    }
}
