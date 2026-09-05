using AzureEventGridSimulator.Domain.Services.Management;
using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
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
            EventSubscriptionMapper.TryMapToHttpSubscriber(
                request.EventSubscriptionName,
                request.Resource,
                out var httpSubscriber
            )
        )
        {
            var alreadyExisted = topic.Subscribers.HttpSubscribers.Any(s =>
                string.Equals(
                    s.Name,
                    request.EventSubscriptionName,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            topic.Subscribers.UpsertHttpSubscriber(httpSubscriber!);

            logger.LogInformation(
                "{Action} runtime event subscription '{SubscriptionName}' (webhook) on topic '{TopicName}'",
                alreadyExisted ? "Updated" : "Created",
                httpSubscriber!.Name,
                topic.Name
            );

            // Azure performs the webhook validation handshake while provisioning; the simulator gates
            // delivery on it. Run it now so the subscription starts receiving events immediately.
            if (!httpSubscriber.DisableValidation)
            {
                await validationSender.ValidateAsync(topic, httpSubscriber, cancellationToken);
            }

            return new CreateOrUpdateEventSubscriptionResult(
                alreadyExisted
                    ? EventSubscriptionWriteOutcome.Updated
                    : EventSubscriptionWriteOutcome.Created,
                EventSubscriptionMapper.MapToArm(request.Scope, httpSubscriber)
            );
        }

        if (
            EventSubscriptionMapper.TryMapToStorageQueueSubscriber(
                request.EventSubscriptionName,
                request.Resource,
                out var queueSubscriber
            )
        )
        {
            // Inherit the topic-level storageQueueConnectionString for delivery.
            queueSubscriber!.ParentTopic = topic;

            var alreadyExisted = topic.Subscribers.StorageQueueSubscribers.Any(s =>
                string.Equals(
                    s.Name,
                    request.EventSubscriptionName,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            topic.Subscribers.UpsertStorageQueueSubscriber(queueSubscriber);

            logger.LogInformation(
                "{Action} runtime event subscription '{SubscriptionName}' (storage queue '{QueueName}') on topic '{TopicName}'",
                alreadyExisted ? "Updated" : "Created",
                queueSubscriber.Name,
                queueSubscriber.QueueName,
                topic.Name
            );

            // Storage-queue destinations have no validation handshake (that is webhook-only).
            return new CreateOrUpdateEventSubscriptionResult(
                alreadyExisted
                    ? EventSubscriptionWriteOutcome.Updated
                    : EventSubscriptionWriteOutcome.Created,
                EventSubscriptionMapper.MapToArm(request.Scope, queueSubscriber)
            );
        }

        return new CreateOrUpdateEventSubscriptionResult(
            EventSubscriptionWriteOutcome.UnsupportedDestination,
            null
        );
    }
}
