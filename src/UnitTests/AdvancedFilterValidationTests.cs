using System;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Settings;
using Xunit;

namespace UnitTests
{
    public class AdvancedFilterValidationTests
    {
        private SimulatorSettings GetValidSimulatorSettings(AdvancedFilterSetting advancedFilter)
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
                            new SubscriptionSettings
                            {
                                Filter = new FilterSetting{ AdvancedFilters = new[]{ advancedFilter } }
                            }
                        }
                    }
                }
            };
        }

        [Fact]
        public void TestDefaultFilterValidation()
        {
            var filterConfig = new AdvancedFilterSetting();
            var exception = Assert.Throws<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.Equal(nameof(filterConfig.Key), exception.ParamName);
            Assert.Equal("A filter key must be provided\r\nParameter name: Key", exception.Message);
        }

        [Fact]
        public void TestFilterValidationWithEmptyKey()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "" };
            var exception = Assert.Throws<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.Equal(nameof(filterConfig.Key), exception.ParamName);
            Assert.Equal("A filter key must be provided\r\nParameter name: Key", exception.Message);
        }

        [Fact]
        public void TestFilterValidationWithWhitespaceKey()
        {
            var filterConfig = new AdvancedFilterSetting { Key = " " };
            var exception = Assert.Throws<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.Equal(nameof(filterConfig.Key), exception.ParamName);
            Assert.Equal("A filter key must be provided\r\nParameter name: Key", exception.Message);
        }

        [Fact]
        public void TestFilterValidationWithKey()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data" };
            var exception = Assert.Throws<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.Equal(nameof(filterConfig.Value), exception.ParamName);
            Assert.Equal("Either a Value or a set of Values must be provided\r\nParameter name: Value", exception.Message);
        }

        [Fact]
        public void TestFilterValidationWithKeyAndValue()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data", Value = "SomeValue" };
            GetValidSimulatorSettings(filterConfig).Validate();
        }

        [Fact]
        public void TestFilterValidationWithValidLongValue()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data", Value = "SomeValue".PadLeft(512, 'a') };
            GetValidSimulatorSettings(filterConfig).Validate();
        }

        [Fact]
        public void TestFilterValidationWithOverlyLongValue()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data", Value = "SomeValue".PadLeft(513, 'a') };
            var exception = Assert.Throws<ArgumentOutOfRangeException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.Equal(nameof(filterConfig.Value), exception.ParamName);
            Assert.Equal("Advanced filtering limits strings to 512 characters per string value\r\nParameter name: Value", exception.Message);
        }

        [Fact]
        public void TestFilterValidationWithValidLongValues()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[] { "SomeValue".PadLeft(512, 'a') } };
            GetValidSimulatorSettings(filterConfig).Validate();
        }

        [Fact]
        public void TestFilterValidationWithOverlyLongValues()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[] { "SomeValue".PadLeft(513, 'a') } };
            var exception = Assert.Throws<ArgumentOutOfRangeException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.Equal(nameof(filterConfig.Values), exception.ParamName);
            Assert.Equal("Advanced filtering limits strings to 512 characters per string value\r\nParameter name: Values", exception.Message);
        }

        [Fact]
        public void TestFilterValidationWithFiveValues()
        {
            foreach (AdvancedFilterSetting.OperatorTypeEnum operatorType in Enum.GetValues(typeof(AdvancedFilterSetting.OperatorTypeEnum)))
            {
                var filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[5], OperatorType = operatorType };
                GetValidSimulatorSettings(filterConfig).Validate();
            }
        }

        [Fact]
        public void TestFilterValidationWithSixValues()
        {
            foreach (AdvancedFilterSetting.OperatorTypeEnum operatorType in Enum.GetValues(typeof(AdvancedFilterSetting.OperatorTypeEnum)))
            {
                var filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[6], OperatorType = operatorType };
                if (new[] { AdvancedFilterSetting.OperatorTypeEnum.NumberIn, AdvancedFilterSetting.OperatorTypeEnum.NumberNotIn, AdvancedFilterSetting.OperatorTypeEnum.StringIn, AdvancedFilterSetting.OperatorTypeEnum.StringNotIn }.Contains(operatorType))
                {
                    var exception = Assert.Throws<ArgumentOutOfRangeException>(GetValidSimulatorSettings(filterConfig).Validate);
                    Assert.Equal(nameof(filterConfig.OperatorType), exception.ParamName);
                    Assert.Equal("Advanced filtering limits filters to five values for in and not in operators\r\nParameter name: OperatorType", exception.Message);
                }
                else
                {
                    GetValidSimulatorSettings(filterConfig).Validate();
                }
            }
        }

        [Fact]
        public void TestFilterValidationWithSingleDepthKey()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data.key1", Value = "SomeValue" };
            GetValidSimulatorSettings(filterConfig).Validate();
        }

        [Fact]
        public void TestFilterValidationWithGrandchildKey()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data.Key1.SubKey", Value = "SomeValue" };
            var exception = Assert.Throws<ArgumentOutOfRangeException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.Equal(nameof(filterConfig.Key), exception.ParamName);
            Assert.Equal("The key can only have one level of nesting (like data.key1)\r\nParameter name: Key", exception.Message);
        }
    }
}
