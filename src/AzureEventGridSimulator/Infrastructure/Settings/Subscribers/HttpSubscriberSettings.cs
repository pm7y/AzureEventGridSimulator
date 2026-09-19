using System.Text;
using System.Text.Json.Serialization;
using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Settings for HTTP webhook subscribers.
/// </summary>
public class HttpSubscriberSettings : ISubscriberSettings
{
    private readonly DateTimeOffset _createdAt = DateTimeOffset.UtcNow;

    // Computed once on first read (Endpoint is init-only, so the code never changes).
    // Lazy rather than a Guid? field: the /validate handler reads this from request threads
    // and a Nullable<Guid> write is not atomic. PublicationOnly keeps today's behaviour of
    // not caching a failure (e.g. a null Endpoint throws again on the next read).
    private readonly Lazy<Guid> _validationCode;

    public HttpSubscriberSettings()
    {
        _validationCode = new Lazy<Guid>(GetValidationCode, LazyThreadSafetyMode.PublicationOnly);
    }

    [JsonPropertyName("endpoint")]
    public required string Endpoint { get; init; }

    [JsonPropertyName("disableValidation")]
    public bool DisableValidation { get; init; }

    [JsonIgnore]
    public SubscriptionValidationStatus ValidationStatus { get; set; }

    [JsonIgnore]
    public Guid ValidationCode => _validationCode.Value;

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("filter")]
    public FilterSetting? Filter { get; init; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }

    /// <summary>
    ///     Gets or sets the delivery schema for events sent to this subscriber.
    ///     If null, uses the topic's output schema or the original event schema.
    /// </summary>
    [JsonPropertyName("deliverySchema")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EventSchema? DeliverySchema { get; init; }

    /// <summary>
    ///     Gets or sets the retry policy for this subscriber.
    ///     If null, default Azure Event Grid retry behavior is used (enabled with 30 attempts, 24h TTL).
    /// </summary>
    [JsonPropertyName("retryPolicy")]
    public RetryPolicySettings? RetryPolicy { get; init; }

    /// <summary>
    ///     Gets or sets the dead-letter settings for this subscriber.
    ///     Events that cannot be delivered are written to the dead-letter destination.
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
    ///     Determines if the validation period has expired.
    /// </summary>
    /// <param name="now">
    ///     The current UTC time.
    /// </param>
    /// <returns>
    ///     True if the 5-minute validation window has expired.
    /// </returns>
    public bool ValidationPeriodExpired(DateTimeOffset now)
    {
        return now > _createdAt.AddMinutes(5);
    }

    public Guid GetValidationCode()
    {
        // Derive a stable code from a hash of the full endpoint. Building the Guid from raw
        // endpoint bytes crashes for endpoints shorter than 16 UTF-8 bytes and collides for
        // endpoints sharing the same trailing bytes.
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Endpoint));
        return new Guid(hash.AsSpan(0, 16));
    }
}
