﻿using System;
using System.Collections.Generic;
using AzureEventGridSimulator.Infrastructure.Settings;
using Shouldly;
using Xunit;

namespace UnitTests.Filtering
{
    public class FilterSettingsValidationTests
    {
        private SimulatorSettings GetValidSimulatorSettings(FilterSetting filter)
        {
            return new()
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
                            new()
                            {
                                Name = "SubscriberName",
                                Filter = filter
                            }
                        }.ToArray()
                    }
                }
            };
        }

        private AdvancedFilterSetting GetValidAdvancedFilter()
        {
            return new()
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
            for (var i = 0; i < 6; i++)
            {
                filterConfig.AdvancedFilters.Add(GetValidAdvancedFilter());
            }

            var exception = Should.Throw<ArgumentException>(() => GetValidSimulatorSettings(filterConfig).Validate());

            exception.ParamName.ShouldBe(nameof(filterConfig.AdvancedFilters));
            exception.Message.ShouldBe("Advanced filtering is limited to five advanced filters per event grid subscription. (Parameter 'AdvancedFilters')");
        }
    }
}
