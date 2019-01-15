using System;
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
        public static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var settings = new SimulatorSettings();
            configuration.Bind(settings);

            var topics = settings.Topics;

            foreach (var topic in topics)
            {
                Task.Factory.StartNew(async () =>
                {

                    var listener = new HttpListener();
                    listener.Prefixes.Add($"https://localhost:{topic.HttpsPort}/");

                    listener.Start();

                    while (true)
                    {
                        var context = await listener.GetContextAsync();
                        var request = context.Request;
                        var response = context.Response;

                        await HandleRequestAsync(request, response, topic, topic.Subscriptions.ToArray());
                    }

                }, TaskCreationOptions.LongRunning).ConfigureAwait(false);
            }

            Console.WriteLine("Waiting for requests...");
            Console.ReadKey();
        }

        private static async Task HandleRequestAsync(HttpListenerRequest request,
            HttpListenerResponse response,
            TopicSettings topic,
            SubscriptionSettings[] subscriptions)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(topic.Key))
                {
                    if (request.Headers.AllKeys.Any(k =>
                        string.Equals(k, "aeg-sas-key")))
                    {
                        if (string.Equals(request.Headers["aeg-sas-key"], topic.Key))
                        {
                            Console.WriteLine("'aeg-sas-key' value did not match configured value!");
                            response.StatusCode = (int) HttpStatusCode.Unauthorized;
                            return;
                        }
                    }

                    if (request.Headers.AllKeys.Any(k =>
                        string.Equals(k, "aeg-sas-token")))
                    {
                        var token = request.Headers["aeg-sas-token"];
                        if (!TokenIsValid(token, topic.Key))
                        {
                            Console.WriteLine("'aeg-sas-token' value was not valid based on the configured key value!");
                            response.StatusCode = (int) HttpStatusCode.Unauthorized;
                            return;
                        }
                    }
                }

                EditableEventGridEvent[] events;

                using (var sr = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    var body = await sr.ReadToEndAsync();

                    try
                    {
                        events = JsonConvert.DeserializeObject<EditableEventGridEvent[]>(body);

                        foreach (var eventGridEvent in events)
                        {
                            eventGridEvent.Topic = $"/azure/eventgrid-simulator/{topic.Name}";
                            eventGridEvent.MetadataVersion = "1.0";
                        }
                    }
                    catch (JsonSerializationException)
                    {
                        response.StatusCode = (int) HttpStatusCode.BadRequest;
                        return;
                    }
                }

                Console.WriteLine($"Received {events.Length} new events for {topic.Name}");

#pragma warning disable 4014
                Task.Factory.StartNew(async () =>
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Add("aeg-event-type", "Notification");
                        httpClient.Timeout = TimeSpan.FromSeconds(60);

                        var json = JsonConvert.SerializeObject(events);

                        using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                        {
                            foreach (var subscription in subscriptions)
                            {
                                try
                                {
                                    Console.WriteLine($"Sending to subscriber '{subscription.Name}'");

                                    await httpClient.PostAsync(subscription.Endpoint, content)
                                        .ContinueWith(t =>
                                        {
                                            if (t.IsCompletedSuccessfully)
                                            {
                                                Console.WriteLine(
                                                    $"Sent to subscriber '{subscription.Name}' successfully");
                                            }
                                            else
                                            {
                                                Console.WriteLine(
                                                    $"Failed to send to subscriber '{subscription.Name}', {t.Status.ToString()}, {t.Exception?.GetBaseException()?.Message}");
                                            }
                                        }).ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(
                                        $"Failed to send to subscriber '{subscription.Name}', {ex.Message}");
                                    throw;
                                }
                            }
                        }
                    }
                }, TaskCreationOptions.LongRunning);
#pragma warning restore 4014

                /*
                    Success	200 OK
                    Event data has incorrect format	400 Bad Request
                    Invalid access key	401 Unauthorized
                    Incorrect endpoint	404 Not Found
                    Array or event exceeds size limits	413 Payload Too Large
                 */

                response.StatusCode = 200; //OK
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                response.StatusCode = 500; // error
            }
            finally
            {
                response.Close();
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

                if (encodedSignature != signature)
                {
                    Console.WriteLine($"{encodedComputedSignature} != {signature}");
                    return false;
                }

                return true;
            }
        }
    }
}
