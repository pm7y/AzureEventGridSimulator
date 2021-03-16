using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureEventGridSimulator.Tests.Integration
{
    public class TestContextFixture :  WebApplicationFactory<Startup>, IAsyncLifetime
    {
        public TestContextFixture()
        {

        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);

            builder.ConfigureAppConfiguration((context, configurationBuilder) =>
            {
                configurationBuilder
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.tests.json", false, true);
                    // .AddJsonFile("appsettings.tests.json", false, true)
                    // .AddJsonFile("appsettings.tests.localdev.json", optional: true, true)
                    // .AddEnvironmentVariables()
                    // .AddUserSecrets(Assembly.GetExecutingAssembly(), true);

                // builder.ConfigureServices(services => { });
            });

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
        }


        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }
    }
}
