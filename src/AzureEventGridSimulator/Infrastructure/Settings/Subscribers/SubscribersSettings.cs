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

    private readonly object _httpMutationLock = new();

    private readonly object _storageQueueMutationLock = new();

    /// <summary>
    ///     Adds or replaces (by name, case-insensitive) an HTTP subscriber at runtime. Mutations use
    ///     copy-on-write under a lock so that the delivery path, which enumerates the subscriber
    ///     collection without locking, always sees a consistent snapshot.
    /// </summary>
    public void UpsertHttpSubscriber(HttpSubscriberSettings subscriber)
    {
        lock (_httpMutationLock)
        {
            var retained = (Http ?? [])
                .Where(s =>
                    !string.Equals(s.Name, subscriber.Name, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
            retained.Add(subscriber);
            Http = [.. retained];
        }
    }

    /// <summary>
    ///     Removes an HTTP subscriber by name (case-insensitive). Returns true if a subscriber was
    ///     removed.
    /// </summary>
    public bool RemoveHttpSubscriber(string name)
    {
        lock (_httpMutationLock)
        {
            var current = Http ?? [];
            var retained = current
                .Where(s => !string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (retained.Length == current.Length)
            {
                return false;
            }

            Http = retained;
            return true;
        }
    }

    /// <summary>
    ///     Adds or replaces (by name, case-insensitive) a Storage Queue subscriber at runtime. Same
    ///     copy-on-write-under-lock contract as <see cref="UpsertHttpSubscriber" />.
    /// </summary>
    public void UpsertStorageQueueSubscriber(StorageQueueSubscriberSettings subscriber)
    {
        lock (_storageQueueMutationLock)
        {
            var retained = (StorageQueue ?? [])
                .Where(s =>
                    !string.Equals(s.Name, subscriber.Name, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
            retained.Add(subscriber);
            StorageQueue = [.. retained];
        }
    }

    /// <summary>
    ///     Removes a Storage Queue subscriber by name (case-insensitive). Returns true if one was removed.
    /// </summary>
    public bool RemoveStorageQueueSubscriber(string name)
    {
        lock (_storageQueueMutationLock)
        {
            var current = StorageQueue ?? [];
            var retained = current
                .Where(s => !string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (retained.Length == current.Length)
            {
                return false;
            }
            StorageQueue = retained;
            return true;
        }
    }

    public void Validate()
    {
        foreach (var subscriber in All)
        {
            subscriber.Validate();
        }

        // Check for duplicate names
        var names = All.Select(s => s.Name).ToList();
        var duplicates = names
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count != 0)
        {
            throw new ArgumentException(
                $"Duplicate subscriber names found: {string.Join(", ", duplicates)}"
            );
        }
    }
}
