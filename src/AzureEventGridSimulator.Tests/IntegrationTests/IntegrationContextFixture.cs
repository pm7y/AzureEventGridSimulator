using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

[UsedImplicitly]
public class IntegrationContextFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    ///     Captures every outbound HTTP request the simulator makes (deliveries
    ///     and subscription validation events) and fakes the subscriber responses.
    /// </summary>
    public CapturingHttpMessageHandler OutboundHttp { get; } = new();

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                configurationBuilder
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.test.json", false, true);
            }
        );

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        builder.ConfigureTestServices(services =>
        {
            // Route the simulator's outbound HTTP (deliveries, validation events)
            // through the capturing handler instead of the network.
            services
                .AddHttpClient(nameof(AzureEventGridSimulator))
                .ConfigurePrimaryHttpMessageHandler(() => OutboundHttp);

            // The simulator binds SimulatorSettings from its own standalone
            // configuration root (Program.BuildConfiguration), which never sees
            // the json file added via ConfigureAppConfiguration above - it reads
            // the default appsettings.json copied from the simulator project.
            // Replace the singleton with settings genuinely bound from the test
            // configuration file, mirroring AddSimulatorSettings.
            var testConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", false, false)
                .Build();

            var testSettings = new Infrastructure.Settings.SimulatorSettings();
            testConfiguration.Bind(testSettings);
            testSettings.Validate();

            services.AddSingleton(testSettings);
        });
    }
}
