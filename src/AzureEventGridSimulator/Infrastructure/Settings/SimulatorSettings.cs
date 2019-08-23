using System;
using System.Linq;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings
{
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

            if (Topics.SelectMany(o => o.Subscribers).GroupBy(o => o.Name).Count() !=
                Topics.SelectMany(o => o.Subscribers).Count())
            {
                throw new InvalidOperationException("Each subscriber must have a unique name.");
            }

            if (Topics.Select(t => t.Name).Concat(Topics.SelectMany(t => t.Subscribers).Select(s => s.Name)).Any(name => string.IsNullOrWhiteSpace(name) || name.ToArray().Any(c => !(char.IsLetterOrDigit(c) || c == '-'))))
            {
                throw new InvalidOperationException("A topic/subscriber name can only contain letters, numbers, and dashes.");
            }

            // validate the filters
            foreach (var filter in Topics.Where(t => t.Subscribers.Any()).SelectMany(t => t.Subscribers.Where(s => s.Filter != null).Select(s => s.Filter)))
            {
                filter.Validate();
            }
        }
    }
}
