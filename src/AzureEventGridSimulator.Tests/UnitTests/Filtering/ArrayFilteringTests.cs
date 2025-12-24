using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

[Trait("Category", "unit")]
public class ArrayFilteringTests
{
    /// <summary>
    /// Test event with array data for filtering tests.
    /// </summary>
    private static EventGridEvent CreateEventWithArrayData()
    {
        var evt = new EventGridEvent
        {
            Id = "test-id",
            Subject = "test-subject",
            EventType = "Test.Event.Type",
            EventTime = DateTimeOffset.UtcNow.ToString("O"),
            Data = new
            {
                Tags = new[] { "important", "urgent", "review" },
                Numbers = new[] { 1, 2, 3, 4, 5 },
                Categories = new[] { "CategoryA", "CategoryB" },
                SingleValue = "not-an-array",
            },
            DataVersion = "1.0",
            MetadataVersion = "1",
        };
        evt.SetTopic("test-topic");
        return evt;
    }

    [Fact]
    public void StringIn_WithArrayFiltering_MatchesAnyElement()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Values = ["important"],
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void StringIn_WithArrayFiltering_MatchesMiddleElement()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Values = ["urgent"],
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void StringIn_WithArrayFiltering_NoMatch()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Values = ["nonexistent"],
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeFalse();
    }

    [Fact]
    public void StringContains_WithArrayFiltering_MatchesPartialElement()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                    Values = ["port"], // matches "important"
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void StringBeginsWith_WithArrayFiltering_MatchesElement()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .StringBeginsWith,
                    Values = ["urg"], // matches "urgent"
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void StringEndsWith_WithArrayFiltering_MatchesElement()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith,
                    Values = ["view"], // matches "review"
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void NumberIn_WithArrayFiltering_MatchesElement()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Numbers",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                    Values = [3],
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void NumberGreaterThan_WithArrayFiltering_MatchesAnyElement()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Numbers",
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .NumberGreaterThan,
                    Value = 4, // 5 > 4
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void NumberGreaterThan_WithArrayFiltering_NoElementMatches()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Numbers",
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .NumberGreaterThan,
                    Value = 10, // no element > 10
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeFalse();
    }

    [Fact]
    public void StringNotIn_WithArrayFiltering_AllElementsMustNotMatch()
    {
        // For negation operators, ALL elements must satisfy the condition
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                    Values = ["nonexistent"], // none of the elements match "nonexistent"
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void StringNotIn_WithArrayFiltering_FailsIfAnyElementMatches()
    {
        // For negation operators, ALL elements must satisfy the condition
        // If one element matches the "not in" list, the filter fails
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                    Values = ["important"], // "important" IS in the array
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeFalse();
    }

    [Fact]
    public void StringNotContains_WithArrayFiltering_AllElementsMustPass()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .StringNotContains,
                    Values = ["xyz"], // none of the elements contain "xyz"
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void ArrayFiltering_DisabledByDefault_TreatsArrayAsNonMatchingValue()
    {
        // When EnableAdvancedFilteringOnArrays is false (default),
        // array values don't match string filters
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = false,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Values = ["important"],
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeFalse();
    }

    [Fact]
    public void ArrayFiltering_WorksWithNonArrayValues()
    {
        // Array filtering should not break normal scalar filtering
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.SingleValue",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Values = ["not-an-array"],
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }

    [Fact]
    public void ArrayFiltering_MultipleFilters_AllMustPass()
    {
        var filter = new FilterSetting
        {
            EnableAdvancedFilteringOnArrays = true,
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    Key = "Data.Tags",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains,
                    Values = ["important"],
                },
                new AdvancedFilterSetting
                {
                    Key = "Data.Numbers",
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                    Values = [1, 2, 3],
                },
            ],
        };

        filter.AcceptsEvent(CreateEventWithArrayData()).ShouldBeTrue();
    }
}
