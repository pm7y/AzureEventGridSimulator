using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Domain.Services
{
    public class SubscriptionValidationService : IHostedService
    {
        private readonly SimulatorSettings _simulatorSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ValidationIpAddress _validationIpAddress;
        private readonly ILogger _logger;

        public SubscriptionValidationService(SimulatorSettings simulatorSettings,
                                             IHttpClientFactory httpClientFactory,
                                             ValidationIpAddress validationIpAddress,
                                             ILogger logger)
        {
            _simulatorSettings = simulatorSettings;
            _httpClientFactory = httpClientFactory;
            _validationIpAddress = validationIpAddress;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
#pragma warning disable 4014
            SendSubscriptionValidationEventToAllSubscriptions();
#pragma warning restore 4014
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task SendSubscriptionValidationEventToAllSubscriptions()
        {
            foreach (var topic in _simulatorSettings.Topics)
            {
                foreach (var subscription in topic.Subscribers)
                {
#pragma warning disable 4014
                    ValidateSubscription(topic, subscription);
#pragma warning restore 4014
                }
            }
        }

        private async Task<bool> ValidateSubscription(TopicSettings topic, SubscriptionSettings subscription)
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

                var json = JsonConvert.SerializeObject(new[] { evt }, Formatting.Indented);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Add("aeg-event-type", "SubscriptionValidation");
                    httpClient.Timeout = TimeSpan.FromSeconds(15);

                    subscription.ValidationStatus = SubscriptionValidationStatus.ValidationEventSent;

                    var response = await httpClient.PostAsync(subscription.Endpoint, content);
                    response.EnsureSuccessStatusCode();

                    var text = await response.Content.ReadAsStringAsync();
                    var validationResponse = JsonConvert.DeserializeObject<SubscriptionValidationResponse>(text);

                    if (validationResponse.ValidationResponse == subscription.ValidationCode)
                    {
                        subscription.ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful;
                        _logger.LogInformation("Successfully validated subscriber '{SubscriberName}'.", subscription.Name);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to receive validation event from subscriber '{SubscriberName}': {ErrorMessage}", subscription.Name, ex.Message);
                _logger.LogInformation("'{SubscriberName}' manual validation url: {ValidationUrl}", subscription.Name, validationUrl);
            }

            subscription.ValidationStatus = SubscriptionValidationStatus.ValidationFailed;
            return false;
        }
    }
}
