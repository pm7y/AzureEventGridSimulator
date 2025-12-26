using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings;

/// <summary>
///     Configurable limits for event validation that match Azure Event Grid behavior.
/// </summary>
public class EventValidationLimits
{
    /// <summary>
    ///     Maximum overall request body size in bytes. Default: 1536000 (1.5MB).
    ///     This matches Azure Event Grid's overall message size limit.
    /// </summary>
    [JsonPropertyName("maximumOverallMessageSizeInBytes")]
    public int MaximumOverallMessageSizeInBytes { get; set; } = 1536000;

    /// <summary>
    ///     Maximum individual event size in bytes. Default: 1049600 (1MB).
    ///     This matches Azure Event Grid's per-event size limit.
    /// </summary>
    [JsonPropertyName("maximumEventSizeInBytes")]
    public int MaximumEventSizeInBytes { get; set; } = 1049600;
}
