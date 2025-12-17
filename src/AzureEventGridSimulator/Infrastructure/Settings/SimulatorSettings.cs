using System;
using System.Linq;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class SimulatorSettings
{
    [JsonProperty(PropertyName = "topics", Required = Required.Always)]
    public TopicSettings[] Topics { get; set; } = Array.Empty<TopicSettings>();

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

        if (allSubscribers.GroupBy(o => o.Name).Count() != allSubscribers.Count)
        {
            throw new InvalidOperationException("Each subscriber must have a unique name.");
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

        // Validate each subscriber
        foreach (var subscriber in allSubscribers)
        {
            subscriber.Validate();
        }

        // Validate filters
        foreach (var filter in allSubscribers.Where(s => s.Filter != null).Select(s => s.Filter))
        {
            filter.Validate();
        }
    }
}
