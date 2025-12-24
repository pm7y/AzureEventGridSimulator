using System.Text;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Settings for HTTP webhook subscribers.
/// </summary>
public class HttpSubscriberSettings : ISubscriberSettings
{
    private readonly DateTimeOffset _createdAt = DateTimeOffset.UtcNow;

    [JsonPropertyName("endpoint")]
    public required string Endpoint { get; init; }

    [JsonPropertyName("disableValidation")]
    public bool DisableValidation { get; init; }

    [JsonIgnore]
    public SubscriptionValidationStatus ValidationStatus { get; set; }

    [JsonIgnore]
    public Guid ValidationCode => GetValidationCode();

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("filter")]
    public FilterSetting? Filter { get; init; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }

    /// <summary>
    /// Gets or sets the delivery schema for events sent to this subscriber.
    /// If null, uses the topic's output schema or the original event schema.
    /// </summary>
    [JsonPropertyName("deliverySchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? DeliverySchema { get; init; }

    /// <summary>
    /// Gets or sets the retry policy for this subscriber.
    /// If null, default Azure Event Grid retry behavior is used (enabled with 30 attempts, 24h TTL).
    /// </summary>
    [JsonPropertyName("retryPolicy")]
    public RetryPolicySettings? RetryPolicy { get; init; }

    /// <summary>
    /// Gets or sets the dead-letter settings for this subscriber.
    /// Events that cannot be delivered are written to the dead-letter destination.
    /// </summary>
    [JsonPropertyName("deadLetter")]
    public DeadLetterSettings? DeadLetter { get; init; }

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
        RetryPolicy?.Validate();
        DeadLetter?.Validate();
    }

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
