using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services.Retry;

/// <summary>
/// Writes dead-lettered events to local JSON files.
/// </summary>
public class DeadLetterService(ILogger<DeadLetterService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Writes a dead-letter event to the configured folder.
    /// </summary>
    /// <param name="delivery" >
    /// The failed delivery.
    /// </param>
    /// <param name="reason" >
    /// The reason for dead-lettering.
    /// </param>
    public async Task WriteDeadLetterAsync(PendingDelivery delivery, string reason)
    {
        var deadLetterSettings = delivery.Subscriber.DeadLetter;

        if (deadLetterSettings?.Enabled != true)
        {
            logger.LogDebug(
                "Dead-lettering disabled for subscriber '{SubscriberName}'. Event {EventId} will be dropped",
                delivery.Subscriber.Name,
                delivery.Event.Id
            );
            return;
        }

        try
        {
            var basePath = deadLetterSettings.FolderPath ?? "./dead-letters";
            var folder = Path.Combine(basePath, delivery.Topic.Name, delivery.Subscriber.Name);

            Directory.CreateDirectory(folder);

            var lastAttempt = delivery.LastAttempt;

            var deadLetterEvent = new DeadLetterEvent
            {
                DeadLetterReason = reason,
                DeliveryAttempts = delivery.AttemptCount,
                LastDeliveryOutcome = lastAttempt?.Outcome.ToString() ?? "Unknown",
                LastHttpStatusCode = lastAttempt?.HttpStatusCode,
                LastErrorMessage = lastAttempt?.ErrorMessage,
                PublishTime = delivery.EnqueuedTime,
                LastDeliveryAttemptTime = lastAttempt?.AttemptTime ?? delivery.EnqueuedTime,
                TopicName = delivery.Topic.Name,
                SubscriberName = delivery.Subscriber.Name,
                SubscriberType = delivery.Subscriber.SubscriberType,
                Event = GetEventPayload(delivery),
            };

            var timestamp = delivery.EnqueuedTime.ToString("yyyyMMdd_HHmmss");
            var eventId = SanitizeFileName(delivery.Event.Id ?? Guid.NewGuid().ToString());
            var fileName = $"{timestamp}_{eventId}.json";
            var filePath = Path.Combine(folder, fileName);

            var json = JsonSerializer.Serialize(deadLetterEvent, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            logger.LogWarning(
                "Event {EventId} dead-lettered to {FilePath}. Reason: {Reason}. Attempts: {Attempts}",
                delivery.Event.Id,
                filePath,
                reason,
                delivery.AttemptCount
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to write dead-letter file for event {EventId}. Reason: {Reason}",
                delivery.Event.Id,
                reason
            );
        }
    }

    /// <summary>
    /// Gets the event payload for serialization.
    /// </summary>
    private static object GetEventPayload(PendingDelivery delivery)
    {
        return delivery.Event.Schema switch
        {
            EventSchema.EventGridSchema => delivery.Event.EventGridEvent,
            EventSchema.CloudEventV1_0 => delivery.Event.CloudEvent,
            _ => new
            {
                delivery.Event.Id,
                delivery.Event.EventType,
                delivery.Event.Subject,
            },
        };
    }

    /// <summary>
    /// Sanitizes a string to be safe for use as a file name.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());

        // Limit length
        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50];
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
