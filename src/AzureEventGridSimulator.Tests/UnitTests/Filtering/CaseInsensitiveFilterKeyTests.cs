using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Filtering;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

/// <summary>
///     Azure resolves filter keys case-insensitively. These tests cover nested data
///     paths; top-level keys (id, eventType, subject, ...) are covered for both schemas
///     by SimulatorEventCaseInsensitiveFilterKeyTests.
/// </summary>
[Trait("Category", "unit")]
public class CaseInsensitiveFilterKeyTests
{
    private static bool Evaluate(AdvancedFilterSetting filter)
    {
        var simulatorEvent = SimulatorEvent.FromEventGridEvent(
            TestHelpers.CreateValidEventGridEvent(
                data: new { Name = "StringValue", SubObject = new { Id = 5 } }
            )
        );
        var filterConfig = new FilterSetting { AdvancedFilters = [filter] };

        return filterConfig.AcceptsEvent(simulatorEvent);
    }

    [Theory]
    [InlineData("Data.Name")]
    [InlineData("data.name")]
    [InlineData("DATA.NAME")]
    public void GivenNestedDataKeyInAnyCase_WhenFilterEvaluated_ThenKeyResolves(string key)
    {
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Key = key,
                    Values = ["StringValue"],
                }
            )
            .ShouldBeTrue(key);
    }

    [Theory]
    [InlineData("Data.SubObject.Id")]
    [InlineData("data.subobject.id")]
    [InlineData("DATA.SUBOBJECT.ID")]
    public void GivenGrandchildKeyInAnyCase_WhenFilterEvaluated_ThenKeyResolves(string key)
    {
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberIn,
                    Key = key,
                    Values = [5d],
                }
            )
            .ShouldBeTrue(key);
    }

    [Fact]
    public void GivenKeyCaseDiffersButValueDoesNotMatch_WhenFilterEvaluated_ThenEventRejected()
    {
        // Case-insensitive key resolution must not loosen the value comparison
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Key = "DATA.NAME",
                    Values = ["SomeOtherValue"],
                }
            )
            .ShouldBeFalse();
    }
}
