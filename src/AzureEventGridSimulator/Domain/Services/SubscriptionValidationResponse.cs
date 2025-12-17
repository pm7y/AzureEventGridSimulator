using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Domain.Services;

public class SubscriptionValidationResponse
{
    [JsonPropertyName("validationResponse")]
    public Guid ValidationResponse { get; set; }
}
