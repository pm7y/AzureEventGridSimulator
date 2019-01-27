using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;

namespace AzureEventGridSimulator.Controllers
{
    [Route("/api/events")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly SimulatorSettings _simulatorSettings;

        public ApiController(ILogger logger,
                             SimulatorSettings simulatorSettings)
        {
            _logger = logger;
            _simulatorSettings = simulatorSettings;
        }

        public TopicSettings TopicSettings => _simulatorSettings.Topics.First(t => t.HttpsPort == HttpContext.Connection.LocalPort);

        [HttpPost]
        public IActionResult Post([FromBody] EventGridEvent[] events)
        {
            _logger.Information("New request ({EventCount} event(s)) for '{TopicName}' @ {RequestUrl}", events.Length, TopicSettings.Name, Request.GetDisplayUrl());

            var formattedJson = JsonConvert.SerializeObject(events);

            foreach (var subscription in TopicSettings.Subscribers)
            {
#pragma warning disable 4014
                SendToSubscriber(subscription, formattedJson);
#pragma warning restore 4014
            }

            return Ok();
        }

        private async Task SendToSubscriber(SubscriptionSettings subscription, string json)
        {
            try
            {
                _logger.Debug($"Sending to subscriber '{subscription.Name}'");

                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("aeg-event-type", "Notification");
                    httpClient.Timeout = TimeSpan.FromSeconds(5);

                    await httpClient.PostAsync(subscription.Endpoint, content)
                                    .ContinueWith(t =>
                                    {
                                        if (t.IsCompletedSuccessfully)
                                        {
                                            _logger.Debug(
                                                          $"Sent to subscriber '{subscription.Name}' successfully");
                                        }
                                        else
                                        {
                                            _logger.Error(
                                                          $"Failed to send to subscriber '{subscription.Name}', {t.Status.ToString()}, {t.Exception?.GetBaseException()?.Message}");
                                        }
                                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(
                          $"Failed to send to subscriber '{subscription.Name}', {ex.Message}");
            }
        }
    }
}
