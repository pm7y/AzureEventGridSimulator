using System;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Settings;
using NUnit.Framework;
using static AzureEventGridSimulator.Settings.AdvancedFilterSetting;

namespace Tests
{
    public class AdvancedFilterValidationTests
    {
        private SimulatorSettings GetValidSimulatorSettings(AdvancedFilterSetting advancedFilter)
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
                            new SubscriptionSettings
                            {
                                Filter = new FilterSetting{ AdvancedFilters = new AdvancedFilterSetting[]{ advancedFilter } }
                            }
                        }
                    }
                }
            };
        }

        [Test]
        public void TestDefaultFilterValidation()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting();
            var exception = Assert.Catch<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.AreEqual(nameof(filterConfig.Key), exception.ParamName);
            Assert.AreEqual("A filter key must be provided\r\nParameter name: Key", exception.Message);
        }

        [Test]
        public void TestFilterValidationWithEmptyKey()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "" };
            var exception = Assert.Catch<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.AreEqual(nameof(filterConfig.Key), exception.ParamName);
            Assert.AreEqual("A filter key must be provided\r\nParameter name: Key", exception.Message);
        }

        [Test]
        public void TestFilterValidationWithWhitespaceKey()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = " " };
            var exception = Assert.Catch<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.AreEqual(nameof(filterConfig.Key), exception.ParamName);
            Assert.AreEqual("A filter key must be provided\r\nParameter name: Key", exception.Message);
        }

        [Test]
        public void TestFilterValidationWithKey()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data" };
            var exception = Assert.Catch<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.AreEqual(nameof(filterConfig.Value), exception.ParamName);
            Assert.AreEqual("Either a Value or a set of Values must be provided\r\nParameter name: Value", exception.Message);
        }

        [Test]
        public void TestFilterValidationWithKeyAndValue()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data", Value = "SomeValue" };
            Assert.DoesNotThrow(GetValidSimulatorSettings(filterConfig).Validate);
        }

        [Test]
        public void TestFilterValidationWithValidLongValue()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data", Value = "SomeValue".PadLeft(512, 'a') };
            Assert.DoesNotThrow(GetValidSimulatorSettings(filterConfig).Validate);
        }

        [Test]
        public void TestFilterValidationWithOverlyLongValue()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data", Value = "SomeValue".PadLeft(513, 'a') };
            var exception = Assert.Catch<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.AreEqual(nameof(filterConfig.Value), exception.ParamName);
            Assert.AreEqual("Advanced filtering limits strings to 512 characters per string value\r\nParameter name: Value", exception.Message);
        }

        [Test]
        public void TestFilterValidationWithValidLongValues()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[] { "SomeValue".PadLeft(512, 'a') } };
            Assert.DoesNotThrow(GetValidSimulatorSettings(filterConfig).Validate);
        }

        [Test]
        public void TestFilterValidationWithOverlyLongValues()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[] { "SomeValue".PadLeft(513, 'a') } };
            var exception = Assert.Catch<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.AreEqual(nameof(filterConfig.Values), exception.ParamName);
            Assert.AreEqual("Advanced filtering limits strings to 512 characters per string value\r\nParameter name: Values", exception.Message);
        }

        [Test]
        public void TestFilterValidationWithFiveValues()
        {
            foreach (OperatorTypeEnum operatorType in Enum.GetValues(typeof(OperatorTypeEnum)))
            {
                AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[5], OperatorType = operatorType };
                Assert.DoesNotThrow(GetValidSimulatorSettings(filterConfig).Validate);
            }
        }

        [Test]
        public void TestFilterValidationWithSixValues()
        {
            foreach (OperatorTypeEnum operatorType in Enum.GetValues(typeof(OperatorTypeEnum)))
            {
                AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data", Values = new object[6], OperatorType = operatorType };
                if (new OperatorTypeEnum[] { OperatorTypeEnum.NumberIn, OperatorTypeEnum.NumberNotIn, OperatorTypeEnum.StringIn, OperatorTypeEnum.StringNotIn }.Contains(operatorType))
                {
                    var exception = Assert.Catch<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
                    Assert.AreEqual(nameof(filterConfig.OperatorType), exception.ParamName);
                    Assert.AreEqual("Advanced filtering limits filters to five values for in and not in operators\r\nParameter name: OperatorType", exception.Message);
                }
                else
                {
                    Assert.DoesNotThrow(GetValidSimulatorSettings(filterConfig).Validate);
                }
            }
        }

        [Test]
        public void TestFilterValidationWithSingleDepthKey()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data.key1", Value = "SomeValue" };
            Assert.DoesNotThrow(GetValidSimulatorSettings(filterConfig).Validate);
        }

        [Test]
        public void TestFilterValidationWithGrandchildKey()
        {
            AdvancedFilterSetting filterConfig = new AdvancedFilterSetting { Key = "Data.Key1.SubKey", Value = "SomeValue" };
            var exception = Assert.Catch<ArgumentException>(GetValidSimulatorSettings(filterConfig).Validate);
            Assert.AreEqual(nameof(filterConfig.Key), exception.ParamName);
            Assert.AreEqual("The key can only have one level of nesting (like data.key1)\r\nParameter name: Key", exception.Message);
        }
    }
}
