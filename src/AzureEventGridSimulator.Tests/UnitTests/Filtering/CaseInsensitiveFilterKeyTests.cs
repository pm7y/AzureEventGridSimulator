using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

/// <summary>
///     Azure resolves filter keys case-insensitively, both for top-level event
///     properties and for nested data paths.
/// </summary>
[Trait("Category", "unit")]
public class CaseInsensitiveFilterKeyTests
{
    private static bool Evaluate(AdvancedFilterSetting filter)
    {
        var gridEvent = TestHelpers.CreateValidEventGridEvent(
            data: new { Name = "StringValue", SubObject = new { Id = 5 } }
        );
        var filterConfig = new FilterSetting { AdvancedFilters = [filter] };

        return filterConfig.AcceptsEvent(gridEvent);
    }

    [Theory]
    [InlineData("EventType")]
    [InlineData("eventtype")]
    [InlineData("EVENTTYPE")]
    public void GivenTopLevelKeyInAnyCase_WhenFilterEvaluated_ThenKeyResolves(string key)
    {
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Key = key,
                    Values = ["Test.EventType"],
                }
            )
            .ShouldBeTrue(key);
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("id")]
    [InlineData("ID")]
    public void GivenIdKeyInAnyCase_WhenFilterEvaluated_ThenKeyResolves(string key)
    {
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Key = key,
                    Values = ["test-id-123"],
                }
            )
            .ShouldBeTrue(key);
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
