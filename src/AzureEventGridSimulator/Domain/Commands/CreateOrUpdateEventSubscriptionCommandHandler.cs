using AzureEventGridSimulator.Domain.Services.Management;
using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using JetBrains.Annotations;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
[UsedImplicitly]
public class CreateOrUpdateEventSubscriptionCommandHandler(
    SimulatorSettings simulatorSettings,
    SubscriptionValidationSender validationSender,
    ILogger<CreateOrUpdateEventSubscriptionCommandHandler> logger
) : IRequestHandler<CreateOrUpdateEventSubscriptionCommand, CreateOrUpdateEventSubscriptionResult>
{
    public async Task<CreateOrUpdateEventSubscriptionResult> Handle(
        CreateOrUpdateEventSubscriptionCommand request,
        CancellationToken cancellationToken
    )
    {
        var topic = simulatorSettings.Topics.FirstOrDefault(t =>
            string.Equals(t.Name, request.Scope.TopicName, StringComparison.OrdinalIgnoreCase)
        );

        if (topic is null)
        {
            return new CreateOrUpdateEventSubscriptionResult(
                EventSubscriptionWriteOutcome.TopicNotFound,
                null
            );
        }

        if (
            !EventSubscriptionMapper.TryMapToHttpSubscriber(
                request.EventSubscriptionName,
                request.Resource,
                out var subscriber
            )
        )
        {
            return new CreateOrUpdateEventSubscriptionResult(
                EventSubscriptionWriteOutcome.UnsupportedDestination,
                null
            );
        }

        var alreadyExisted = topic.Subscribers.HttpSubscribers.Any(s =>
            string.Equals(s.Name, request.EventSubscriptionName, StringComparison.OrdinalIgnoreCase)
        );

        topic.Subscribers.UpsertHttpSubscriber(subscriber!);

        logger.LogInformation(
            "{Action} runtime event subscription '{SubscriptionName}' on topic '{TopicName}'",
            alreadyExisted ? "Updated" : "Created",
            subscriber!.Name,
            topic.Name
        );

        // Azure performs the webhook validation handshake while provisioning the subscription, and
        // the simulator gates delivery on a successful handshake. Run it now so a runtime-created
        // subscription starts receiving events as soon as it is created.
        if (!subscriber.DisableValidation)
        {
            await validationSender.ValidateAsync(topic, subscriber, cancellationToken);
        }

        var resource = EventSubscriptionMapper.MapToArm(request.Scope, subscriber);

        return new CreateOrUpdateEventSubscriptionResult(
            alreadyExisted
                ? EventSubscriptionWriteOutcome.Updated
                : EventSubscriptionWriteOutcome.Created,
            resource
        );
    }
}
