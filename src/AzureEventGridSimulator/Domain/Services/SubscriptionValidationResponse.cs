using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
///     Response for subscription validation.
/// </summary>
/// <param name="ValidationResponse">
///     The validation response code.
/// </param>
public record SubscriptionValidationResponse(
    [property: JsonPropertyName("validationResponse")] Guid ValidationResponse
);
