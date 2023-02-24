﻿using System;
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

namespace AzureEventGridSimulator.Domain.Commands.Http;

// ReSharper disable once UnusedMember.Global
public class ValidateAllHttpSubscriptionsCommandHandler : IRequestHandler<ValidateAllHttpSubscriptionsCommand>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ValidateAllHttpSubscriptionsCommandHandler> _logger;
    private readonly SimulatorSettings _simulatorSettings;
    private readonly ValidationIpAddressProvider _validationIpAddress;

    public ValidateAllHttpSubscriptionsCommandHandler(ILogger<ValidateAllHttpSubscriptionsCommandHandler> logger,
                                                  IHttpClientFactory httpClientFactory,
                                                  SimulatorSettings simulatorSettings,
                                                  ValidationIpAddressProvider validationIpAddress)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _simulatorSettings = simulatorSettings;
        _validationIpAddress = validationIpAddress;
    }

    public async Task<Unit> Handle(ValidateAllHttpSubscriptionsCommand request, CancellationToken cancellationToken)
    {
        foreach (var enabledTopic in _simulatorSettings.Topics
                                                       .Where(o => !o.Disabled))
        {
            foreach (var subscriber in enabledTopic.Subscribers.Http
                                                   .Where(o => !o.DisableValidation && !o.Disabled))
            {
                await ValidateSubscription(enabledTopic, subscriber);
            }
        }

        return Unit.Value;
    }

    private async Task ValidateSubscription(TopicSettings topic, HttpSubscriptionSettings subscription)
    {
        var validationUrl = $"https://{_validationIpAddress}:{topic.Port}/validate?id={subscription.ValidationCode}";

        try
        {
            _logger.LogDebug("Sending subscription validation event to subscriber '{SubscriberName}'", subscription.Name);

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
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add(Constants.AegEventTypeHeader, Constants.ValidationEventType);
            httpClient.DefaultRequestHeaders.Add(Constants.AegSubscriptionNameHeader, subscription.Name.ToUpperInvariant());
            httpClient.DefaultRequestHeaders.Add(Constants.AegDataVersionHeader, evt.DataVersion);
            httpClient.DefaultRequestHeaders.Add(Constants.AegMetadataVersionHeader, evt.MetadataVersion);
            httpClient.DefaultRequestHeaders.Add(Constants.AegDeliveryCountHeader, "0"); // TODO implement re-tries
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            subscription.ValidationStatus = SubscriptionValidationStatus.ValidationEventSent;

            var response = await httpClient.PostAsync(subscription.Endpoint, content);
            response.EnsureSuccessStatusCode();

            var text = await response.Content.ReadAsStringAsync();
            var validationResponse = JsonConvert.DeserializeObject<SubscriptionValidationResponse>(text);

            if (validationResponse != null && validationResponse.ValidationResponse == subscription.ValidationCode)
            {
                subscription.ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful;
                _logger.LogInformation("Successfully validated subscriber '{SubscriberName}'", subscription.Name);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to validate subscriber '{SubscriberName}'. Note that subscriber must be started before the simulator. Or you can disable validation for this subscriber via settings: '{Error}'",
                             subscription.Name, ex.Message);
            _logger.LogInformation("'{SubscriberName}' manual validation url: {ValidationUrl}", subscription.Name, validationUrl);
        }

        subscription.ValidationStatus = SubscriptionValidationStatus.ValidationFailed;
    }
}
