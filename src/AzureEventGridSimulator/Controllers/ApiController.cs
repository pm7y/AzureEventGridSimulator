using System;
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

        public ApiController(ILogger logger)
        {
            _logger = logger;
        }

        public TopicSettings TopicSettings => HttpContext.RetrieveTopicSettings();

        [HttpPost]
        public IActionResult Post()
        {
            var events = HttpContext.RetrieveEvents();

            _logger.LogInformation("New request ({EventCount} event(s)) for '{TopicName}' @ {RequestUrl}", events.Length, TopicSettings.Name, Request.GetDisplayUrl());

            foreach (var subscription in TopicSettings.Subscribers)
            {
#pragma warning disable 4014
                SendToSubscriber(subscription, events);
#pragma warning restore 4014
            }

            return Ok();
        }

        private async Task SendToSubscriber(SubscriptionSettings subscription, EventGridEvent[] events)
        {
            try
            {
                _logger.LogDebug("Sending to subscriber '{SubscriberName}'.", subscription.Name);

                // "Event Grid sends the events to subscribers in an array that has a single event. This behavior may change in the future."
                // https://docs.microsoft.com/en-us/azure/event-grid/event-schema
                foreach (var evt in events)
                {
                    var json = JsonConvert.SerializeObject(new[] { evt }, Formatting.Indented);
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Add("aeg-event-type", "Notification");
                        httpClient.Timeout = TimeSpan.FromSeconds(5);

                        await httpClient.PostAsync(subscription.Endpoint, content)
                                        .ContinueWith(t =>
                                        {
                                            if (t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode)
                                            {
                                                _logger.LogDebug(
                                                                 "Event {EventId} sent to subscriber '{SubscriberName}' successfully.", evt.Id, subscription.Name);
                                            }
                                            else
                                            {
                                                _logger.LogError(t.Exception?.GetBaseException(),
                                                                 "Failed to send event {EventId} to subscriber '{SubscriberName}', '{TaskStatus}', '{Reason}'.", evt.Id,
                                                                 subscription.Name,
                                                                 t.Status.ToString(),
                                                                 t.Result?.ReasonPhrase);
                                            }
                                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                                 "Failed to send to subscriber '{SubscriberName}'.", subscription.Name);
            }
        }
    }
}
