using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Container for all subscriber types.
/// </summary>
public class SubscribersSettings
{
    /// <summary>
    /// Gets or sets HTTP webhook subscribers.
    /// </summary>
    [JsonProperty(PropertyName = "http", Required = Required.Default)]
    public HttpSubscriberSettings[] Http { get; set; } = Array.Empty<HttpSubscriberSettings>();

    /// <summary>
    /// Gets or sets Azure Service Bus subscribers.
    /// </summary>
    [JsonProperty(PropertyName = "serviceBus", Required = Required.Default)]
    public ServiceBusSubscriberSettings[] ServiceBus { get; set; } =
        Array.Empty<ServiceBusSubscriberSettings>();

    /// <summary>
    /// Gets all subscribers of all types.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<ISubscriberSettings> All =>
        (Http ?? Array.Empty<HttpSubscriberSettings>())
            .Cast<ISubscriberSettings>()
            .Concat(ServiceBus ?? Array.Empty<ServiceBusSubscriberSettings>());

    /// <summary>
    /// Gets all HTTP subscribers.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<HttpSubscriberSettings> HttpSubscribers =>
        Http ?? Array.Empty<HttpSubscriberSettings>();

    /// <summary>
    /// Gets all Service Bus subscribers.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<ServiceBusSubscriberSettings> ServiceBusSubscribers =>
        ServiceBus ?? Array.Empty<ServiceBusSubscriberSettings>();

    /// <summary>
    /// Gets whether there are any subscribers configured.
    /// </summary>
    [JsonIgnore]
    public bool Any => All.Any();

    /// <summary>
    /// Gets the total count of subscribers.
    /// </summary>
    [JsonIgnore]
    public int Count => All.Count();

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

        if (duplicates.Any())
        {
            throw new ArgumentException(
                $"Duplicate subscriber names found: {string.Join(", ", duplicates)}"
            );
        }
    }
}
