using System;
using System.Linq;
using System.Text;
using AzureEventGridSimulator.Domain.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Settings for HTTP webhook subscribers.
/// </summary>
public class HttpSubscriberSettings : ISubscriberSettings
{
    private readonly DateTime _expired = DateTime.UtcNow.AddMinutes(5);

    [JsonProperty(PropertyName = "name", Required = Required.Always)]
    public string Name { get; set; }

    [JsonProperty(PropertyName = "endpoint", Required = Required.Always)]
    public string Endpoint { get; set; }

    [JsonProperty(PropertyName = "filter", Required = Required.Default)]
    public FilterSetting Filter { get; set; }

    [JsonProperty(PropertyName = "disableValidation", Required = Required.Default)]
    public bool DisableValidation { get; set; }

    [JsonProperty(PropertyName = "disabled", Required = Required.Default)]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the delivery schema for events sent to this subscriber.
    /// If null, uses the topic's output schema or the original event schema.
    /// </summary>
    [JsonProperty(PropertyName = "deliverySchema", Required = Required.Default)]
    [JsonConverter(typeof(StringEnumConverter))]
    public EventSchema? DeliverySchema { get; set; }

    [JsonIgnore]
    public string SubscriberType => "http";

    [JsonIgnore]
    public SubscriptionValidationStatus ValidationStatus { get; set; }

    [JsonIgnore]
    public Guid ValidationCode => GetValidationCode();

    [JsonIgnore]
    public bool ValidationPeriodExpired => DateTime.UtcNow > _expired;

    public Guid GetValidationCode()
    {
        return new Guid(Encoding.UTF8.GetBytes(Endpoint).Reverse().Take(16).ToArray());
    }

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
}
