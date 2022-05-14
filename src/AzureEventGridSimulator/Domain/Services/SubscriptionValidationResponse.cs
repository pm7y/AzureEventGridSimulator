using System;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Services;

public class SubscriptionValidationResponse
{
    [JsonProperty(PropertyName = "validationResponse", Required = Required.Always)]
    public Guid ValidationResponse { get; set; }
}
