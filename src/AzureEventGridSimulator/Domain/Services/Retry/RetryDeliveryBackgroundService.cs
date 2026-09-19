using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Retry;

/// <summary>
///     Background service that processes the delivery queue and handles retries.
/// </summary>
public class RetryDeliveryBackgroundService(
    IDeliveryQueue queue,
    HttpEventDeliveryService httpDeliveryService,
    ServiceBusEventDeliveryService serviceBusDeliveryService,
    StorageQueueEventDeliveryService storageQueueDeliveryService,
    EventHubEventDeliveryService eventHubDeliveryService,
    DeadLetterService deadLetterService,
    IEventHistoryService eventHistoryService,
    RetryScheduler retryScheduler,
    TimeProvider timeProvider,
    ILogger<RetryDeliveryBackgroundService> logger
) : BackgroundService
{
    /// <summary>
    ///     The most subscribers a pass delivers to at the same time.
    /// </summary>
    private const int MaxConcurrentSubscribers = 8;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
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
                // A delay after each pass, not a fixed period, so a slow pass isn't followed
                // straight away by another
                await Task.Delay(PollInterval, timeProvider, stoppingToken);
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
    ///     Processes all deliveries that are due. Each subscriber's deliveries are processed one at
    ///     a time, in the order they fell due, while different subscribers are processed
    ///     concurrently, so a slow or unreachable subscriber doesn't hold up the others. The pass
    ///     ends when every subscriber's deliveries have been processed. If it's cancelled (at
    ///     shutdown), the deliveries in flight finish, the rest are dropped without being sent
    ///     (they are off the queue, which shutdown discards anyway), and the pass throws an
    ///     <see cref="OperationCanceledException" />.
    /// </summary>
    internal async Task ProcessDueDeliveriesAsync(CancellationToken cancellationToken)
    {
        var dueDeliveries = new List<PendingDelivery>();
        await foreach (var delivery in queue.GetDueDeliveriesAsync(cancellationToken))
        {
            // Remove from queue before processing (we'll re-add if retry needed)
            queue.Remove(delivery.Id);
            dueDeliveries.Add(delivery);
        }

        // GroupBy keeps the due order within each group
        var subscriberGroups = dueDeliveries.GroupBy(d => (d.Topic.Name, d.Subscriber.Name));

        await Parallel.ForEachAsync(
            subscriberGroups,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentSubscribers,
                CancellationToken = cancellationToken,
            },
            async (subscriberDeliveries, ct) =>
            {
                foreach (var delivery in subscriberDeliveries)
                {
                    // Once the pass is cancelled, don't start another delivery: it would go out
                    // with a cancelled token and be recorded as a failed attempt, or even
                    // dead-lettered, without ever being sent
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    await ProcessDeliveryIsolatedAsync(delivery, ct);
                }
            }
        );
    }

    /// <summary>
    ///     Processes a delivery, logging an unexpected error instead of letting it end the pass,
    ///     which would cancel the deliveries in flight to other subscribers.
    /// </summary>
    private async Task ProcessDeliveryIsolatedAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await ProcessDeliveryAsync(delivery, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Error processing event {EventId} for subscriber '{SubscriberName}'",
                delivery.Event.Id,
                delivery.Subscriber.Name
            );
        }
    }

    /// <summary>
    ///     Processes a single delivery.
    /// </summary>
    internal async Task ProcessDeliveryAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        // Check if event has expired (TTL)
        if (delivery.IsExpired(timeProvider.GetUtcNow()))
        {
            logger.LogWarning(
                "Event {EventId} expired for subscriber '{SubscriberName}'. TTL exceeded",
                delivery.Event.Id,
                delivery.Subscriber.Name
            );

            await DeadLetterAsync(delivery, DeadLetterReasons.EventTimeToLiveExpired);
            return;
        }

        // Check if max attempts reached
        if (delivery.HasReachedMaxAttempts)
        {
            logger.LogWarning(
                "Event {EventId} reached max delivery attempts ({MaxAttempts}) for subscriber '{SubscriberName}'",
                delivery.Event.Id,
                delivery.EffectiveRetryPolicy.MaxDeliveryAttempts,
                delivery.Subscriber.Name
            );

            await DeadLetterAsync(delivery, DeadLetterReasons.MaxDeliveryAttemptsExceeded);
            return;
        }

        // Attempt delivery
        var result = await AttemptDeliveryAsync(delivery, cancellationToken);

        // Record the attempt
        delivery.AttemptCount++;
        var attempt = new DeliveryAttempt(
            delivery.AttemptCount,
            result.Outcome,
            timeProvider.GetUtcNow(),
            result.HttpStatusCode,
            result.ErrorMessage
        );
        delivery.Attempts.Add(attempt);

        // Record attempt for dashboard
        eventHistoryService.RecordDeliveryAttempt(
            delivery.Event.Id,
            delivery.Subscriber.Name,
            attempt
        );

        if (result.Success)
        {
            logger.LogDebug(
                "Event {EventId} delivered successfully to '{SubscriberName}' on attempt {Attempt}",
                delivery.Event.Id,
                delivery.Subscriber.Name,
                delivery.AttemptCount
            );
            eventHistoryService.RecordDeliveryCompleted(
                delivery.Event.Id,
                delivery.Subscriber.Name,
                DeliveryStatus.Delivered,
                timeProvider.GetUtcNow()
            );
            return;
        }

        // Handle failure
        await HandleDeliveryFailureAsync(delivery, result, cancellationToken);
    }

    /// <summary>
    ///     Attempts to deliver an event. Each delivery service turns its own failures, exceptions
    ///     included, into a failed <see cref="DeliveryResult" />.
    /// </summary>
    private async Task<DeliveryResult> AttemptDeliveryAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        return delivery.Subscriber switch
        {
            HttpSubscriberSettings => await httpDeliveryService.DeliverAsync(
                delivery,
                cancellationToken
            ),
            ServiceBusSubscriberSettings => await serviceBusDeliveryService.DeliverAsync(
                delivery,
                cancellationToken
            ),
            StorageQueueSubscriberSettings => await storageQueueDeliveryService.DeliverAsync(
                delivery,
                cancellationToken
            ),
            EventHubSubscriberSettings => await eventHubDeliveryService.DeliverAsync(
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
    ///     Handles a failed delivery attempt.
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
            && retryScheduler.ShouldImmediatelyDeadLetter(result.HttpStatusCode.Value)
        )
        {
            var reason = retryScheduler.GetDeadLetterReasonForStatusCode(
                result.HttpStatusCode.Value
            );

            logger.LogWarning(
                "Event {EventId} immediately dead-lettered due to HTTP {StatusCode} from '{SubscriberName}'",
                delivery.Event.Id,
                result.HttpStatusCode.Value,
                delivery.Subscriber.Name
            );

            await DeadLetterAsync(delivery, reason);
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

            await DeadLetterAsync(delivery, DeadLetterReasons.RetryDisabledDeliveryFailed);
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

            await DeadLetterAsync(delivery, DeadLetterReasons.MaxDeliveryAttemptsExceeded);
            return;
        }

        // Check if the TTL has already expired (e.g. during a slow attempt). This checks the
        // current time, not whether the TTL would run out before the next retry
        if (delivery.IsExpired(timeProvider.GetUtcNow()))
        {
            logger.LogWarning(
                "Event {EventId} TTL expired during retry for '{SubscriberName}'",
                delivery.Event.Id,
                delivery.Subscriber.Name
            );

            await DeadLetterAsync(delivery, DeadLetterReasons.EventTimeToLiveExpired);
            return;
        }

        // Schedule retry
        delivery.NextAttemptTime = retryScheduler.GetNextRetryTime(
            delivery.AttemptCount,
            result.HttpStatusCode
        );

        queue.RequeueForRetry(delivery);

        logger.LogInformation(
            "Event {EventId} scheduled for retry at {NextAttempt} (attempt {Attempt}/{MaxAttempts}) for '{SubscriberName}'",
            delivery.Event.Id,
            delivery.NextAttemptTime,
            delivery.AttemptCount,
            delivery.EffectiveRetryPolicy.MaxDeliveryAttempts,
            delivery.Subscriber.Name
        );
    }

    /// <summary>
    ///     Writes the delivery to the subscriber's dead-letter folder (when dead-lettering is
    ///     enabled), then records it as dead-lettered for the dashboard.
    /// </summary>
    private async Task DeadLetterAsync(PendingDelivery delivery, string reason)
    {
        await deadLetterService.WriteDeadLetterAsync(delivery, reason);
        eventHistoryService.RecordDeliveryCompleted(
            delivery.Event.Id,
            delivery.Subscriber.Name,
            DeliveryStatus.DeadLettered,
            timeProvider.GetUtcNow()
        );
    }
}
