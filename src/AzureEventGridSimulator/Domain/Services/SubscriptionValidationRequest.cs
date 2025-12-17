using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Domain.Services;

public class SubscriptionValidationRequest
{
    [JsonPropertyName("validationCode")]
    public Guid ValidationCode { get; set; }

    [JsonPropertyName("validationUrl")]
    public string ValidationUrl { get; set; }
}
