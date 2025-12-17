using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using JetBrains.Annotations;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
[UsedImplicitly]
public class ValidateAllSubscriptionsCommandHandler(
    ILogger<ValidateAllSubscriptionsCommandHandler> logger,
    IHttpClientFactory httpClientFactory,
    SimulatorSettings simulatorSettings,
    ValidationIpAddressProvider validationIpAddress
) : IRequestHandler<ValidateAllSubscriptionsCommand>
{
    public async Task Handle(
        ValidateAllSubscriptionsCommand request,
        CancellationToken cancellationToken
    )
    {
        foreach (var enabledTopic in simulatorSettings.Topics.Where(o => !o.Disabled))
        {
            // Only HTTP subscribers need validation (Service Bus subscribers don't use webhook validation)
            foreach (
                var subscriber in enabledTopic.Subscribers.HttpSubscribers.Where(o =>
                    !o.DisableValidation && !o.Disabled
                )
            )
            {
                await ValidateSubscription(enabledTopic, subscriber);
            }
        }
    }

    private async Task ValidateSubscription(
        TopicSettings topic,
        HttpSubscriberSettings subscription
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
                EventTime = DateTime.UtcNow.ToString("O"),
                DataVersion = "1",
                EventType = "Microsoft.EventGrid.SubscriptionValidationEvent",
                Id = Guid.NewGuid().ToString(),
                Subject = "",
                MetadataVersion = "1",
                Data = new SubscriptionValidationRequest
                {
                    ValidationCode = subscription.ValidationCode,
                    ValidationUrl =
                        $"https://{validationIpAddress}:{topic.Port}/validate?id={subscription.ValidationCode}",
                },
            };

            var json = JsonSerializer.Serialize(
                new[] { evt },
                new JsonSerializerOptions { WriteIndented = true }
            );
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var httpClient = httpClientFactory.CreateClient();
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
            httpClient.DefaultRequestHeaders.Add(Constants.AegDeliveryCountHeader, "0"); // TODO implement re-tries
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            subscription.ValidationStatus = SubscriptionValidationStatus.ValidationEventSent;

            var response = await httpClient.PostAsync(subscription.Endpoint, content);
            response.EnsureSuccessStatusCode();

            var text = await response.Content.ReadAsStringAsync();
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
