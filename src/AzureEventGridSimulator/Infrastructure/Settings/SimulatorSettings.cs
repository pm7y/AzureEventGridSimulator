using System.Text.Json.Serialization;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

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
        Normalize();

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

        if (Topics.Any(t => !IsValidResourceName(t.Name)))
        {
            throw new InvalidOperationException(
                "A topic name can only contain letters, numbers, and dashes."
            );
        }

        if (allSubscribers.Any(s => !IsValidResourceName(s.Name)))
        {
            throw new InvalidOperationException(
                "A subscriber name can only contain letters, numbers, and dashes."
            );
        }

        // Validate each subscriber (this also validates its filter, retry policy and dead-letter settings)
        foreach (var subscriber in allSubscribers)
        {
            subscriber.Validate();
        }

        // Validate dashboard port is determinable if dashboard is enabled
        if (DashboardEnabled && DashboardPort is null && !Topics.Any(t => !t.Disabled))
        {
            throw new InvalidOperationException(
                "Dashboard is enabled but no port is available. Either set 'dashboardPort' or enable at least one topic."
            );
        }
    }

    /// <summary>
    ///     Wires up parent-topic references and applies defaults. Runs as the first step of
    ///     <see cref="Validate" /> because subscriber validation reads <c>ParentTopic</c> to
    ///     resolve topic-level credentials.
    /// </summary>
    private void Normalize()
    {
        foreach (var topic in Topics)
        {
            // Wire up topic references for connection string inheritance
            foreach (var subscriber in topic.Subscribers.All.OfType<SubscriberSettingsBase>())
            {
                subscriber.ParentTopic = topic;
            }

            foreach (var subscriber in topic.Subscribers.All)
            {
                subscriber.DeadLetter?.ApplyDefaults();
            }
        }
    }

    private static bool IsValidResourceName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.All(c => char.IsLetterOrDigit(c) || c == '-');
}
