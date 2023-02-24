using System;
using System.Collections.Generic;
using AzureEventGridSimulator.Infrastructure.Settings;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

[Trait("Category", "unit")]
public class FilterSettingsValidationTests
{
    private static SimulatorSettings GetValidSimulatorSettings(FilterSetting filter)
    {
        return new SimulatorSettings
        {
            Topics = new[]
            {
                new TopicSettings
                {
                    Key = "TopicKey",
                    Name = "TopicName",
                    Port = 12345,
                    Subscribers = new SubscriberSettings{
                        Http = new List<HttpSubscriptionSettings>
                            {
                                new()
                                {
                                    Name = "SubscriberName",
                                    Filter = filter
                                }
                            }.ToArray()
                    }
                }
            }
        };
    }

    private static AdvancedFilterSetting GetValidAdvancedFilter()
    {
        return new AdvancedFilterSetting
        {
            Key = "key",
            OperatorType = AdvancedFilterSetting.OperatorTypeEnum.BoolEquals,
            Value = true
        };
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void TestFilterSettingsValidationWithValidNumberOfAdvancedFilterSettings(byte n)
    {
        Should.NotThrow(() =>
        {
            var filterConfig = new FilterSetting { AdvancedFilters = new List<AdvancedFilterSetting>() };
            for (byte i = 0; i < n; i++)
            {
                filterConfig.AdvancedFilters.Add(GetValidAdvancedFilter());
            }

            GetValidSimulatorSettings(filterConfig).Validate();
        });
    }

    [Fact]
    public void TestFilterSettingsValidationWithSixAdvancedFilters()
    {
        var filterConfig = new FilterSetting { AdvancedFilters = new List<AdvancedFilterSetting>() };
        for (var i = 0; i < 26; i++)
        {
            filterConfig.AdvancedFilters.Add(GetValidAdvancedFilter());
        }

        var exception = Should.Throw<ArgumentException>(() => GetValidSimulatorSettings(filterConfig).Validate());

        exception.ParamName.ShouldBe(nameof(filterConfig.AdvancedFilters));
        exception.Message.ShouldBe("Advanced filtering is limited to twenty-five advanced filters per event grid subscription. (Parameter 'AdvancedFilters')");
    }
}
