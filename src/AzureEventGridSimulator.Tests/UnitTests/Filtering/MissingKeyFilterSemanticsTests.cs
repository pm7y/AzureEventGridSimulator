using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

/// <summary>
///     Pins Azure's documented (and counterintuitive) missing-key semantics:
///     when the filter key is absent from the event, only NumberNotIn,
///     NumberNotInRange and StringNotIn evaluate as matched - the other
///     negation operators do NOT match, despite being negations.
/// </summary>
[Trait("Category", "unit")]
public class MissingKeyFilterSemanticsTests
{
    private static bool EvaluateWithMissingKey(
        AdvancedFilterSetting.AdvancedFilterOperatorType operatorType
    )
    {
        var gridEvent = TestHelpers.CreateValidEventGridEvent(data: new { Name = "StringValue" });
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    OperatorType = operatorType,
                    Key = "Data.MissingKey",
                    Value = "1",
                    Values = [new object[] { 1d, 2d }],
                },
            ],
        };

        return filterConfig.AcceptsEvent(gridEvent);
    }

    [Theory]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotIn)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.NumberNotInRange)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.IsNullOrUndefined)]
    public void GivenFilterKeyMissingFromEvent_WhenOperatorMatchesOnAbsence_ThenEventAccepted(
        AdvancedFilterSetting.AdvancedFilterOperatorType operatorType
    )
    {
        EvaluateWithMissingKey(operatorType).ShouldBeTrue(operatorType.ToString());
    }

    [Theory]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotContains)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotBeginsWith)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotEndsWith)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.StringContains)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.StringBeginsWith)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.StringEndsWith)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.NumberInRange)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThan)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.NumberGreaterThanOrEquals)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThan)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.NumberLessThanOrEquals)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.BoolEquals)]
    [InlineData(AdvancedFilterSetting.AdvancedFilterOperatorType.IsNotNull)]
    public void GivenFilterKeyMissingFromEvent_WhenOperatorRequiresPresence_ThenEventRejected(
        AdvancedFilterSetting.AdvancedFilterOperatorType operatorType
    )
    {
        EvaluateWithMissingKey(operatorType).ShouldBeFalse(operatorType.ToString());
    }

    [Fact]
    public void GivenKeyPresentWithNullValue_WhenIsNullOrUndefined_ThenEventAccepted()
    {
        var gridEvent = TestHelpers.CreateValidEventGridEvent(
            data: new { NullableValue = (string?)null }
        );
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .IsNullOrUndefined,
                    Key = "Data.NullableValue",
                },
            ],
        };

        filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue();
    }

    [Fact]
    public void GivenKeyPresentWithNullValue_WhenIsNotNull_ThenEventRejected()
    {
        var gridEvent = TestHelpers.CreateValidEventGridEvent(
            data: new { NullableValue = (string?)null }
        );
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.IsNotNull,
                    Key = "Data.NullableValue",
                },
            ],
        };

        filterConfig.AcceptsEvent(gridEvent).ShouldBeFalse();
    }

    [Fact]
    public void GivenKeyPresentWithValue_WhenIsNotNull_ThenEventAccepted()
    {
        var gridEvent = TestHelpers.CreateValidEventGridEvent(data: new { Name = "StringValue" });
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.IsNotNull,
                    Key = "Data.Name",
                },
            ],
        };

        filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue();
    }
}
