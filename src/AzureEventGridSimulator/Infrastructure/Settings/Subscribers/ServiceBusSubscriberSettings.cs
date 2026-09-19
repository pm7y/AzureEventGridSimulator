using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Settings for Azure Service Bus subscribers (queues and topics).
/// </summary>
public class ServiceBusSubscriberSettings : NamespaceSubscriberSettingsBase
{
    /// <summary>
    ///     Gets or sets the topic name. Either Topic or Queue must be specified, but not both.
    /// </summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>
    ///     Gets or sets the queue name. Either Topic or Queue must be specified, but not both.
    /// </summary>
    [JsonPropertyName("queue")]
    public string? Queue { get; init; }

    /// <summary>
    ///     Gets the destination name (either topic or queue).
    /// </summary>
    [JsonIgnore]
    public string DestinationName =>
        !string.IsNullOrWhiteSpace(Topic)
            ? Topic
            : Queue ?? throw new InvalidOperationException("Neither Topic nor Queue is set");

    /// <summary>
    ///     Gets whether the destination is a topic (vs a queue).
    /// </summary>
    [JsonIgnore]
    public bool IsTopic => !string.IsNullOrWhiteSpace(Topic);

    [JsonIgnore]
    public override string SubscriberType => "serviceBus";

    protected override string? TopicConnectionString => ParentTopic?.ServiceBusConnectionString;

    protected override string? TopicNamespace => ParentTopic?.ServiceBusNamespace;

    protected override string? TopicSharedAccessKeyName =>
        ParentTopic?.ServiceBusSharedAccessKeyName;

    protected override string? TopicSharedAccessKey => ParentTopic?.ServiceBusSharedAccessKey;

    protected override string DisplayName => "Service Bus";

    // Order matters: it decides which error a user sees first. Service Bus checks
    // authentication before the topic/queue destination.
    public override void Validate()
    {
        ValidateName();
        ValidateNamespaceAuth();

        // Validate destination
        var hasTopic = !string.IsNullOrWhiteSpace(Topic);
        var hasQueue = !string.IsNullOrWhiteSpace(Queue);

        if (!hasTopic && !hasQueue)
        {
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' must specify either a topic or queue."
            );
        }

        if (hasTopic && hasQueue)
        {
            throw new ArgumentException(
                $"Service Bus subscriber '{Name}' must specify either a topic or queue, not both."
            );
        }

        ValidateProperties();
        ValidateCommonTail();
    }
}
