using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Core;
using Azure.Messaging.EventGrid;
using Azure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

public class IntegrationContextFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
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
    public EventGridPublisherClient CreateEventGridPublisherClient(string url = "https://localhost:60101/api/events", string sasKey = "TheLocal+DevelopmentKey=")
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", false, true);
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
    }
}
