using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Converters;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Authentication;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Filters;
using AzureEventGridSimulator.Infrastructure.Middleware;
using AzureEventGridSimulator.Infrastructure.ModelBinders;
using AzureEventGridSimulator.Infrastructure.Options;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

[assembly: InternalsVisibleTo("AzureEventGridSimulator.Tests")]

namespace AzureEventGridSimulator;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // Build it and fire it up
            var app = CreateWebHostBuilder(args)
                .Build();

            app.UseSerilogRequestLogging(options => { options.GetLevel = (_, _, _) => LogEventLevel.Debug; });
            app.UseNotFoundMiddleware();
            app.UseRouting();
            app.UseAuthorization();
            app.MapControllers();

            await StartSimulator(app);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start the Azure Event Grid Simulator");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    public static async Task StartSimulator(WebApplication host, CancellationToken token = default)
    {
        try
        {
            await host.StartAsync(token)
                      .ContinueWith(_ => OnApplicationStarted(host, host.Lifetime), token)
                      .ConfigureAwait(false);

            await host.WaitForShutdownAsync(token).ConfigureAwait(false);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task OnApplicationStarted(IApplicationBuilder app, IHostApplicationLifetime lifetime)
    {
        try
        {
            Log.Verbose("Started");

            var simulatorSettings = app.ApplicationServices.GetService<SimulatorSettings>();

            if (simulatorSettings is null)
            {
                Log.Fatal("Settings are not found. The application will now exit");
                lifetime.StopApplication();
                return;
            }

            if (!simulatorSettings.Topics.Any())
            {
                Log.Fatal("There are no configured topics. The application will now exit");
                lifetime.StopApplication();
                return;
            }

            if (simulatorSettings.Topics.All(o => o.Disabled))
            {
                Log.Fatal("All of the configured topics are disabled. The application will now exit");
                lifetime.StopApplication();
                return;
            }

            var mediator = app.ApplicationServices.GetService<IMediator>();

            if (mediator is null)
            {
                Log.Fatal("Required component was not found. The application will now exit");
                lifetime.StopApplication();
                return;
            }

            await mediator.Send(new ValidateAllHttpSubscriptionsCommand());

            Log.Information("It's alive !");
        }
        catch (Exception e)
        {
            Log.Fatal(e, "It died !");
            lifetime.StopApplication();
        }
    }

    private static WebApplicationBuilder CreateWebHostBuilder(string[] args)
    {
        // Set up basic Console logger we can use to log to until we've finished building everything
        Log.Logger = CreateBasicConsoleLogger();

        // Configure the web host builder
        return ConfigureWebHost(args);
    }

    private static ILogger CreateBasicConsoleLogger()
    {
        return new LoggerConfiguration()
               .MinimumLevel.Is(LogEventLevel.Verbose)
               .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
               .MinimumLevel.Override("System", LogEventLevel.Error)
               .WriteTo.Console()
               .CreateLogger();
    }

    private static WebApplicationBuilder ConfigureWebHost(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSimulatorSettings();
        builder.Services.AddMediatR(Assembly.GetExecutingAssembly());
        builder.Services.AddHttpClient();
        builder.Services.AddAuthentication("SAS")
            .AddScheme<SasAuthenticationOptions, SasAuthenticationHandler>("SAS", null);

        builder.Services.AddScoped<MaxContentLengthAttribute>();
        builder.Services.AddScoped<NotFoundMiddleware>();
        builder.Services.AddScoped<TopicMiddleware>();
        builder.Services.AddSingleton<ValidationIpAddressProvider>();

        builder.Services.AddSingleton<EventGridEventConverter>();
        builder.Services.AddSingleton<CloudEventConverter>();
        builder.Services.AddSingleton<HttpContextFeaturesModelBinderProvider>();

        builder.Services.AddSingleton<ConfigureApiBehaviorOptions>();
        builder.Services.AddSingleton<ConfigureJsonOptions>();
        builder.Services.AddSingleton<ConfigureMvcOptions>();

        builder.Services.ConfigureOptions<ConfigureMvcOptions>();
        builder.Services.ConfigureOptions<ConfigureJsonOptions>();
        builder.Services.AddControllers();

        // must be after AddControllers();
        builder.Services.ConfigureOptions<ConfigureApiBehaviorOptions>();

        builder.Services.AddApiVersioning(config =>
        {
            config.DefaultApiVersion = new ApiVersion(DateTime.Parse(Constants.SupportedApiVersion, new ApiVersionFormatProvider()));
            config.AssumeDefaultVersionWhenUnspecified = true;
            config.ReportApiVersions = true;
        });

        builder.Logging.ClearProviders();
        builder.Host.UseSerilog((context, loggerConfiguration) =>
        {
            var hasAtLeastOneLogSinkBeenConfigured = context.Configuration.GetSection("Serilog:WriteTo").GetChildren().ToArray().Any();

            loggerConfiguration
                .Enrich.FromLogContext()
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithProperty("Environment", context.Configuration.EnvironmentName())
                .Enrich.WithProperty("Application", nameof(AzureEventGridSimulator))
                .Enrich.WithProperty("Version", Assembly.GetExecutingAssembly().GetName().Version)
                // The sensible defaults
                .MinimumLevel.Is(LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                .MinimumLevel.Override("System", LogEventLevel.Error)
                // Override defaults from settings if any
                .ReadFrom.Configuration(context.Configuration, "Serilog")
                .WriteTo.Conditional(_ => !hasAtLeastOneLogSinkBeenConfigured, sinkConfiguration => sinkConfiguration.Console());
        });

        builder.AddConfiguration(args);
        builder.AddTopicRouting();

        builder.WebHost.UseKestrel((context, options) =>
        {
            Log.Verbose(((IConfigurationRoot)context.Configuration).GetDebugView().Normalize());

            options.ConfigureSimulatorCertificate();

            foreach (var topics in options.ApplicationServices.EnabledTopics())
            {
                options.Listen(IPAddress.Any,
                               topics.Port,
                               listenOptions => listenOptions
                                   .UseHttps());
            }
        });

        return builder;
    }
}
