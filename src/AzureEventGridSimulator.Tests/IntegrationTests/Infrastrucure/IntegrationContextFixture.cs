namespace AzureEventGridSimulator.Tests.IntegrationTests.Infrastrucure;

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Messaging.EventGrid;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RichardSzalay.MockHttp;
using Xunit;

public class IntegrationContextFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public IntegrationContextFixture()
    {
        Settings = new SimulatorSettings()
        {
            Topics = new[]
            {
                new TopicSettings
                {
                    Name = "EventGridEvent",
                    Key = "TheLocal+DevelopmentKey=",
                    Port = 60101,
                    Type = EventType.EventGridEvent,
                    Subscribers = new SubscriberSettings
                    {
                        Http = new[]
                        {
                            new HttpSubscriptionSettings
                            {
                                Endpoint = "http://http.eventgridevent/",
                                DisableValidation = true,
                                Name = "EventGridEventHttpSubscriber"
                            }
                        },
                        ServiceBus = new[]
                        {
                            new AzureServiceBusSubscriptionSettings
                            {
                                Name = "EventGridEventAzureServiceBusSubscriber",
                                SharedAccessKey = "TheServiceBusKey=",
                                SharedAccessKeyName = "EventGridEvent.SharedAccessKeyName",
                                Namespace = "EventGridEventNamespace",
                                Topic = "EventGridEvent-Topic",
                                Disabled = false
                            }
                        }
                    }
                },
                new TopicSettings
                {
                    Name = "CloudEvent",
                    Key = "TheLocal+DevelopmentKey=",
                    Port = 60102,
                    Type = EventType.CloudEvent,
                    Subscribers = new SubscriberSettings
                    {
                        Http = new[]
                        {
                            new HttpSubscriptionSettings
                            {
                                Endpoint = "http://http.cloudevent/",
                                DisableValidation = true,
                                Name = "CloudEventHttpSubscriber"
                            }
                        },
                        ServiceBus = new[]
                        {
                            new AzureServiceBusSubscriptionSettings
                            {
                                Name = "CloudEventAzureServiceBusSubscriber",
                                SharedAccessKey = "TheServiceBusKey=",
                                SharedAccessKeyName = "CloudEvent.SharedAccessKeyName",
                                Namespace = "CloudEventNamespace",
                                Topic = "CloudEvent-Topic",
                                Disabled = false
                            }
                        }
                    }
                }
            }
        };
    }

    public SimulatorSettings Settings { get; }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates an <see cref="EventGridPublisherClient" /> that uses the <see cref="WebApplicationFactory"/> HTTP transport.
    /// </summary>
    /// <param name="url">URL is used as an identifier in the target HttpContext but not for the network connection ie. WebApplicationFactory HTTP transport does not make use of ports.</param>
    /// <param name="sasKey">Azure credential key</param>
    /// <returns><see cref="EventGridPublisherClient" /></returns>
    public EventGridPublisherClient CreateEventGridPublisherClient(string url, string sasKey)
    {
        return new EventGridPublisherClient(
                                          new Uri(url),
                                          new AzureKeyCredential(sasKey),
                                          new EventGridPublisherClientOptions
                                          {
                                              Retry =
                                              {
                                                  Mode = RetryMode.Fixed,
                                                  MaxRetries = 0,
                                                  NetworkTimeout = TimeSpan.FromSeconds(5)
                                              },
                                              Transport = new HttpClientTransport(CreateClient())
                                          });
    }

    public EventGridPublisherClient CreateEventGridEventPublisherClient(string sasKey = "TheLocal+DevelopmentKey=")
    {
        return CreateEventGridPublisherClient("https://localhost:60101/api/events", sasKey);
    }

    public EventGridPublisherClient CreateCloudEventPublisherClient(string sasKey = "TheLocal+DevelopmentKey=")
    {
        return CreateEventGridPublisherClient("https://localhost:60102/api/events", sasKey);
    }

    public MockHttpMessageHandler MockHttp { get; } = new MockHttpMessageHandler();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        builder.ConfigureTestServices(services =>
        {
            void Replace<T>(T instance)
            {
                var descriptor = new ServiceDescriptor(typeof(T), instance);
                services.Replace(descriptor);
            }

            Replace<IHttpClientFactory>(new MockHttpClientFactory(MockHttp));
            Replace(Settings);
        });
    }
}
