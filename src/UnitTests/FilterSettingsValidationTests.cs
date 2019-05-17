using System;
using System.Collections.Generic;
using AzureEventGridSimulator.Settings;
using NUnit.Framework;

namespace Tests
{
    public class FilterSettingsValidationTests
    {
        private SimulatorSettings GetValidSimulatorSettings(FilterSetting filter)
        {
            return new SimulatorSettings
            {
                Topics = new TopicSettings[]
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

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void TestFilterSettingsValidationWithValidNumberOfAdvancedFilterSettings(byte n)
        {
            var filterConfig = new FilterSetting { AdvancedFilters = new List<AdvancedFilterSetting>() };
            for (byte i = 0; i < n; i++)
            {
                filterConfig.AdvancedFilters.Add(GetValidAdvancedFilter());
            }

            Assert.DoesNotThrow(GetValidSimulatorSettings(filterConfig).Validate);
        }

        [Test]
        public void TestFilterSettingsValidationWithSixAdvancedFilters()
        {
            var filterConfig = new FilterSetting { AdvancedFilters = new List<AdvancedFilterSetting>() };
            for (int i = 0; i < 6; i++)
            {
                filterConfig.AdvancedFilters.Add(GetValidAdvancedFilter());
            }

            var exception = Assert.Catch<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.AreEqual(nameof(filterConfig.AdvancedFilters), exception.ParamName);
            Assert.AreEqual("Advanced filtering is lmited to five advanced filters per event grid subscription\r\nParameter name: AdvancedFilters", exception.Message);
        }
    }
}
