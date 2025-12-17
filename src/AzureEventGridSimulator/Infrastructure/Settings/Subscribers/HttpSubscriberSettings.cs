using System.Text;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Settings for HTTP webhook subscribers.
/// </summary>
public class HttpSubscriberSettings : ISubscriberSettings
{
    private readonly DateTime _expired = DateTime.UtcNow.AddMinutes(5);

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; }

    [JsonPropertyName("disableValidation")]
    public bool DisableValidation { get; set; }

    [JsonIgnore]
    public SubscriptionValidationStatus ValidationStatus { get; set; }

    [JsonIgnore]
    public Guid ValidationCode => GetValidationCode();

    [JsonIgnore]
    public bool ValidationPeriodExpired => DateTime.UtcNow > _expired;

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("filter")]
    public FilterSetting Filter { get; set; }

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
    public string SubscriberType => "http";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Subscriber name is required.", nameof(Name));
        }

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new ArgumentException(
                "Endpoint is required for HTTP subscribers.",
                nameof(Endpoint)
            );
        }

        if (
            !Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        )
        {
            throw new ArgumentException(
                "Endpoint must be a valid HTTP or HTTPS URL.",
                nameof(Endpoint)
            );
        }

        Filter?.Validate();
    }

    public Guid GetValidationCode()
    {
        return new Guid(
            Encoding.UTF8.GetBytes(Endpoint).AsEnumerable().Reverse().Take(16).ToArray()
        );
    }
}
