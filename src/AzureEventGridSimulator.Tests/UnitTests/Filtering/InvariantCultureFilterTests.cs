using System.Globalization;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

/// <summary>
///     Numeric filter values must be parsed with the invariant culture. Under
///     cultures like de-DE the string "3.5" would otherwise parse as 35
///     ('.' is the group separator), silently changing filter outcomes.
/// </summary>
[Trait("Category", "unit")]
public class InvariantCultureFilterTests
{
    private static void RunWithCulture(string cultureName, Action action)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    [InlineData("en-AU")]
    public void GivenStringFilterValueWithDecimalPoint_WhenEvaluatedUnderAnyCulture_ThenParsedAsInvariant(
        string cultureName
    )
    {
        var gridEvent = TestHelpers.CreateValidEventGridEvent(data: new { DoubleValue = 3.5 });
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .NumberGreaterThanOrEquals,
                    Key = "Data.DoubleValue",
                    Value = "3.5",
                },
            ],
        };

        // With culture-sensitive parsing under de-DE the filter value "3.5" would
        // become 35 and the comparison (3.5 >= 35) would fail.
        RunWithCulture(
            cultureName,
            () => filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue(cultureName)
        );
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void GivenStringRangeBounds_WhenEvaluatedUnderNonInvariantCulture_ThenParsedAsInvariant(
        string cultureName
    )
    {
        var gridEvent = TestHelpers.CreateValidEventGridEvent(data: new { DoubleValue = 3.5 });
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberInRange,
                    Key = "Data.DoubleValue",
                    Values = [new object[] { "1.5", "4.5" }],
                },
            ],
        };

        // Culture-sensitive parsing would read the range as [15, 45] and exclude 3.5
        RunWithCulture(
            cultureName,
            () => filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue(cultureName)
        );
    }

    [Fact]
    public void GivenNumberNotInRangeFilter_WhenValueOutsideRanges_ThenAcceptedUnderNonInvariantCulture()
    {
        var gridEvent = TestHelpers.CreateValidEventGridEvent(data: new { DoubleValue = 10.5 });
        var filterConfig = new FilterSetting
        {
            AdvancedFilters =
            [
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .NumberNotInRange,
                    Key = "Data.DoubleValue",
                    Values = [new object[] { "1.5", "4.5" }],
                },
            ],
        };

        RunWithCulture("de-DE", () => filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue());
    }
}
