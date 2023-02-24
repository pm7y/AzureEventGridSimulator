namespace AzureEventGridSimulator.Infrastructure.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class IsDateTimeOffsetValidationAttribute : ValidationAttribute
    {
        private readonly bool _required;
        private readonly string _errorMessage;

        public IsDateTimeOffsetValidationAttribute(string errorMessage = "{0} is not a valid date/time", bool required = false)
        {
            _errorMessage = errorMessage;
            _required = required;
        }

        public override bool IsValid(object value)
        {
            if (value == null)
            {
                return !_required;
            }

            if (value is not string)
            {
                return false;
            }

            return DateTimeOffset.TryParse((string)value, out var _);
        }

        public override string FormatErrorMessage(string name)
        {
            return string.Format(_errorMessage, name);
        }
    }
}
