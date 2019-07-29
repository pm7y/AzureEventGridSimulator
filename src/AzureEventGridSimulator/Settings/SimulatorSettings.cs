using System;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Extensions;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Settings
{
    public class SimulatorSettings
    {
        [JsonProperty(PropertyName = "topics", Required = Required.Always)]
        public ICollection<TopicSettings> Topics { get; set; }

        public void Validate()
        {
            if (Topics.GroupBy(o => o.Port).Count() != Topics.Count)
            {
                throw new InvalidOperationException("Each topic must use a unique port.");
            }

            if (Topics.GroupBy(o => o.Name).Count() != Topics.Count)
            {
                throw new InvalidOperationException("Each topic must have a unique name.");
            }

            if (Topics.SelectMany(o => o.Subscribers ?? new SubscriptionSettings[0]).GroupBy(o => o.Name).Count() !=
                Topics.SelectMany(o => o.Subscribers ?? new SubscriptionSettings[0]).Count())
            {
                throw new InvalidOperationException("Each subscriber must have a unique name.");
            }

            // validate the filters
            foreach (var filter in Topics.Where(t => t.Subscribers.HasItems()).SelectMany(t => t.Subscribers.Where(s => s.Filter != null).Select(s => s.Filter)))
            {
                filter.Validate();
            }
        }
    }
}
