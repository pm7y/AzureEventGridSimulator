using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Domain.Services.Retry;

/// <summary>
/// Background service that processes the delivery queue and handles retries.
/// </summary>
public class RetryDeliveryBackgroundService(
    IDeliveryQueue queue,
    IServiceProvider serviceProvider,
    DeadLetterService deadLetterService,
    ILogger<RetryDeliveryBackgroundService> logger
) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Retry delivery background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueDeliveriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing delivery queue");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
        }

        logger.LogInformation("Retry delivery background service stopped");
    }

    /// <summary>
    /// Processes all deliveries that are due.
    /// </summary>
    private async Task ProcessDueDeliveriesAsync(CancellationToken cancellationToken)
    {
        var dueDeliveries = queue.GetDueDeliveries().ToList();

        foreach (var delivery in dueDeliveries)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Remove from queue before processing (we'll re-add if retry needed)
            queue.Remove(delivery.Id);

            await ProcessDeliveryAsync(delivery, cancellationToken);
        }
    }

    /// <summary>
    /// Processes a single delivery.
    /// </summary>
    private async Task ProcessDeliveryAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        // Check if event has expired (TTL)
        if (delivery.IsExpired)
        {
            logger.LogWarning(
                "Event {EventId} expired for subscriber '{SubscriberName}'. TTL exceeded",
                delivery.Event.Id,
                delivery.Subscriber.Name
            );

            await deadLetterService.WriteDeadLetterAsync(delivery, "EventTimeToLiveExpired");
            return;
        }

        // Check if max attempts reached
        if (delivery.HasReachedMaxAttempts)
        {
            logger.LogWarning(
                "Event {EventId} reached max delivery attempts ({MaxAttempts}) for subscriber '{SubscriberName}'",
                delivery.Event.Id,
                delivery.Subscriber.RetryPolicy?.MaxDeliveryAttempts ?? 30,
                delivery.Subscriber.Name
            );

            await deadLetterService.WriteDeadLetterAsync(delivery, "MaxDeliveryAttemptsExceeded");
            return;
        }

        // Attempt delivery
        var result = await AttemptDeliveryAsync(delivery, cancellationToken);

        // Record the attempt
        delivery.AttemptCount++;
        delivery.Attempts.Add(
            new DeliveryAttempt
            {
                AttemptTime = DateTime.UtcNow,
                AttemptNumber = delivery.AttemptCount,
                Outcome = result.Outcome,
                HttpStatusCode = result.HttpStatusCode,
                ErrorMessage = result.ErrorMessage,
            }
        );

        if (result.Success)
        {
            logger.LogDebug(
                "Event {EventId} delivered successfully to '{SubscriberName}' on attempt {Attempt}",
                delivery.Event.Id,
                delivery.Subscriber.Name,
                delivery.AttemptCount
            );
            return;
        }

        // Handle failure
        await HandleDeliveryFailureAsync(delivery, result, cancellationToken);
    }

    /// <summary>
    /// Attempts to deliver an event.
    /// </summary>
    private async Task<DeliveryResult> AttemptDeliveryAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        using var scope = serviceProvider.CreateScope();

        return delivery.Subscriber switch
        {
            HttpSubscriberSettings => await DeliverToHttpAsync(scope, delivery, cancellationToken),
            ServiceBusSubscriberSettings => await DeliverToServiceBusAsync(
                scope,
                delivery,
                cancellationToken
            ),
            StorageQueueSubscriberSettings => await DeliverToStorageQueueAsync(
                scope,
                delivery,
                cancellationToken
            ),
            _ => new DeliveryResult(
                false,
                DeliveryOutcome.NetworkError,
                ErrorMessage: "Unknown subscriber type"
            ),
        };
    }

    /// <summary>
    /// Delivers to an HTTP endpoint.
    /// </summary>
    private static async Task<DeliveryResult> DeliverToHttpAsync(
        IServiceScope scope,
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        var service = scope.ServiceProvider.GetRequiredService<HttpEventDeliveryService>();
        return await service.DeliverAsync(delivery, cancellationToken);
    }

    /// <summary>
    /// Delivers to Service Bus.
    /// </summary>
    private async Task<DeliveryResult> DeliverToServiceBusAsync(
        IServiceScope scope,
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var service =
                scope.ServiceProvider.GetRequiredService<ServiceBusEventDeliveryService>();
            return await service.DeliverAsync(delivery, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Service Bus delivery failed for event {EventId}",
                delivery.Event.Id
            );
            return new DeliveryResult(
                false,
                DeliveryOutcome.ServiceBusError,
                ErrorMessage: ex.Message
            );
        }
    }

    /// <summary>
    /// Delivers to Storage Queue.
    /// </summary>
    private async Task<DeliveryResult> DeliverToStorageQueueAsync(
        IServiceScope scope,
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var service =
                scope.ServiceProvider.GetRequiredService<StorageQueueEventDeliveryService>();
            return await service.DeliverAsync(delivery, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Storage Queue delivery failed for event {EventId}",
                delivery.Event.Id
            );
            return new DeliveryResult(
                false,
                DeliveryOutcome.StorageQueueError,
                ErrorMessage: ex.Message
            );
        }
    }

    /// <summary>
    /// Handles a failed delivery attempt.
    /// </summary>
    private async Task HandleDeliveryFailureAsync(
        PendingDelivery delivery,
        DeliveryResult result,
        CancellationToken cancellationToken
    )
    {
        // Check for immediate dead-letter conditions (HTTP 400, 401, 403, 413)
        if (
            result.HttpStatusCode.HasValue
            && RetryScheduler.ShouldImmediatelyDeadLetter(result.HttpStatusCode.Value)
        )
        {
            var reason = RetryScheduler.GetDeadLetterReasonForStatusCode(
                result.HttpStatusCode.Value
            );

            logger.LogWarning(
                "Event {EventId} immediately dead-lettered due to HTTP {StatusCode} from '{SubscriberName}'",
                delivery.Event.Id,
                result.HttpStatusCode.Value,
                delivery.Subscriber.Name
            );

            await deadLetterService.WriteDeadLetterAsync(delivery, reason);
            return;
        }

        // Check if retry is disabled
        if (!delivery.RetryEnabled)
        {
            logger.LogWarning(
                "Event {EventId} delivery failed and retry is disabled for '{SubscriberName}'. Dead-lettering",
                delivery.Event.Id,
                delivery.Subscriber.Name
            );

            await deadLetterService.WriteDeadLetterAsync(delivery, "RetryDisabled_DeliveryFailed");
            return;
        }

        // Check if we've now hit max attempts after this failure
        if (delivery.HasReachedMaxAttempts)
        {
            logger.LogWarning(
                "Event {EventId} reached max delivery attempts after failure for '{SubscriberName}'",
                delivery.Event.Id,
                delivery.Subscriber.Name
            );

            await deadLetterService.WriteDeadLetterAsync(delivery, "MaxDeliveryAttemptsExceeded");
            return;
        }

        // Check if TTL will be exceeded before next retry
        if (delivery.IsExpired)
        {
            logger.LogWarning(
                "Event {EventId} TTL expired during retry for '{SubscriberName}'",
                delivery.Event.Id,
                delivery.Subscriber.Name
            );

            await deadLetterService.WriteDeadLetterAsync(delivery, "EventTimeToLiveExpired");
            return;
        }

        // Schedule retry
        delivery.NextAttemptTime = RetryScheduler.GetNextRetryTime(
            delivery.AttemptCount,
            result.HttpStatusCode
        );

        queue.RequeueForRetry(delivery);

        logger.LogInformation(
            "Event {EventId} scheduled for retry at {NextAttempt} (attempt {Attempt}/{MaxAttempts}) for '{SubscriberName}'",
            delivery.Event.Id,
            delivery.NextAttemptTime,
            delivery.AttemptCount,
            delivery.Subscriber.RetryPolicy?.MaxDeliveryAttempts ?? 30,
            delivery.Subscriber.Name
        );
    }
}
