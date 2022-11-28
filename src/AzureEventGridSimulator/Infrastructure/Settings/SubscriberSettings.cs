using System;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class SubscriberSettings
{
    [JsonProperty(PropertyName = "http", Required = Required.Default)]
    public HttpSubscriptionSettings[] Http { get; set; } = Array.Empty<HttpSubscriptionSettings>();

    internal BaseSubscriptionSettings[] AllSubscriptions => Http;
}
