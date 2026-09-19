using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Settings for Azure Event Hub subscribers.
/// </summary>
public class EventHubSubscriberSettings : NamespaceSubscriberSettingsBase
{
    /// <summary>
    ///     Gets or sets the Event Hub name.
    /// </summary>
    [JsonPropertyName("eventHubName")]
    public required string EventHubName { get; init; }

    [JsonIgnore]
    public override string SubscriberType => "eventHub";

    protected override string? TopicConnectionString => ParentTopic?.EventHubConnectionString;

    protected override string? TopicNamespace => ParentTopic?.EventHubNamespace;

    protected override string? TopicSharedAccessKeyName => ParentTopic?.EventHubSharedAccessKeyName;

    protected override string? TopicSharedAccessKey => ParentTopic?.EventHubSharedAccessKey;

    protected override string DisplayName => "Event Hub";

    // Order matters: it decides which error a user sees first. Event Hub checks
    // eventHubName before authentication.
    public override void Validate()
    {
        ValidateName();

        // Validate Event Hub name
        if (string.IsNullOrWhiteSpace(EventHubName))
        {
            throw new ArgumentException(
                $"Event Hub subscriber '{Name}' must specify an eventHubName."
            );
        }

        ValidateNamespaceAuth();
        ValidateProperties();
        ValidateCommonTail();
    }
}
