using System;
using System.Collections.Generic;
using AzureEventGridSimulator.Settings;
using Xunit;

namespace UnitTests
{
    public class FilterSettingsValidationTests
    {
        private SimulatorSettings GetValidSimulatorSettings(FilterSetting filter)
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
                        Subscribers = new List<SubscriptionSettings>
                        {
                            new SubscriptionSettings { Filter = filter }
                        }
                    }
                }
            };
        }

        private AdvancedFilterSetting GetValidAdvancedFilter()
        {
            return new AdvancedFilterSetting { Key = "key", OperatorType = AdvancedFilterSetting.OperatorTypeEnum.BoolEquals, Value = true };
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public void TestFilterSettingsValidationWithValidNumberOfAdvancedFilterSettings(byte n)
        {
            var filterConfig = new FilterSetting { AdvancedFilters = new List<AdvancedFilterSetting>() };
            for (byte i = 0; i < n; i++)
            {
                filterConfig.AdvancedFilters.Add(GetValidAdvancedFilter());
            }

            GetValidSimulatorSettings(filterConfig).Validate();
        }

        [Fact]
        public void TestFilterSettingsValidationWithSixAdvancedFilters()
        {
            var filterConfig = new FilterSetting { AdvancedFilters = new List<AdvancedFilterSetting>() };
            for (var i = 0; i < 6; i++)
            {
                filterConfig.AdvancedFilters.Add(GetValidAdvancedFilter());
            }

            var exception = Assert.ThrowsAny<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.Equal(nameof(filterConfig.AdvancedFilters), exception.ParamName);
            Assert.Equal("Advanced filtering is limited to five advanced filters per event grid subscription.\r\nParameter name: AdvancedFilters", exception.Message);
        }
    }
}
