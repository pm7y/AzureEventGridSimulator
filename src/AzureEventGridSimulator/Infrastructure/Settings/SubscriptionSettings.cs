using System.Text;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class SubscriptionSettings
{
    private readonly DateTime _expired = DateTime.UtcNow.AddMinutes(5);

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; }

    [JsonPropertyName("filter")]
    public FilterSetting Filter { get; set; }

    [JsonPropertyName("disableValidation")]
    public bool DisableValidation { get; set; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the delivery schema for events sent to this subscriber.
    /// If null, uses the topic's output schema or the original event schema.
    /// </summary>
    [JsonPropertyName("deliverySchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? DeliverySchema { get; set; }

    [JsonIgnore]
    public SubscriptionValidationStatus ValidationStatus { get; set; }

    [JsonIgnore]
    public Guid ValidationCode => GetValidationCode();

    [JsonIgnore]
    public bool ValidationPeriodExpired => DateTime.UtcNow > _expired;

    public Guid GetValidationCode()
    {
        return new Guid(
            Encoding.UTF8.GetBytes(Endpoint).AsEnumerable().Reverse().Take(16).ToArray()
        );
    }
}
