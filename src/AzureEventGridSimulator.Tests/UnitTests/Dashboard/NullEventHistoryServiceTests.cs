using System.Net;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Dashboard;

[Trait("Category", "unit")]
public class NullEventHistoryServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 5, 14, 7, 9, TimeSpan.Zero);

    private static readonly TopicSettings Topic = new()
    {
        Name = "test-topic",
        Port = 60101,
        Key = "key",
    };

    private readonly NullEventHistoryService _service = new();

    [Fact]
    public void GivenEverythingIsRecorded_WhenHistoryIsRead_ThenNothingWasKept()
    {
        RecordEventWithDeliveryAndRejection(_service);

        _service.GetRecentEvents().ShouldBeEmpty();
        _service.GetRecentEvents(Topic.Name).ShouldBeEmpty();
        _service.GetEvent("event-1").ShouldBeNull();
        _service.GetRecentRejections().ShouldBeEmpty();
    }

    [Fact]
    public void GivenEverythingIsRecorded_WhenStatsAreRead_ThenAllCountsAreZero()
    {
        RecordEventWithDeliveryAndRejection(_service);

        _service.GetStats().ShouldBe(new DashboardStats(0, 0, 0, 0, 0, 0, 0, null, null));
    }

    [Fact]
    public void GivenNothingIsRecorded_WhenCleared_ThenItDoesNothing()
    {
        Should.NotThrow(() => _service.Clear());

        _service.GetRecentEvents().ShouldBeEmpty();
        _service.GetRecentRejections().ShouldBeEmpty();
    }

    [Fact]
    public void GivenDashboardIsDisabled_WhenHistoryServiceIsResolved_ThenItIsTheNullService()
    {
        using var provider = BuildProvider(dashboardEnabled: false);

        provider
            .GetRequiredService<IEventHistoryService>()
            .ShouldBeOfType<NullEventHistoryService>();
    }

    [Fact]
    public void GivenDashboardIsEnabled_WhenHistoryServiceIsResolved_ThenItRecordsHistory()
    {
        using var provider = BuildProvider(dashboardEnabled: true);

        var service = provider.GetRequiredService<IEventHistoryService>();
        service.RecordEventReceived(CreateTestEvent("event-1"), Topic, EventSchema.EventGridSchema);

        service.ShouldBeOfType<EventHistoryService>();
        service.GetEvent("event-1").ShouldNotBeNull();
    }

    [Fact]
    public void GivenSettingsAreReplacedAfterRegistration_WhenHistoryServiceIsResolved_ThenTheReplacementDecides()
    {
        // IntegrationContextFixture replaces the SimulatorSettings singleton after Program has
        // registered its services, so the choice must be made when the service is resolved
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new SimulatorSettings { DashboardEnabled = true });
        Program.AddEventHistoryServices(services);
        services.AddSingleton(new SimulatorSettings { DashboardEnabled = false });

        using var provider = services.BuildServiceProvider();

        provider
            .GetRequiredService<IEventHistoryService>()
            .ShouldBeOfType<NullEventHistoryService>();
    }

    private static ServiceProvider BuildProvider(bool dashboardEnabled)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        services.AddSingleton(new SimulatorSettings { DashboardEnabled = dashboardEnabled });
        Program.AddEventHistoryServices(services);

        return services.BuildServiceProvider();
    }

    private static void RecordEventWithDeliveryAndRejection(IEventHistoryService service)
    {
        var subscriber = new HttpSubscriberSettings
        {
            Name = "http-subscriber",
            Endpoint = "https://example.com/webhook",
        };

        service.RecordEventReceived(CreateTestEvent("event-1"), Topic, EventSchema.EventGridSchema);
        service.RecordDeliveryQueued("event-1", subscriber);
        service.RecordDeliveryAttempt(
            "event-1",
            subscriber.Name,
            new DeliveryAttempt(1, DeliveryOutcome.Success, Now, 200)
        );
        service.RecordDeliveryCompleted("event-1", subscriber.Name, DeliveryStatus.Delivered, Now);
        service.RecordEventRejected(
            RejectedEventRecord.Create(
                Topic.Name,
                Topic.Port,
                HttpStatusCode.BadRequest,
                "Invalid JSON",
                Now,
                "[",
                "application/json"
            )
        );
    }

    private static SimulatorEvent CreateTestEvent(string id)
    {
        return SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = id,
                Subject = "/test/subject",
                EventType = "Test.EventType",
                EventTime = Now.ToString("o"),
                DataVersion = "1.0",
                Data = new { Property = "Value" },
            }
        );
    }
}
