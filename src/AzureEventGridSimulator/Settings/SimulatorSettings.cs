using System.Collections.Generic;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Settings
{
    public class SimulatorSettings
    {
        [JsonProperty(Required = Required.Always)]
        public ICollection<TopicSettings> Topics { get; set; }
    }
}
