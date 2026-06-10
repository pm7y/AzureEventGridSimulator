using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class SimulatorSettings
{
    [JsonPropertyName("topics")]
    public TopicSettings[] Topics { get; set; } = Array.Empty<TopicSettings>();

    /// <summary>
    ///     Enable or disable the dashboard. Defaults to true.
    /// </summary>
    [JsonPropertyName("dashboardEnabled")]
    public bool DashboardEnabled { get; set; } = true;

    /// <summary>
    ///     Optional port for the dashboard. If not set, dashboard is served on each topic's port.
    /// </summary>
    [JsonPropertyName("dashboardPort")]
    public int? DashboardPort { get; set; }

    /// <summary>
    ///     Configurable validation limits for events. If not specified, uses default Azure Event Grid limits.
    /// </summary>
    [JsonPropertyName("eventValidationLimits")]
    public EventValidationLimits EventValidationLimits { get; set; } = new();

    public void Validate()
    {
        if (Topics.GroupBy(o => o.Port).Count() != Topics.Length)
        {
            throw new InvalidOperationException("Each topic must use a unique port.");
        }

        if (Topics.GroupBy(o => o.Name).Count() != Topics.Length)
        {
            throw new InvalidOperationException("Each topic must have a unique name.");
        }

        var allSubscribers = Topics.SelectMany(o => o.Subscribers.All).ToList();

        // Subscriber names must be unique within a topic (case-insensitive, matching Azure);
        // the same name may be reused across different topics.
        foreach (var topic in Topics)
        {
            var duplicateNames = topic
                .Subscribers.All.GroupBy(s => s.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateNames.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Each subscriber on a topic must have a unique name. Duplicate name(s) on topic '{topic.Name}': {string.Join(", ", duplicateNames)}."
                );
            }
        }

        if (
            Topics
                .Select(t => t.Name)
                .Any(name =>
                    string.IsNullOrWhiteSpace(name)
                    || name.ToArray().Any(c => !(char.IsLetterOrDigit(c) || c == '-'))
                )
        )
        {
            throw new InvalidOperationException(
                "A topic name can only contain letters, numbers, and dashes."
            );
        }

        if (
            allSubscribers
                .Select(s => s.Name)
                .Any(name =>
                    string.IsNullOrWhiteSpace(name)
                    || name.ToArray().Any(c => !(char.IsLetterOrDigit(c) || c == '-'))
                )
        )
        {
            throw new InvalidOperationException(
                "A subscriber name can only contain letters, numbers, and dashes."
            );
        }

        // Wire up topic references for connection string inheritance
        foreach (var topic in Topics)
        {
            foreach (var subscriber in topic.Subscribers.ServiceBusSubscribers)
            {
                subscriber.ParentTopic = topic;
            }

            foreach (var subscriber in topic.Subscribers.StorageQueueSubscribers)
            {
                subscriber.ParentTopic = topic;
            }

            foreach (var subscriber in topic.Subscribers.EventHubSubscribers)
            {
                subscriber.ParentTopic = topic;
            }
        }

        // Validate each subscriber
        foreach (var subscriber in allSubscribers)
        {
            subscriber.Validate();
        }

        // Validate filters
        foreach (var filter in allSubscribers.Where(s => s.Filter != null).Select(s => s.Filter!))
        {
            filter.Validate();
        }

        // Validate dashboard port is determinable if dashboard is enabled
        if (DashboardEnabled && DashboardPort is null && !Topics.Any(t => !t.Disabled))
        {
            throw new InvalidOperationException(
                "Dashboard is enabled but no port is available. Either set 'dashboardPort' or enable at least one topic."
            );
        }
    }
}
