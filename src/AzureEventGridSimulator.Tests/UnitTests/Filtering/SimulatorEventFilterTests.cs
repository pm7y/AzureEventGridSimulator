using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

[Trait("Category", "unit")]
public class SimulatorEventFilterTests
{
    [Fact]
    public void GivenDefaultFilter_WhenFilteringCloudEvent_ThenAccepted()
    {
        var filter = new FilterSetting();
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
            Subject = "/test/subject",
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }

    [Fact]
    public void GivenEventTypeFilter_WhenCloudEventMatchesType_ThenAccepted()
    {
        var filter = new FilterSetting { IncludedEventTypes = new[] { "com.example.test" } };
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }

    [Fact]
    public void GivenEventTypeFilter_WhenCloudEventDoesNotMatchType_ThenRejected()
    {
        var filter = new FilterSetting { IncludedEventTypes = new[] { "com.example.other" } };
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeFalse();
    }

    [Fact]
    public void GivenSubjectBeginsWithFilter_WhenCloudEventSubjectMatches_ThenAccepted()
    {
        var filter = new FilterSetting { SubjectBeginsWith = "/test/" };
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
            Subject = "/test/subject/path",
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }

    [Fact]
    public void GivenSubjectEndsWithFilter_WhenCloudEventSubjectMatches_ThenAccepted()
    {
        var filter = new FilterSetting { SubjectEndsWith = "/path" };
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
            Subject = "/test/subject/path",
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }

    [Fact]
    public void GivenSubjectFilter_WhenCloudEventHasNoSubject_ThenUsesSourceAsFallback()
    {
        var filter = new FilterSetting { SubjectBeginsWith = "/test/" };
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
            // No Subject - should fall back to Source
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }

    [Fact]
    public void GivenAllEventTypesFilter_WhenFilteringCloudEvent_ThenAccepted()
    {
        var filter = new FilterSetting { IncludedEventTypes = new[] { "All" } };
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "any.event.type",
            Source = "/test/source",
            Id = "test-id",
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }

    [Fact]
    public void GivenCombinedFilters_WhenAllMatch_ThenAccepted()
    {
        var filter = new FilterSetting
        {
            IncludedEventTypes = new[] { "com.example.test" },
            SubjectBeginsWith = "/test/",
            SubjectEndsWith = "/path",
        };
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
            Subject = "/test/subject/path",
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }

    [Fact]
    public void GivenCombinedFilters_WhenOneFails_ThenRejected()
    {
        var filter = new FilterSetting
        {
            IncludedEventTypes = new[] { "com.example.test" },
            SubjectBeginsWith = "/wrong/",
        };
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
            Subject = "/test/subject/path",
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeFalse();
    }

    [Fact]
    public void GivenNullFilter_WhenFilteringSimulatorEvent_ThenAccepted()
    {
        FilterSetting filter = null;
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id",
        };
        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);

        filter.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }
}
