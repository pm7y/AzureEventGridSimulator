using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Shared settings for subscribers that authenticate to a Service Bus or Event Hubs namespace,
///     with a connection string or namespace credentials given on the subscriber or inherited from
///     the topic.
/// </summary>
public abstract class NamespaceSubscriberSettingsBase : SubscriberSettingsBase
{
    /// <summary>
    ///     Gets or sets the connection string.
    ///     Either this OR (Namespace + SharedAccessKeyName + SharedAccessKey) must be provided.
    /// </summary>
    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; init; }

    /// <summary>
    ///     Gets or sets the namespace (without .servicebus.windows.net suffix).
    /// </summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; init; }

    /// <summary>
    ///     Gets or sets the shared access key name.
    /// </summary>
    [JsonPropertyName("sharedAccessKeyName")]
    public string? SharedAccessKeyName { get; init; }

    /// <summary>
    ///     Gets or sets the shared access key.
    /// </summary>
    [JsonPropertyName("sharedAccessKey")]
    public string? SharedAccessKey { get; init; }

    /// <summary>
    ///     Gets or sets the delivery properties to add to each message.
    ///     Keys are property names, values specify whether the property is static or dynamic.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, DeliveryPropertySettings>? Properties { get; init; }

    /// <summary>
    ///     Gets the connection string, either directly specified, built from components, or inherited from
    ///     topic.
    ///     Falls back to topic-level defaults if not specified at subscriber level.
    /// </summary>
    [JsonIgnore]
    public string? EffectiveConnectionString
    {
        get
        {
            // Subscriber-level connection string (direct)
            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                return ConnectionString;
            }

            // Subscriber-level namespace components
            if (HasSubscriberNamespaceCredentials())
            {
                return BuildConnectionString(Namespace!, SharedAccessKeyName!, SharedAccessKey!);
            }

            // Fall back to topic-level connection string
            if (!string.IsNullOrWhiteSpace(TopicConnectionString))
            {
                return TopicConnectionString;
            }

            // Fall back to topic-level namespace components
            if (HasTopicNamespaceCredentials())
            {
                return BuildConnectionString(
                    TopicNamespace!,
                    TopicSharedAccessKeyName!,
                    TopicSharedAccessKey!
                );
            }

            // No connection string available - will fail validation
            return null;
        }
    }

    /// <summary>
    ///     Gets the topic-level default connection string for this subscriber type, or null when
    ///     there is no parent topic.
    /// </summary>
    protected abstract string? TopicConnectionString { get; }

    /// <summary>
    ///     Gets the topic-level default namespace for this subscriber type, or null when there is
    ///     no parent topic.
    /// </summary>
    protected abstract string? TopicNamespace { get; }

    /// <summary>
    ///     Gets the topic-level default shared access key name for this subscriber type, or null
    ///     when there is no parent topic.
    /// </summary>
    protected abstract string? TopicSharedAccessKeyName { get; }

    /// <summary>
    ///     Gets the topic-level default shared access key for this subscriber type, or null when
    ///     there is no parent topic.
    /// </summary>
    protected abstract string? TopicSharedAccessKey { get; }

    /// <summary>
    ///     Gets the name used for this subscriber type in validation messages, e.g. "Service Bus".
    /// </summary>
    protected abstract string DisplayName { get; }

    /// <summary>
    ///     Checks that the subscriber does not mix a connection string with namespace credentials,
    ///     and that some credentials are available at subscriber or topic level.
    /// </summary>
    protected void ValidateNamespaceAuth()
    {
        // Validate authentication (considering topic-level defaults)
        var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);
        var hasTopicConnectionString = !string.IsNullOrWhiteSpace(TopicConnectionString);

        // Check if subscriber specifies both connection string and any namespace components
        if (hasConnectionString && HasAnySubscriberNamespaceCredential())
        {
            throw new ArgumentException(
                $"{DisplayName} subscriber '{Name}' should specify either connectionString or namespace credentials, not both."
            );
        }

        // Check if at least one authentication method is available (subscriber or topic level)
        if (
            !hasConnectionString
            && !HasSubscriberNamespaceCredentials()
            && !hasTopicConnectionString
            && !HasTopicNamespaceCredentials()
        )
        {
            throw new ArgumentException(
                $"{DisplayName} subscriber '{Name}' must have either a connectionString or namespace + sharedAccessKeyName + sharedAccessKey, either at subscriber or topic level."
            );
        }
    }

    /// <summary>
    ///     Validates each configured delivery property.
    /// </summary>
    protected void ValidateProperties()
    {
        if (Properties != null)
        {
            foreach (var (propertyName, propertySetting) in Properties)
            {
                propertySetting.Validate(propertyName);
            }
        }
    }

    private bool HasSubscriberNamespaceCredentials()
    {
        return !string.IsNullOrWhiteSpace(Namespace)
            && !string.IsNullOrWhiteSpace(SharedAccessKeyName)
            && !string.IsNullOrWhiteSpace(SharedAccessKey);
    }

    private bool HasAnySubscriberNamespaceCredential()
    {
        return !string.IsNullOrWhiteSpace(Namespace)
            || !string.IsNullOrWhiteSpace(SharedAccessKeyName)
            || !string.IsNullOrWhiteSpace(SharedAccessKey);
    }

    private bool HasTopicNamespaceCredentials()
    {
        return !string.IsNullOrWhiteSpace(TopicNamespace)
            && !string.IsNullOrWhiteSpace(TopicSharedAccessKeyName)
            && !string.IsNullOrWhiteSpace(TopicSharedAccessKey);
    }

    private static string BuildConnectionString(
        string namespaceName,
        string sharedAccessKeyName,
        string sharedAccessKey
    )
    {
        return $"Endpoint=sb://{namespaceName}.servicebus.windows.net/;SharedAccessKeyName={sharedAccessKeyName};SharedAccessKey={sharedAccessKey}";
    }
}
