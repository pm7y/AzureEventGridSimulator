using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings
{
    public class SubscriptionSettings
    {
        private DateTime _expired = DateTime.UtcNow.AddMinutes(5);

        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "endpoint", Required = Required.Always)]
        public string Endpoint { get; set; }

        [JsonProperty(PropertyName = "filter", Required = Required.Default)]
        public FilterSetting Filter { get; set; }

        [JsonIgnore]
        public SubscriptionValidationStatus ValidationStatus { get; set; }

        [JsonIgnore]
        public Guid ValidationCode => new Guid(Encoding.UTF8.GetBytes(Endpoint).Reverse().Take(16).ToArray());

        [JsonIgnore]
        public bool ValidationPeriodExpired => DateTime.UtcNow > _expired;
    }
}
