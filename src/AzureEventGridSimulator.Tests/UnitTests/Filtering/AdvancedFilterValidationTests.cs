using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

[Trait("Category", "unit")]
public class AdvancedFilterValidationTests
{
    private static SimulatorSettings GetValidSimulatorSettings(AdvancedFilterSetting advancedFilter)
    {
        return new SimulatorSettings
        {
            Topics =
            [
                new TopicSettings
                {
                    Key = "TopicKey",
                    Name = "TopicName",
                    Port = 12345,
                    Subscribers = new SubscribersSettings
                    {
                        Http =
                        [
                            new HttpSubscriberSettings
                            {
                                Name = "SubscriberName",
                                Endpoint = "https://example.com/webhook",
                                Filter = new FilterSetting
                                {
                                    AdvancedFilters = new[] { advancedFilter },
                                },
                            },
                        ],
                    },
                },
            ],
        };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenFilterWithoutAKey_WhenValidated_ThenKeyIsRequired(string? key)
    {
        var filterConfig = new AdvancedFilterSetting { Key = key };
        var exception = Should.Throw<ArgumentException>(() =>
            GetValidSimulatorSettings(filterConfig).Validate()
        );

        exception.ParamName.ShouldBe(nameof(filterConfig.Key));
        exception.Message.ShouldBe("A filter key must be provided (Parameter 'Key')");
    }

    [Fact]
    public void TestFilterValidationWithKey()
    {
        var filterConfig = new AdvancedFilterSetting { Key = "Data" };
        var exception = Should.Throw<ArgumentException>(() =>
            GetValidSimulatorSettings(filterConfig).Validate()
        );

        exception.ParamName.ShouldBe(nameof(filterConfig.Value));
        exception.Message.ShouldBe(
            "Either a Value or a set of Values must be provided (Parameter 'Value')"
        );
    }

    [Fact]
    public void TestFilterValidationWithKeyAndValue()
    {
        Should.NotThrow(() =>
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data", Value = "SomeValue" };
            GetValidSimulatorSettings(filterConfig).Validate();
        });
    }

    [Fact]
    public void TestFilterValidationWithValidLongValue()
    {
        Should.NotThrow(() =>
        {
            var filterConfig = new AdvancedFilterSetting
            {
                Key = "Data",
                Value = "SomeValue".PadLeft(512, 'a'),
            };
            GetValidSimulatorSettings(filterConfig).Validate();
        });
    }

    [Fact]
    public void TestFilterValidationWithOverlyLongValue()
    {
        var filterConfig = new AdvancedFilterSetting
        {
            Key = "Data",
            Value = "SomeValue".PadLeft(513, 'a'),
        };
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            GetValidSimulatorSettings(filterConfig).Validate()
        );

        exception.ParamName.ShouldBe(nameof(filterConfig.Value));
        exception.Message.ShouldBe(
            "Advanced filtering limits strings to 512 characters per string value (Parameter 'Value')"
        );
    }

    [Fact]
    public void TestFilterValidationWithValidLongValues()
    {
        Should.NotThrow(() =>
        {
            var filterConfig = new AdvancedFilterSetting
            {
                Key = "Data",
                Values = new object[] { "SomeValue".PadLeft(512, 'a') },
            };
            GetValidSimulatorSettings(filterConfig).Validate();
        });
    }

    [Fact]
    public void TestFilterValidationWithOverlyLongValues()
    {
        var filterConfig = new AdvancedFilterSetting
        {
            Key = "Data",
            Values = new object[] { "SomeValue".PadLeft(513, 'a') },
        };
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            GetValidSimulatorSettings(filterConfig).Validate()
        );

        exception.ParamName.ShouldBe(nameof(filterConfig.Values));
        exception.Message.ShouldBe(
            "Advanced filtering limits strings to 512 characters per string value (Parameter 'Values')"
        );
    }

    public static TheoryData<AdvancedFilterSetting.AdvancedFilterOperatorType> AllOperators =>
        new(Enum.GetValues<AdvancedFilterSetting.AdvancedFilterOperatorType>());

    [Theory]
    [MemberData(nameof(AllOperators))]
    public void GivenFilterWithFiveValues_WhenValidated_ThenNoExceptionThrown(
        AdvancedFilterSetting.AdvancedFilterOperatorType operatorType
    )
    {
        var filterConfig = new AdvancedFilterSetting
        {
            Key = "Data",
            Values = new object[5],
            OperatorType = operatorType,
        };

        Should.NotThrow(() => GetValidSimulatorSettings(filterConfig).Validate());
    }

    [Theory]
    [MemberData(nameof(AllOperators))]
    public void GivenFilterWithSixValues_WhenValidated_ThenNoExceptionThrown(
        AdvancedFilterSetting.AdvancedFilterOperatorType operatorType
    )
    {
        // Azure Event Grid limits filter values to 25 across all filters per subscription;
        // there is no per-operator five-value limit.
        var filterConfig = new AdvancedFilterSetting
        {
            Key = "Data",
            Values = new object[6],
            OperatorType = operatorType,
        };

        Should.NotThrow(() => GetValidSimulatorSettings(filterConfig).Validate());
    }

    [Fact]
    public void TestFilterValidationWithMoreThan25ValuesInASingleFilter()
    {
        var filterConfig = new AdvancedFilterSetting
        {
            Key = "Data",
            Values = new object[26],
            OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
        };

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            GetValidSimulatorSettings(filterConfig).Validate()
        );

        exception.Message.ShouldBe(
            "Advanced filtering is limited to 25 filter values across all the filters per event grid subscription. (Parameter 'AdvancedFilters')"
        );
    }

    [Fact]
    public void TestFilterValidationCountsEachRangeAsOneValue()
    {
        // Azure counts each [min,max] range as a single filter value (the docs define
        // 'values' for range operators as "an array of ranges"), so 13 ranges = 13
        // values even though they contain 26 numbers.
        var filterConfig = new AdvancedFilterSetting
        {
            Key = "Data",
            Values = Enumerable
                .Range(0, 13)
                .Select(i => (object)new object[] { (double)i, i + 0.5d })
                .ToArray(),
            OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberInRange,
        };

        Should.NotThrow(() => GetValidSimulatorSettings(filterConfig).Validate());
    }

    [Fact]
    public void TestFilterValidationWithMoreThan25Ranges()
    {
        var filterConfig = new AdvancedFilterSetting
        {
            Key = "Data",
            Values = Enumerable
                .Range(0, 26)
                .Select(i => (object)new object[] { (double)i, i + 0.5d })
                .ToArray(),
            OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.NumberInRange,
        };

        Should.Throw<ArgumentOutOfRangeException>(() =>
            GetValidSimulatorSettings(filterConfig).Validate()
        );
    }

    [Fact]
    public void TestFilterValidationWithSingleDepthKey()
    {
        Should.NotThrow(() =>
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data.key1", Value = "SomeValue" };
            GetValidSimulatorSettings(filterConfig).Validate();
        });
    }

    [Fact]
    public void TestFilterValidationWithGrandchildKey()
    {
        // following the announcement here https://azure.microsoft.com/en-us/updates/advanced-filtering-generally-available-in-event-grid/ this should now work
        Should.NotThrow(() =>
        {
            var filterConfig = new AdvancedFilterSetting
            {
                Key = "Data.Key1.SubKey",
                Value = "SomeValue",
            };
            GetValidSimulatorSettings(filterConfig).Validate();
        });
    }
}
