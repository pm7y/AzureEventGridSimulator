using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Settings for Azure Storage Queue subscribers.
/// </summary>
public class StorageQueueSubscriberSettings : SubscriberSettingsBase
{
    /// <summary>
    ///     Gets or sets the Storage Queue connection string.
    /// </summary>
    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; init; }

    /// <summary>
    ///     Gets or sets the queue name.
    /// </summary>
    [JsonPropertyName("queueName")]
    public required string QueueName { get; init; }

    /// <summary>
    ///     Gets the effective connection string, either from subscriber or topic level.
    /// </summary>
    [JsonIgnore]
    public string? EffectiveConnectionString =>
        !string.IsNullOrWhiteSpace(ConnectionString)
            ? ConnectionString
            : ParentTopic?.StorageQueueConnectionString;

    [JsonIgnore]
    public override string SubscriberType => "storageQueue";

    public override void Validate()
    {
        ValidateName();

        // Validate connection string (considering topic-level default)
        if (string.IsNullOrWhiteSpace(EffectiveConnectionString))
        {
            throw new ArgumentException(
                $"Storage Queue subscriber '{Name}' must have a connectionString, either at subscriber or topic level."
            );
        }

        if (string.IsNullOrWhiteSpace(QueueName))
        {
            throw new ArgumentException(
                $"Storage Queue subscriber '{Name}' must have a queueName."
            );
        }

        ValidateCommonTail();
    }
}
