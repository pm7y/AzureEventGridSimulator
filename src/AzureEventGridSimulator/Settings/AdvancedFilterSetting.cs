using System;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Extensions;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Settings
{
    public class AdvancedFilterSetting
    {
        [JsonProperty(PropertyName = "operatorType", Required = Required.Always)]
        public OperatorTypeEnum OperatorType { get; set; }

        [JsonProperty(PropertyName = "key", Required = Required.Always)]
        public string Key { get; set; }

        [JsonProperty(PropertyName = "value", Required = Required.DisallowNull)]
        public object Value { get; set; }

        [JsonProperty(PropertyName = "values", Required = Required.DisallowNull)]
        public ICollection<object> Values { get; set; }

        public enum OperatorTypeEnum
        {
            NumberGreaterThan,
            NumberGreaterThanOrEquals,
            NumberLessThan,
            NumberLessThanOrEquals,
            NumberIn,
            NumberNotIn,
            BoolEquals,
            StringContains,
            StringBeginsWith,
            StringEndsWith,
            StringIn,
            StringNotIn
        }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                throw new ArgumentException("A filter key must be provided", nameof(Key));
            }

            if (Value == null && !Values.HasItems())
            {
                throw new ArgumentException("Either a Value or a set of Values must be provided", nameof(Value));
            }

            const short maxStringLength = 512;

            if ((Value as string)?.Length > maxStringLength)
            {
                throw new ArgumentOutOfRangeException(nameof(Value), $"Advanced filtering limits strings to {maxStringLength} characters per string value");
            }

            if (Values?.Any(o => (o as string)?.Length > maxStringLength) == true)
            {
                throw new ArgumentOutOfRangeException(nameof(Values), $"Advanced filtering limits strings to {maxStringLength} characters per string value");
            }

            if (new[] { OperatorTypeEnum.NumberIn, OperatorTypeEnum.NumberNotIn, OperatorTypeEnum.StringIn, OperatorTypeEnum.StringNotIn }.Contains(OperatorType) && Values?.Count > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(OperatorType), "Advanced filtering limits filters to five values for in and not in operators");
            }

            if (Key.Count(c => c == '.') > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(Key), "The key can only have one level of nesting (like data.key1)");
            }
        }

        public override string ToString()
        {
            return string.Join(", ", Key, OperatorType, Value ?? "null", string.Join(", ", Values.HasItems() ? Values.Select(v => v.ToString()) : new[] { "null" }), Guid.NewGuid());
        }
    }
}
