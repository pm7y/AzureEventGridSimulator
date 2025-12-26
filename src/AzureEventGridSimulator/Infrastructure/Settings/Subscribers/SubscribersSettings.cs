using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Container for all subscriber types.
/// </summary>
public class SubscribersSettings
{
    /// <summary>
    ///     Gets or sets HTTP webhook subscribers.
    /// </summary>
    [JsonPropertyName("http")]
    public HttpSubscriberSettings[]? Http { get; set; }

    /// <summary>
    ///     Gets or sets Azure Service Bus subscribers.
    /// </summary>
    [JsonPropertyName("serviceBus")]
    public ServiceBusSubscriberSettings[]? ServiceBus { get; set; }

    /// <summary>
    ///     Gets or sets Azure Storage Queue subscribers.
    /// </summary>
    [JsonPropertyName("storageQueue")]
    public StorageQueueSubscriberSettings[]? StorageQueue { get; set; }

    /// <summary>
    ///     Gets or sets Azure Event Hub subscribers.
    /// </summary>
    [JsonPropertyName("eventHub")]
    public EventHubSubscriberSettings[]? EventHub { get; set; }

    /// <summary>
    ///     Gets all subscribers of all types.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<ISubscriberSettings> All =>
        (Http ?? [])
            .Cast<ISubscriberSettings>()
            .Concat(ServiceBus ?? [])
            .Concat(StorageQueue ?? [])
            .Concat(EventHub ?? []);

    /// <summary>
    ///     Gets all HTTP subscribers.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<HttpSubscriberSettings> HttpSubscribers => Http ?? [];

    /// <summary>
    ///     Gets all Service Bus subscribers.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<ServiceBusSubscriberSettings> ServiceBusSubscribers => ServiceBus ?? [];

    /// <summary>
    ///     Gets all Storage Queue subscribers.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<StorageQueueSubscriberSettings> StorageQueueSubscribers =>
        StorageQueue ?? [];

    /// <summary>
    ///     Gets all Event Hub subscribers.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<EventHubSubscriberSettings> EventHubSubscribers => EventHub ?? [];

    /// <summary>
    ///     Gets whether there are any subscribers configured.
    /// </summary>
    [JsonIgnore]
    public bool Any => All.Any();

    /// <summary>
    ///     Gets the total count of subscribers.
    /// </summary>
    [JsonIgnore]
    public int Count => All.Count();

    public void Validate()
    {
        foreach (var subscriber in All)
            subscriber.Validate();

        // Check for duplicate names
        var names = All.Select(s => s.Name).ToList();
        var duplicates = names
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count != 0)
            throw new ArgumentException(
                $"Duplicate subscriber names found: {string.Join(", ", duplicates)}"
            );
    }
}
