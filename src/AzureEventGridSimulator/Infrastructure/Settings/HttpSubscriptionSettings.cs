using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class HttpSubscriptionSettings : BaseSubscriptionSettings
{
    private readonly DateTime _expired = DateTime.UtcNow.AddMinutes(5);

    [JsonProperty(PropertyName = "endpoint", Required = Required.Always)]
    public string Endpoint { get; set; }

    [JsonProperty(PropertyName = "disableValidation", Required = Required.Default)]
    public bool DisableValidation { get; set; }

    [JsonIgnore]
    public SubscriptionValidationStatus ValidationStatus { get; set; }

    [JsonIgnore]
    public Guid ValidationCode => new(Encoding.UTF8.GetBytes(Endpoint).Reverse().Take(16).ToArray());

    [JsonIgnore]
    public bool ValidationPeriodExpired => DateTime.UtcNow > _expired;
}
