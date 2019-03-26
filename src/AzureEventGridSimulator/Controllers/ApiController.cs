using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Controllers
{
    [Route("/api/events")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ILogger _logger;

        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true,
            ClientCertificateOptions = ClientCertificateOption.Manual
        });

        public ApiController(ILogger logger)
        {
            _logger = logger;
        }

        public TopicSettings TopicSettings => HttpContext.RetrieveTopicSettings();

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            var events = HttpContext.RetrieveEvents();

            _logger.LogInformation($"New request ({events.Length} event(s)) for '{TopicSettings.Name}' @ {Request.GetDisplayUrl()}");

            foreach (var subscription in TopicSettings.Subscribers)
            {
                await SendToSubscriber(subscription, events);
            }

            return Ok();
        }

        private async Task SendToSubscriber(SubscriptionSettings subscription, EventGridEvent[] events)
        {
            try
            {
                _logger.LogDebug($"Sending to subscriber '{subscription.Name}'.");

                // "Event Grid sends the events to subscribers in an array that has a single event. This behavior may change in the future."
                // https://docs.microsoft.com/en-us/azure/event-grid/event-schema
                foreach (var evt in events)
                {
                    if (string.IsNullOrWhiteSpace(evt.EventType) && !subscription.EventTypes.Any(et => string.Equals(et, evt.EventType, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        _logger.LogDebug($"Skip '{subscription.Name}' for type of {evt.EventType}.");
                        continue;
                    }

                    var json = JsonConvert.SerializeObject(new[] { evt }, Formatting.Indented);
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    {
                        Client.DefaultRequestHeaders.Add("aeg-event-type", "Notification");
                        Client.Timeout = TimeSpan.FromSeconds(5);

                        void ContinuationAction(Task<HttpResponseMessage> t)
                        {
                            if (t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode)
                            {
                                _logger.LogDebug($"Event {evt.Id} sent to subscriber '{subscription.Name}' successfully.");
                            }
                            else
                            {
                                _logger.LogError(t.Exception?.GetBaseException(), $"Failed to send event {evt.Id} to subscriber '{subscription.Name}', '{t.Status}', '{t.Result?.ReasonPhrase}'.");
                            }
                        }

                        await Client
                              .PostAsync(subscription.Endpoint, content)
                              .ContinueWith(ContinuationAction);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send to subscriber '{SubscriberName}'.", subscription.Name);
            }
        }
    }
}
