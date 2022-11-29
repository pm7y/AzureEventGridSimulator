using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class SubscriberSettings
{
    [JsonProperty(PropertyName = "http", Required = Required.Default)]
    public HttpSubscriptionSettings[] Http { get; set; } = Array.Empty<HttpSubscriptionSettings>();

    [JsonProperty(PropertyName = "serviceBus", Required = Required.Default)]
    public AzureServiceBusSubscriptionSettings[] ServiceBus { get; set; } = Array.Empty<AzureServiceBusSubscriptionSettings>();

    internal IEnumerable<BaseSubscriptionSettings> AllSubscriptions => Http.Cast<BaseSubscriptionSettings>().Union(ServiceBus);

    internal void Validate()
    {
        foreach (var serviceBus in ServiceBus)
        {
            serviceBus.Validate();
        }
    }
}
