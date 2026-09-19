using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

[Trait("Category", "unit")]
public class AdvancedFilterEventAcceptanceTests
{
    private static readonly SimulatorEvent _simulatorEvent = SimulatorEvent.FromEventGridEvent(
        CreateTestEvent()
    );

    private static EventGridEvent CreateTestEvent()
    {
        var evt = new EventGridEvent
        {
            Id = "EventId",
            Subject = "TheEventSubject",
            EventType = "this.is.a.test.event.type",
            EventTime = DateTimeOffset.UtcNow.ToString("O"),
            Data = new
            {
                NumberValue = 1,
                IsTrue = true,
                Name = "StringValue",
                DoubleValue = 0.12345d,
                NumberMaxValue = ulong.MaxValue,
                SubObject = new { Id = 1, Name = "Test" },
            },
            DataVersion = "5.0",
            MetadataVersion = "1",
        };
        evt.SetTopic("THE_EVENT_TOPIC");
        return evt;
    }

    [Theory]
    [ClassData(typeof(PositiveFilterTestCaseContainer))]
    public void TestAdvancedFilteringSuccess(AdvancedFilterSetting filter)
    {
        var filterConfig = new FilterSetting { AdvancedFilters = new[] { filter } };

        filterConfig
            .AcceptsEvent(_simulatorEvent)
            .ShouldBeTrue(
                $"{filter.Key} - {filter.OperatorType} - {filter.Value} - {filter.Values.Separate()}"
            );
    }

    [Theory]
    [ClassData(typeof(NegativeFilterTestCaseContainer))]
    public void TestAdvancedFilteringFailure(AdvancedFilterSetting filter)
    {
        var filterConfig = new FilterSetting { AdvancedFilters = new[] { filter } };

        filterConfig
            .AcceptsEvent(_simulatorEvent)
            .ShouldBeFalse(
                $"{filter.Key} - {filter.OperatorType} - {filter.Value} - {filter.Values.Separate()}"
            );
    }

    [Fact]
    public void GivenFilterTestCaseContainers_WhenEnumerated_ThenRowCountsAreAsExpected()
    {
        // Catches rows lost or duplicated when the containers are edited (78 + 94 = 172)
        new PositiveFilterTestCaseContainer()
            .Count()
            .ShouldBe(78);
        new NegativeFilterTestCaseContainer().Count().ShouldBe(94);
    }

    [Fact]
    public void TestSimpleEventDataFilteringSuccess()
    {
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                    Values = [1],
                },
            ],
        };
        var simulatorEvent = SimulatorEvent.FromEventGridEvent(
            TestHelpers.CreateValidEventGridEvent(data: 1)
        );

        filterConfig.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }

    [Fact]
    public void TestSimpleEventDataFilteringUsingValueSuccess()
    {
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data",
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .NumberGreaterThanOrEquals,
                    Value = 1,
                },
                new AdvancedFilterSetting
                {
                    Key = "Data",
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .NumberLessThanOrEquals,
                    Value = 1,
                },
            ],
        };
        var simulatorEvent = SimulatorEvent.FromEventGridEvent(
            TestHelpers.CreateValidEventGridEvent(data: 1)
        );

        filterConfig.AcceptsEvent(simulatorEvent).ShouldBeTrue();
    }

    [Fact]
    public void TestSimpleEventDataFilteringFailure()
    {
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                    Value = 1,
                },
            ],
        };
        var simulatorEvent = SimulatorEvent.FromEventGridEvent(
            TestHelpers.CreateValidEventGridEvent(data: 1)
        );

        filterConfig.AcceptsEvent(simulatorEvent).ShouldBeFalse();
    }
}
