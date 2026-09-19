using System.Text;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
///     Settings for HTTP webhook subscribers.
/// </summary>
public class HttpSubscriberSettings : SubscriberSettingsBase
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

    [JsonIgnore]
    public override string SubscriberType => "http";

    public override void Validate()
    {
        ValidateName();

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

        ValidateCommonTail();
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
