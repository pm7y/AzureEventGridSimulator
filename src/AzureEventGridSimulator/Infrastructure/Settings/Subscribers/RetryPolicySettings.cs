using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Retry policy settings matching Azure Event Grid behavior.
/// </summary>
public class RetryPolicySettings
{
    /// <summary>
    ///     Gets or sets whether retry is enabled.
    ///     Default is true to match Azure Event Grid behavior.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the maximum number of delivery attempts (1-30).
    ///     Default is 30 to match Azure Event Grid.
    /// </summary>
    [JsonPropertyName("maxDeliveryAttempts")]
    public int MaxDeliveryAttempts { get; set; } = 30;

    /// <summary>
    ///     Gets or sets the event time-to-live in minutes (1-1440).
    ///     Events older than this are dead-lettered. Default is 1440 (24 hours).
    /// </summary>
    [JsonPropertyName("eventTimeToLiveInMinutes")]
    public int EventTimeToLiveInMinutes { get; set; } = 1440;

    public void Validate()
    {
        if (MaxDeliveryAttempts < 1 || MaxDeliveryAttempts > 30)
            throw new ArgumentException(
                "MaxDeliveryAttempts must be between 1 and 30.",
                nameof(MaxDeliveryAttempts)
            );

        if (EventTimeToLiveInMinutes < 1 || EventTimeToLiveInMinutes > 1440)
            throw new ArgumentException(
                "EventTimeToLiveInMinutes must be between 1 and 1440.",
                nameof(EventTimeToLiveInMinutes)
            );
    }
}
