using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureEventGridSimulator.Domain.Commands;

// ReSharper disable once UnusedMember.Global
public class SendNotificationEventsToAzureServiceBusSubscriberCommandHandler : AsyncRequestHandler<SendNotificationEventsToSpecializedSubscriberCommand<AzureServiceBusSubscriptionSettings>>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public SendNotificationEventsToAzureServiceBusSubscriberCommandHandler(IHttpClientFactory httpClientFactory, ILogger<SendNotificationEventsToAzureServiceBusSubscriberCommandHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task Handle(SendNotificationEventsToSpecializedSubscriberCommand<AzureServiceBusSubscriptionSettings> request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Subscription.Disabled)
            {
                _logger.LogWarning("ASB subscription '{SubscriberName}' on topic '{TopicName}' is disabled and so Notification was skipped", request.Subscription.Name, request.TopicName);
                return;
            }

            _logger.LogDebug("Sending to ASB subscriber '{SubscriberName}' on topic '{TopicName}'", request.Subscription.Name, request.TopicName);

            var resourceUri = new Uri($"https://{request.Subscription.Namespace}.servicebus.windows.net/{request.Subscription.Topic}");
            var uri = new Uri(resourceUri, $"/{request.Subscription.Topic}/messages?timeout=60");

            foreach (var evt in request.Events)
            {
                if (!request.Subscription.Filter.AcceptsEvent(evt))
                {
                    _logger.LogDebug("Event {EventId} filtered out for ASB subscriber '{SubscriberName}'", evt.Id, request.Subscription.Name);
                    continue;
                }

                var messages = new[]
                {
                    CreateMessage(request.Subscription, evt)
                };

                var json = JsonConvert.SerializeObject(messages, Formatting.Indented);
                using var content = new StringContent(json, Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.microsoft.servicebus.json");

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", CreateToken(resourceUri.ToString(), request.Subscription.SharedAccessKeyName, request.Subscription.SharedAccessKey));
                httpClient.DefaultRequestHeaders.Add("Host", resourceUri.Host);

                httpClient.Timeout = TimeSpan.FromSeconds(60);

                await httpClient.PostAsync(uri, content, cancellationToken)
                     .ContinueWith(t => LogResult(t, evt, request.Subscription, request.TopicName));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send to ASB subscriber '{SubscriberName}'", request.Subscription.Name);
        }
    }

    public static Message CreateMessage(AzureServiceBusSubscriptionSettings subscription, EventGridEvent evt)
    {
        var obj = JObject.FromObject(evt);
        var msg = new Message
        {
            Body = obj.ToString()
        };

        foreach (var property in subscription.Properties)
        {
            var value = property.Value.PropertyType switch
            {
                AzureServiceBusSubscriptionSettings.PropertyType.Static => property.Value.Value,
                AzureServiceBusSubscriptionSettings.PropertyType.Dynamic => obj.SelectToken(property.Value.Value),
                _ => throw new NotImplementedException($"Unsupported property type '{property.Value.PropertyType}'")
            };

            if (value == null || value.Type == JTokenType.Null)
            {
                continue;
            }

            if (BrokerPropertyKeys.AllKeys.Contains(property.Key))
            {
                if (property.Key == BrokerPropertyKeys.MessageId && property.Value.PropertyType == AzureServiceBusSubscriptionSettings.PropertyType.Static)
                {
                    throw new InvalidOperationException($"'{property.Value.PropertyType}' is unsupported for property '{BrokerPropertyKeys.MessageId}'");
                }

                msg.BrokerProperties ??= new Dictionary<string, string>();
                msg.BrokerProperties[property.Key] = value.ToString();
                continue;
            }

            msg.UserProperties ??= new Dictionary<string, string>();
            msg.UserProperties[property.Key] = value.ToString();
        }

        if (msg.BrokerProperties.ContainsKey(BrokerPropertyKeys.SessionId))
        {
            if (msg.BrokerProperties.ContainsKey(BrokerPropertyKeys.MessageId))
            {
                // You can only set either SessionId or MessageId (https://learn.microsoft.com/en-us/azure/event-grid/delivery-properties)
                msg.BrokerProperties.Remove(BrokerPropertyKeys.MessageId);
            }
        }
        else
        {
            if (!msg.BrokerProperties.ContainsKey(BrokerPropertyKeys.MessageId))
            {
                // The default value of MessageId is the internal ID of the Event Grid event. You can override it (https://learn.microsoft.com/en-us/azure/event-grid/delivery-properties)
                msg.BrokerProperties.Add(BrokerPropertyKeys.MessageId, evt.Id);
            }
        }

        // set event properties
        msg.UserProperties[Constants.AegEventTypeHeader] = Constants.NotificationEventType;
        msg.UserProperties[Constants.AegSubscriptionNameHeader] = subscription.Name.ToUpperInvariant();
        msg.UserProperties[Constants.AegDataVersionHeader] = evt.DataVersion;
        msg.UserProperties[Constants.AegMetadataVersionHeader] = evt.MetadataVersion;
        msg.UserProperties[Constants.AegDeliveryCountHeader] = "0";

        return msg;
    }

    private static string CreateToken(string resourceUri, string keyName, string key)
    {
        const int min = 60;

        var sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
        var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + min);
        var stringToSign = $"{HttpUtility.UrlEncode(resourceUri)}\n{expiry}";
        var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        var sasToken = $"SharedAccessSignature sr={HttpUtility.UrlEncode(resourceUri)}&sig={HttpUtility.UrlEncode(signature)}&se={expiry}&skn={keyName}";
        return sasToken;
    }

    private void LogResult(Task<HttpResponseMessage> task, EventGridEvent evt, AzureServiceBusSubscriptionSettings subscription, string topicName)
    {
        if (task.IsCompletedSuccessfully && task.Result.IsSuccessStatusCode)
        {
            _logger.LogDebug("Event {EventId} sent to ASB subscriber '{SubscriberName}' on topic '{TopicName}' successfully", evt.Id, subscription.Name, topicName);
        }
        else
        {
            _logger.LogError(task.Exception?.GetBaseException(),
                             "Failed to send event {EventId} to ASB subscriber '{SubscriberName}', '{TaskStatus}', '{Reason}'",
                             evt.Id,
                             subscription.Name,
                             task.Status.ToString(),
                             task.Result?.ReasonPhrase);
        }
    }

    public sealed class BrokerPropertyKeys
    {
        public const string MessageId = "MessageId";
        public const string PartitionKey = "PartitionKey";
        public const string SessionId = "SessionId";
        public const string CorrelationId = "CorrelationId";
        public const string Label = "Label";
        public const string ReplyTo = "ReplyTo";
        public const string ReplyToSessionId = "ReplyToSessionId";
        public const string To = "To";
        public const string ViaPartitionKey = "ViaPartitionKey";

        public static IEnumerable<string> AllKeys => new[]
        {
            MessageId,
            PartitionKey,
            SessionId,
            CorrelationId,
            Label,
            ReplyTo,
            ReplyToSessionId,
            To,
            ViaPartitionKey
        };
    }

    public sealed class Message
    {
        [JsonProperty("Body")]
        public string Body { get; set; }

        [JsonProperty("BrokerProperties")]
        public Dictionary<string, string> BrokerProperties { get; set; } = new Dictionary<string, string>();

        [JsonProperty("UserProperties")]
        public Dictionary<string, string> UserProperties { get; set; } = new Dictionary<string, string>();
    }
}
