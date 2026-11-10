using System.Globalization;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
///     The Azure Event Grid Simulator, run as a container from its published image.
/// </summary>
/// <param name="name">The name of the resource.</param>
public sealed class AzureEventGridSimulatorResource(string name) : ContainerResource(name)
{
    internal const string Registry = "docker.io";
    internal const string Image = "pmcilreavy/azureeventgridsimulator";

    private readonly List<AzureEventGridSimulatorTopicResource> _topics = [];

    /// <summary>
    ///     Gets the topics declared on this simulator.
    /// </summary>
    public IReadOnlyList<AzureEventGridSimulatorTopicResource> Topics => _topics;

    internal void AddTopic(AzureEventGridSimulatorTopicResource topic) => _topics.Add(topic);

    /// <summary>
    ///     Writes every topic and subscriber as an AEGS_ environment variable, the simulator's
    ///     own configuration format. Reference values resolve when the simulator starts.
    /// </summary>
    internal void WriteConfiguration(EnvironmentCallbackContext context)
    {
        for (var topicIndex = 0; topicIndex < _topics.Count; topicIndex++)
        {
            var topic = _topics[topicIndex];
            var topicPrefix = $"AEGS_topics__{topicIndex}__";

            context.EnvironmentVariables[$"{topicPrefix}name"] = topic.Name;
            context.EnvironmentVariables[$"{topicPrefix}port"] = topic.Port.ToString(
                CultureInfo.InvariantCulture
            );

            if (topic.Key is not null)
            {
                context.EnvironmentVariables[$"{topicPrefix}key"] = topic.Key;
            }

            foreach (
                var section in topic.Subscribers.GroupBy(s => s.Section, StringComparer.Ordinal)
            )
            {
                var subscriberIndex = 0;
                foreach (var subscriber in section)
                {
                    var prefix = $"{topicPrefix}subscribers__{section.Key}__{subscriberIndex++}__";
                    foreach (var (setting, value) in subscriber.Settings)
                    {
                        context.EnvironmentVariables[$"{prefix}{setting}"] = value;
                    }
                }
            }
        }
    }
}
