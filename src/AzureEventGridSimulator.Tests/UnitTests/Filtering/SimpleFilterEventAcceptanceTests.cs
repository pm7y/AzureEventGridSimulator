using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

[Trait("Category", "unit")]
public class SimpleFilterEventAcceptanceTests
{
    [Fact]
    public void TestDefaultFilterSettingsAcceptsDefaultGridEvent()
    {
        var filterConfig = new FilterSetting();
        var gridEvent = TestHelpers.CreateValidEventGridEvent();

        filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData([new[] { "All" }])]
    [InlineData([new[] { "This.is.a.test" }])]
    public void TestEventTypeFilteringSuccess(string[]? includedEventTypes)
    {
        var filterConfig = new FilterSetting { IncludedEventTypes = includedEventTypes };
        var gridEvent = TestHelpers.CreateValidEventGridEvent(eventType: "This.is.a.test");

        filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue();
    }

    [Theory]
    [InlineData([new[] { "This" }])]
    [InlineData([new[] { "this.is.a.test" }])]
    [InlineData([new[] { "THIS.IS.A.TEST" }])]
    [InlineData([new[] { "this.is.a.test.event" }])]
    [InlineData([new[] { "this.is.a.testevent" }])]
    [InlineData([new string[0]])]
    public void TestEventTypeFilteringFailure(string[] includedEventTypes)
    {
        var filterConfig = new FilterSetting { IncludedEventTypes = includedEventTypes };
        var gridEvent = TestHelpers.CreateValidEventGridEvent(eventType: "This.is.a.test");

        filterConfig.AcceptsEvent(gridEvent).ShouldBeFalse();
    }

    [Theory]
    [InlineData("This", null, true)]
    [InlineData("This", null, false)]
    [InlineData("THIS", null, false)]
    [InlineData("this_is_a_test_subject", null, false)]
    [InlineData("This_Is_A_Test_", null, true)]
    [InlineData("T", null, true)]
    [InlineData("t", null, false)]
    [InlineData("", null, true)]
    [InlineData("", null, false)]
    [InlineData(null, null, false)]
    [InlineData(null, null, true)]
    [InlineData("This", "ect", true)]
    [InlineData("This", "eCt", false)]
    [InlineData("this_is_a_test_subject", "this_is_a_test_subject", false)]
    [InlineData("This_Is_A_Test_Subject", "This_Is_A_Test_Subject", true)]
    [InlineData(null, "This_Is_A_Test_Subject", true)]
    [InlineData(null, "_Subject", true)]
    [InlineData(null, "_subject", false)]
    [InlineData(null, "_SUBJECT", false)]
    public void TestSubjectFilteringSuccess(
        string? beginsWith,
        string? endsWith,
        bool caseSensitive
    )
    {
        var filterConfig = new FilterSetting
        {
            SubjectBeginsWith = beginsWith,
            SubjectEndsWith = endsWith,
            IsSubjectCaseSensitive = caseSensitive,
        };
        var gridEvent = TestHelpers.CreateValidEventGridEvent(subject: "This_Is_A_Test_Subject");

        filterConfig.AcceptsEvent(gridEvent).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Thus", null, true)]
    [InlineData("Thus", null, false)]
    [InlineData("TH", null, true)]
    [InlineData("wrong", null, false)]
    [InlineData("t", null, true)]
    [InlineData("This", "wrong", false)]
    [InlineData("This", "eCt", true)]
    [InlineData("this_is_a_test_subject", "this_is_a_test_subject", true)]
    [InlineData("This_Is_A_Test_Subject", "wrong", true)]
    [InlineData(null, "THIS_IS_A_TEST_SUBJECT", true)]
    [InlineData(null, "this_is_a_test_subject", true)]
    [InlineData(null, "_subject", true)]
    [InlineData(null, "_SUBJECT", true)]
    public void TestSubjectFilteringFailure(
        string? beginsWith,
        string? endsWith,
        bool caseSensitive
    )
    {
        var filterConfig = new FilterSetting
        {
            SubjectBeginsWith = beginsWith,
            SubjectEndsWith = endsWith,
            IsSubjectCaseSensitive = caseSensitive,
        };
        var gridEvent = TestHelpers.CreateValidEventGridEvent(subject: "This_Is_A_Test_Subject");

        filterConfig.AcceptsEvent(gridEvent).ShouldBeFalse();
    }
}
