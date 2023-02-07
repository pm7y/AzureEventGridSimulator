namespace AzureEventGridSimulator.Domain.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

// ReSharper disable once UnusedMember.Global
public abstract class SendNotificationEventsToAzureServiceBusSubscriberCommandHandler<TEvent> : AsyncRequestHandler<SendNotificationEventsToSpecializedSubscriberCommand<AzureServiceBusSubscriptionSettings, TEvent>>
    where TEvent : IEvent
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public SendNotificationEventsToAzureServiceBusSubscriberCommandHandler(ILogger logger, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task Handle(SendNotificationEventsToSpecializedSubscriberCommand<AzureServiceBusSubscriptionSettings, TEvent> request, CancellationToken cancellationToken)
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

                var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
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

    internal Message CreateMessage(AzureServiceBusSubscriptionSettings subscription, TEvent evt)
    {
        var msg = new Message
        {
            Body = JsonSerializer.Serialize(evt)
        };

        // System.Text.Json currently does not have support for JsonPath. Fall back to Newtonsoft.
        var obj = JObject.FromObject(evt);
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

        AddAegProperties(ref msg, subscription, evt);
        return msg;
    }

    protected abstract void AddAegProperties(ref Message message, AzureServiceBusSubscriptionSettings subscription, TEvent evt);

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

    private void LogResult(Task<HttpResponseMessage> task, TEvent evt, AzureServiceBusSubscriptionSettings subscription, string topicName)
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
        [JsonPropertyName("Body")]
        public string Body { get; set; }

        [JsonPropertyName("BrokerProperties")]
        public Dictionary<string, string> BrokerProperties { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("UserProperties")]
        public Dictionary<string, string> UserProperties { get; set; } = new Dictionary<string, string>();
    }
}
