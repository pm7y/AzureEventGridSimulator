namespace AzureEventGridSimulator.Infrastructure.Settings;

using System;
using System.Linq;
using Newtonsoft.Json;

public class SubscriberSettings
{
    [JsonProperty(PropertyName = "http", Required = Required.Default)]
    public HttpSubscriptionSettings[] Http { get; set; } = Array.Empty<HttpSubscriptionSettings>();

    [JsonProperty(PropertyName = "serviceBus", Required = Required.Default)]
    public AzureServiceBusSubscriptionSettings[] ServiceBus { get; set; } = Array.Empty<AzureServiceBusSubscriptionSettings>();

    internal BaseSubscriptionSettings[] AllSubscriptions => Http.Cast<BaseSubscriptionSettings>().Union(ServiceBus).ToArray();

    internal void Validate()
    {
        foreach (var serviceBus in ServiceBus)
        {
            serviceBus.Validate();
        }
    }
}
