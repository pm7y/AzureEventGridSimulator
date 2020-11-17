using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Commands
{
    public class ValidateAllSubscriptionsCommandHandler : IRequestHandler<ValidateAllSubscriptionsCommand>
    {
        private readonly ILogger<ValidateAllSubscriptionsCommandHandler> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SimulatorSettings _simulatorSettings;
        private readonly ValidationIpAddress _validationIpAddress;

        public ValidateAllSubscriptionsCommandHandler(ILogger<ValidateAllSubscriptionsCommandHandler> logger,
                                                      IHttpClientFactory httpClientFactory,
                                                      SimulatorSettings simulatorSettings,
                                                      ValidationIpAddress validationIpAddress)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _simulatorSettings = simulatorSettings;
            _validationIpAddress = validationIpAddress;
        }

        public async Task<Unit> Handle(ValidateAllSubscriptionsCommand request, CancellationToken cancellationToken)
        {
            foreach (var enabledTopic in _simulatorSettings.Topics
                                                           .Where(o => !o.Disabled))
            {
                foreach (var subscriber in enabledTopic.Subscribers
                                                       .Where(o => !o.Disabled))
                {
                    await ValidateSubscription(enabledTopic, subscriber);
                }
            }

            return Unit.Value;
        }

        private async Task ValidateSubscription(TopicSettings topic, SubscriptionSettings subscription)
        {
            var validationUrl = $"https://{_validationIpAddress}:{topic.Port}/validate?id={subscription.ValidationCode}";

            try
            {
                _logger.LogDebug("Sending subscription validation event to subscriber '{SubscriberName}'.", subscription.Name);

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
                        ValidationUrl = $"https://{_validationIpAddress}:{topic.Port}/validate?id={subscription.ValidationCode}"
                    }
                };

                // ReSharper disable once MethodHasAsyncOverload
                var json = JsonConvert.SerializeObject(new[] { evt }, Formatting.Indented);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("aeg-event-type", "SubscriptionValidation");
                httpClient.DefaultRequestHeaders.Add("aeg-subscription-name", subscription.Name.ToUpperInvariant());
                httpClient.DefaultRequestHeaders.Add("aeg-data-version", evt.DataVersion);
                httpClient.DefaultRequestHeaders.Add("aeg-metadata-version", evt.MetadataVersion);
                httpClient.DefaultRequestHeaders.Add("aeg-delivery-count", "0"); // TODO implement re-tries
                httpClient.Timeout = TimeSpan.FromSeconds(60);

                subscription.ValidationStatus = SubscriptionValidationStatus.ValidationEventSent;

                var response = await httpClient.PostAsync(subscription.Endpoint, content);
                response.EnsureSuccessStatusCode();

                var text = await response.Content.ReadAsStringAsync();
                var validationResponse = JsonConvert.DeserializeObject<SubscriptionValidationResponse>(text);

                if (validationResponse.ValidationResponse == subscription.ValidationCode)
                {
                    subscription.ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful;
                    _logger.LogInformation("Successfully validated subscriber '{SubscriberName}'.", subscription.Name);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to validate subscriber '{SubscriberName}'. Note that subscriber must be started before the simulator. Or you can disable validation for this subscriber via settings: '{Error}'", subscription.Name, ex.Message);
            }

            subscription.ValidationStatus = SubscriptionValidationStatus.ValidationFailed;
        }

    }
}
