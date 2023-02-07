namespace AzureEventGridSimulator.Tests.UnitTests.Filtering.CloudEvent;

using System;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using Shouldly;
using Xunit;

[Trait("Category", "unit")]
[Trait("Type", "CloudEvent")]
public class AdvancedFilterEventAcceptanceTests
{
    private static readonly CloudEvent _gridEvent = new()
    {
        Id = "EventId",
        SpecVersion = "1.0",
        Source = "",
        Type = "this.is.a.test.event.type",
        DataContentType = "this.is.a.data.content.type",
        DataSchema = "this.is.a.data.schema",
        Subject = "TheEventSubject",
        Time = DateTime.UtcNow.ToString("O"),
        Data = new { NumberValue = 1, IsTrue = true, Name = "StringValue", DoubleValue = 0.12345d, NumberMaxValue = ulong.MaxValue, SubObject = new { Id = 1, Name = "Test" } },
        DataBase64 = "",
        ExtensionAttributes = { { "ExtensionAttrib", "this.is.an.extension.attribute" } }
    };

    [Theory]
    [ClassData(typeof(PositiveFilterTestCaseContainer))]
    public void TestAdvancedFilteringSuccess(AdvancedFilterSetting filter)
    {
        var filterConfig = new FilterSetting { AdvancedFilters = new[] { filter } };

        filterConfig.AcceptsEvent(_gridEvent).ShouldBeTrue($"{filter.Key} - {filter.OperatorType} - {filter.Value} - {filter.Values.Separate()}");
    }

    [Theory]
    [ClassData(typeof(NegativeFilterTestCaseContainer))]
    public void TestAdvancedFilteringFailure(AdvancedFilterSetting filter)
    {
        var filterConfig = new FilterSetting { AdvancedFilters = new[] { filter } };

        filterConfig.AcceptsEvent(_gridEvent).ShouldBeFalse($"{filter.Key} - {filter.OperatorType} - {filter.Value} - {filter.Values.Separate()}");
    }

    [Fact]
    public void TestSimpleEventDataFilteringSuccess()
    {
        var filterConfig = new FilterSetting
        {
            AdvancedFilters = new[]
            {
                new AdvancedFilterSetting { Key = "Data", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberIn, Values = new object[] { 1 } }
            }
        };
        var gridEvent = new EventGridEvent { Data = 1 };

        filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue();
    }

    [Fact]
    public void TestSimpleEventDataFilteringUsingValueSuccess()
    {
        var filterConfig = new FilterSetting
        {
            AdvancedFilters = new[]
            {
                new AdvancedFilterSetting { Key = "Data", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberGreaterThanOrEquals, Value = 1 },
                new AdvancedFilterSetting { Key = "Data", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberLessThanOrEquals, Value = 1 }
            }
        };
        var gridEvent = new EventGridEvent { Data = 1 };

        filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue();
    }

    [Fact]
    public void TestSimpleEventDataFilteringFailure()
    {
        var filterConfig = new FilterSetting
        {
            AdvancedFilters = new[]
            {
                new AdvancedFilterSetting { Key = "Data", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.NumberIn, Value = 1 }
            }
        };
        var gridEvent = new EventGridEvent { Data = 1 };

        filterConfig.AcceptsEvent(gridEvent).ShouldBeFalse();
    }
}
