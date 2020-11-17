using System;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Infrastructure.Settings;
using Shouldly;
using Xunit;

namespace UnitTests.Filtering
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
                                Name = "SubscriberName",
                                Filter = new FilterSetting{ AdvancedFilters = new[]{ advancedFilter } }
                            }
                        }.ToArray()
                    }
                }
            };
        }

        [Fact]
        public void TestDefaultFilterValidation()
        {
            var filterConfig = new AdvancedFilterSetting();
            var exception = Should.Throw<ArgumentException>(() => GetValidSimulatorSettings(filterConfig).Validate());

            exception.ParamName.ShouldBe(nameof(filterConfig.Key));
            exception.Message.ShouldBe("A filter key must be provided (Parameter 'Key')");
        }

        [Fact]
        public void TestFilterValidationWithEmptyKey()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "" };
            var exception = Should.Throw<ArgumentException>(() => GetValidSimulatorSettings(filterConfig).Validate());

            exception.ParamName.ShouldBe(nameof(filterConfig.Key));
            exception.Message.ShouldBe("A filter key must be provided (Parameter 'Key')");
        }

        [Fact]
        public void TestFilterValidationWithWhitespaceKey()
        {
            var filterConfig = new AdvancedFilterSetting { Key = " " };
            var exception = Should.Throw<ArgumentException>(() => GetValidSimulatorSettings(filterConfig).Validate());

            exception.ParamName.ShouldBe(nameof(filterConfig.Key));
            exception.Message.ShouldBe("A filter key must be provided (Parameter 'Key')");
        }

        [Fact]
        public void TestFilterValidationWithKey()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data" };
            var exception = Should.Throw<ArgumentException>(() => GetValidSimulatorSettings(filterConfig).Validate());

            exception.ParamName.ShouldBe(nameof(filterConfig.Value));
            exception.Message.ShouldBe("Either a Value or a set of Values must be provided (Parameter 'Value')");
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
                var filterConfig = new AdvancedFilterSetting { Key = "Data", Value = "SomeValue".PadLeft(512, 'a') };
                GetValidSimulatorSettings(filterConfig).Validate();
            });
        }

        [Fact]
        public void TestFilterValidationWithOverlyLongValue()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data", Value = "SomeValue".PadLeft(513, 'a') };
            var exception = Should.Throw<ArgumentOutOfRangeException>(() => GetValidSimulatorSettings(filterConfig).Validate());

            exception.ParamName.ShouldBe(nameof(filterConfig.Value));
            exception.Message.ShouldBe("Advanced filtering limits strings to 512 characters per string value (Parameter 'Value')");
        }

        [Fact]
        public void TestFilterValidationWithValidLongValues()
        {
            Should.NotThrow(() =>
            {
                var filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[] { "SomeValue".PadLeft(512, 'a') } };
                GetValidSimulatorSettings(filterConfig).Validate();
            });
        }

        [Fact]
        public void TestFilterValidationWithOverlyLongValues()
        {
            var filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[] { "SomeValue".PadLeft(513, 'a') } };
            var exception = Should.Throw<ArgumentOutOfRangeException>(() => GetValidSimulatorSettings(filterConfig).Validate());

            exception.ParamName.ShouldBe(nameof(filterConfig.Values));
            exception.Message.ShouldBe("Advanced filtering limits strings to 512 characters per string value (Parameter 'Values')");
        }

        [Fact]
        public void TestFilterValidationWithFiveValues()
        {
            Should.NotThrow(() =>
            {
                foreach (AdvancedFilterSetting.OperatorTypeEnum operatorType in Enum.GetValues(typeof(AdvancedFilterSetting.OperatorTypeEnum)))
                {
                    var filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[5], OperatorType = operatorType };
                    GetValidSimulatorSettings(filterConfig).Validate();
                }
            });
        }

        [Fact]
        public void TestFilterValidationWithSixValues()
        {
            foreach (AdvancedFilterSetting.OperatorTypeEnum operatorType in Enum.GetValues(typeof(AdvancedFilterSetting.OperatorTypeEnum)))
            {
                var filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[6], OperatorType = operatorType };
                if (new[] { AdvancedFilterSetting.OperatorTypeEnum.NumberIn, AdvancedFilterSetting.OperatorTypeEnum.NumberNotIn, AdvancedFilterSetting.OperatorTypeEnum.StringIn, AdvancedFilterSetting.OperatorTypeEnum.StringNotIn }.Contains(operatorType))
                {
                    var exception = Should.Throw<ArgumentOutOfRangeException>(() => GetValidSimulatorSettings(filterConfig).Validate());

                    exception.ParamName.ShouldBe(nameof(filterConfig.OperatorType));
                    exception.Message.ShouldBe("Advanced filtering limits filters to five values for in and not in operators (Parameter 'OperatorType')");
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
                var filterConfig = new AdvancedFilterSetting { Key = "Data.Key1.SubKey", Value = "SomeValue" };
                GetValidSimulatorSettings(filterConfig).Validate();
            });
        }
    }
}
