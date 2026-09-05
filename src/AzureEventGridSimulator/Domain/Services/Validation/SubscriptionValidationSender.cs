using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Validation;

/// <summary>
///     Performs the outbound Event Grid subscription-validation handshake against a single HTTP
///     subscriber: it sends a <c>SubscriptionValidationEvent</c> to the subscriber's endpoint and
///     marks the subscriber validated when the endpoint echoes the validation code. This mirrors the
///     handshake Azure performs when a webhook subscription is created, and is used both at boot and
///     when a subscription is created at runtime via the management API.
/// </summary>
public class SubscriptionValidationSender(
    ILogger<SubscriptionValidationSender> logger,
    IHttpClientFactory httpClientFactory,
    ValidationIpAddressProvider validationIpAddress,
    TimeProvider timeProvider
)
{
    public async Task ValidateAsync(
        TopicSettings topic,
        HttpSubscriberSettings subscription,
        CancellationToken cancellationToken
    )
    {
        var validationUrl =
            $"https://{validationIpAddress}:{topic.Port}/validate?id={subscription.ValidationCode}";

        try
        {
            logger.LogDebug(
                "Sending subscription validation event to subscriber '{SubscriberName}'",
                subscription.Name
            );

            var evt = new EventGridEvent
            {
                EventTime = timeProvider.GetUtcNow().ToString("o"),
                DataVersion = "1",
                EventType = "Microsoft.EventGrid.SubscriptionValidationEvent",
                Id = Guid.NewGuid().ToString(),
                Subject = "",
                MetadataVersion = "1",
                Data = new SubscriptionValidationRequest
                {
                    ValidationCode = subscription.ValidationCode,
                    ValidationUrl = validationUrl,
                },
            };

            var json = JsonSerializer.Serialize(
                new[] { evt },
                new JsonSerializerOptions { WriteIndented = true }
            );
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            // Use the named client so the optional DangerousAcceptAnyServerCertificateValidator applies
            using var httpClient = httpClientFactory.CreateClient(nameof(AzureEventGridSimulator));
            httpClient.DefaultRequestHeaders.Add(
                Constants.AegEventTypeHeader,
                Constants.ValidationEventType
            );
            httpClient.DefaultRequestHeaders.Add(
                Constants.AegSubscriptionNameHeader,
                subscription.Name.ToUpperInvariant()
            );
            httpClient.DefaultRequestHeaders.Add(Constants.AegDataVersionHeader, evt.DataVersion);
            httpClient.DefaultRequestHeaders.Add(
                Constants.AegMetadataVersionHeader,
                evt.MetadataVersion
            );
            httpClient.DefaultRequestHeaders.Add(Constants.AegDeliveryCountHeader, "0");
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            subscription.ValidationStatus = SubscriptionValidationStatus.ValidationEventSent;

            using var response = await httpClient.PostAsync(
                subscription.Endpoint,
                content,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            var validationResponse = JsonSerializer.Deserialize<SubscriptionValidationResponse>(
                text
            );

            if (
                validationResponse != null
                && validationResponse.ValidationResponse == subscription.ValidationCode
            )
            {
                subscription.ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful;
                logger.LogInformation(
                    "Successfully validated subscriber '{SubscriberName}'",
                    subscription.Name
                );
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to validate subscriber '{SubscriberName}'. Note that subscriber must be started before the simulator. Or you can disable validation for this subscriber via settings: '{Error}'",
                subscription.Name,
                ex.Message
            );
            logger.LogInformation(
                "'{SubscriberName}' manual validation url: {ValidationUrl}",
                subscription.Name,
                validationUrl
            );
        }

        subscription.ValidationStatus = SubscriptionValidationStatus.ValidationFailed;
    }
}
