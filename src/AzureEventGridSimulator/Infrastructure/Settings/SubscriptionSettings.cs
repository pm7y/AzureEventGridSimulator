using System.Text;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class SubscriptionSettings
{
    private readonly DateTimeOffset _createdAt = DateTimeOffset.UtcNow;

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("endpoint")]
    public required string Endpoint { get; init; }

    [JsonPropertyName("filter")]
    public FilterSetting? Filter { get; init; }

    [JsonPropertyName("disableValidation")]
    public bool DisableValidation { get; init; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }

    /// <summary>
    /// Gets or sets the delivery schema for events sent to this subscriber.
    /// If null, uses the topic's output schema or the original event schema.
    /// </summary>
    [JsonPropertyName("deliverySchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? DeliverySchema { get; init; }

    [JsonIgnore]
    public SubscriptionValidationStatus ValidationStatus { get; set; }

    [JsonIgnore]
    public Guid ValidationCode => GetValidationCode();

    /// <summary>
    /// Determines if the validation period has expired.
    /// </summary>
    /// <param name="now" >
    /// The current UTC time.
    /// </param>
    /// <returns>
    /// True if the 5-minute validation window has expired.
    /// </returns>
    public bool ValidationPeriodExpired(DateTimeOffset now)
    {
        return now > _createdAt.AddMinutes(5);
    }

    public Guid GetValidationCode()
    {
        return new Guid(
            Encoding.UTF8.GetBytes(Endpoint).AsEnumerable().Reverse().Take(16).ToArray()
        );
    }
}
