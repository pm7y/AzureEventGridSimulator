using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Settings
{
    public class SimulatorSettings
    {
        [JsonProperty(PropertyName = "topics", Required = Required.Always)]
        public ICollection<TopicSettings> Topics { get; set; }

        public void Validate()
        {
            if (Topics.GroupBy(o => o.Port).Count() != Topics.Count())
            {
                throw new InvalidOperationException("Each topic must use a unique port.");
            }

            if (Topics.GroupBy(o => o.Name).Count() != Topics.Count())
            {
                throw new InvalidOperationException("Each topic must have a unique name.");
            }

            if (Topics.SelectMany(o => o.Subscribers ?? new List<SubscriptionSettings>()).GroupBy(o => o.Name).Count() !=
                Topics.SelectMany(o => o.Subscribers ?? new List<SubscriptionSettings>()).Count())
            {
                throw new InvalidOperationException("Each subscriber must have a unique name.");
            }
        }
    }
}
