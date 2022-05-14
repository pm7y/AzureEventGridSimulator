using System;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Services;

public class SubscriptionValidationRequest
{
    [JsonProperty(PropertyName = "validationCode", Required = Required.Always)]
    public Guid ValidationCode { get; set; }

    [JsonProperty(PropertyName = "validationUrl", Required = Required.Default)]
    public string ValidationUrl { get; set; }
}
