using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace AzureEventGridSimulator
{
    public class Program
    {
        private static bool _quitting;
        private static readonly List<string> ReceivedEventIds = new List<string>();

        public static void Main(string[] args)
        {
            Log.Info("Azure Event Grid Simulator Starting...");

            var settings = GetSimulatorSettings();

            Log.Debug($"Found {settings.Topics.Count} topics in appsettings.json.");

            foreach (var topic in settings.Topics)
            {
                CreateListener(topic);
            }

            Console.ReadKey();
            _quitting = true;
        }

        private static SimulatorSettings GetSimulatorSettings()
        {
            var configuration = new ConfigurationBuilder()
                                .AddJsonFile("appsettings.json")
                                .Build();

            var settings = new SimulatorSettings();
            configuration.Bind(settings);

            // Add the default internal subscriber endpoint
            foreach (var topic in settings.Topics)
            {
                if (topic.Subscribers == null || !topic.Subscribers.Any())
                {
                    topic.Subscribers = new List<SubscriptionSettings>();
                }

                var internalSubscriber = InternalSubscriber.New(topic.Name);
                topic.Subscribers.Add(new SubscriptionSettings { Name = internalSubscriber.Name, Endpoint = internalSubscriber.Prefix });
            }

            return settings;
        }

        private static void CreateListener(TopicSettings topic)
        {
            var listener = new HttpListener();

            var prefix = $"https://127.0.0.1:{topic.HttpsPort}/api/events/";
            listener.Prefixes.Add(prefix);
            listener.Start();

            Log.Info($"Topic '{topic.Name}' listening @ {prefix}");

#pragma warning disable 4014
            Process(listener, topic);
#pragma warning restore 4014
        }

        private static async Task Process(HttpListener listener, TopicSettings topic)
        {
            while (!_quitting)
            {
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                await HandleRequestAsync(request, response, topic);
            }
        }

        private static async Task HandleRequestAsync(HttpListenerRequest request,
                                                     HttpListenerResponse response,
                                                     TopicSettings topic)
        {
            try
            {
                Log.Debug($"Request received for topic '{topic.Name}'.");

                if (request.Url.LocalPath.ToLowerInvariant().TrimEnd('/') != "/api/events")
                {
                    Log.Error("Invalid endpoint, should be in the form https://127.0.0.1:<port>/api/events");
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(topic.Key))
                {
                    if (request.Headers.AllKeys.Any(k =>
                                                        string.Equals(k, "aeg-sas-key")))
                    {
                        if (!string.Equals(request.Headers["aeg-sas-key"], topic.Key))
                        {
                            Log.Error("'aeg-sas-key' value did not match configured value!");
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            return;
                        }

                        Log.Debug($"'aeg-sas-key' header is valid");
                    }
                    else if (request.Headers.AllKeys.Any(k =>
                                                             string.Equals(k, "aeg-sas-token")))
                    {
                        var token = request.Headers["aeg-sas-token"];
                        if (!TokenIsValid(token, topic.Key))
                        {
                            Log.Error("'aeg-sas-key' value did not match configured value!");
                            response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            return;
                        }

                        Log.Debug($"'aeg-sas-token' header is valid");
                    }
                    else
                    {
                        Log.Warn($"Request received without key header for topic '{topic.Name}'.");
                    }
                }
                else
                {
                    Log.Warn("There is no configured topic key so the request key/token header will not be validated.");
                }

                try
                {
                    var unformattedJson = await GetJsonFromRequestBody(request);
                    var events = JsonConvert.DeserializeObject<EditableEventGridEvent[]>(unformattedJson);
                    var formattedJson = JsonConvert.SerializeObject(events, Formatting.Indented);

                    Log.Info(formattedJson);

                    // Check the event hasn't already been seen
                    foreach (var evt in events)
                    {
                        var eventId = evt.Id;

                        if (ReceivedEventIds.Contains(eventId))
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                            return;
                        }

                        ReceivedEventIds.Add(eventId);
                    }

                    // Check the overall message isn't > 1Mb
                    if (formattedJson.Length > 1 * 1024 * 1024)
                    {
                        Log.Error("The incoming message is greater the 1Mb.");
                        response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                        return;
                    }

                    // Check that no event is > 64Kb
                    foreach (var evt in events)
                    {
                        var evtJson = JsonConvert.SerializeObject(evt, Formatting.Indented);

                        if (evtJson.Length > 64 * 1024)
                        {
                            Log.Error($"Event with Id '{evt.Id}' is greater than 64Kb.");
                            response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                            return;
                        }
                    }

                    // Check that each event is valid
                    foreach (var evt in events)
                    {
                        evt.Validate();
                    }

                    var topicPath = $"/azure/event/grid/simulator/{Environment.MachineName}/{topic.Name}";

                    // Check that the topic is null or that it's valid
                    foreach (var eventGridEvent in events)
                    {
                        if (!string.IsNullOrWhiteSpace(eventGridEvent.Topic))
                        {
                            Log.Warn("'Topic' property was expected to be null or empty.");

                            var topicProperty = eventGridEvent.Topic.TrimEnd('/');

                            if (!string.Equals(topicProperty, topicPath, StringComparison.OrdinalIgnoreCase))
                            {
                                Log.Error($"Topic property should be null or {topicPath}");
                                response.StatusCode = (int)HttpStatusCode.BadRequest;
                                return;
                            }
                        }

                        eventGridEvent.Topic = topicPath;
                        eventGridEvent.MetadataVersion = "1";
                    }

                    formattedJson = JsonConvert.SerializeObject(events, Formatting.Indented);

                    foreach (var subscription in topic.Subscribers)
                    {
#pragma warning disable 4014
                        SendToSubscriber(subscription, formattedJson);
#pragma warning restore 4014
                    }
                }
                catch (JsonSerializationException ex)
                {
                    Log.Error(ex);
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Log.Error(ex);

                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            finally
            {
                response.Close();
            }
        }

        private static async Task SendToSubscriber(SubscriptionSettings subscription, string json)
        {
            try
            {
                Log.Debug($"Sending to subscriber '{subscription.Name}'");
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
                                            Log.Debug(
                                                     $"Sent to subscriber '{subscription.Name}' successfully");
                                        }
                                        else
                                        {
                                            Log.Error(
                                                      $"Failed to send to subscriber '{subscription.Name}', {t.Status.ToString()}, {t.Exception?.GetBaseException()?.Message}");
                                        }
                                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(
                          $"Failed to send to subscriber '{subscription.Name}', {ex.Message}");
                throw;
            }
        }

        private static async Task<string> GetJsonFromRequestBody(HttpListenerRequest request)
        {
            using (var sr = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return await sr.ReadToEndAsync();
            }
        }

        private static bool TokenIsValid(string token, string key)
        {
            var query = HttpUtility.ParseQueryString(token);
            var decodedResource = HttpUtility.UrlDecode(query["r"], Encoding.UTF8);
            var decodedExpiration = HttpUtility.UrlDecode(query["e"], Encoding.UTF8);
            var encodedSignature = query["s"];

            var encodedResource = HttpUtility.UrlEncode(decodedResource);
            var encodedExpiration = HttpUtility.UrlEncode(decodedExpiration);

            var unsignedSas = $"r={encodedResource}&e={encodedExpiration}";

            using (var hmac = new HMACSHA256(Convert.FromBase64String(key)))
            {
                var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedSas)));
                var encodedComputedSignature = HttpUtility.UrlEncode(signature);

                if (encodedSignature == signature)
                {
                    return true;
                }

                Log.Warn($"{encodedComputedSignature} != {signature}");
                return false;
            }
        }
    }
}
