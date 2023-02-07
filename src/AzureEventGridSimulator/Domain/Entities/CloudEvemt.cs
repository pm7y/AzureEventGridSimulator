namespace AzureEventGridSimulator.Domain.Entities
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.DataContracts;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using AzureEventGridSimulator.Domain.Converters;
    using Newtonsoft.Json.Linq;

    [TypeConverter(typeof(CloudEventConverter))]
    [DataContract]
    public class CloudEvent : IEvent
    {
        private static readonly IReadOnlyDictionary<string, Func<CloudEvent, object>> _propertyAccessors;

        static CloudEvent()
        {
            var accessors = new Dictionary<string, Func<CloudEvent, object>>(StringComparer.OrdinalIgnoreCase);

            var typeExpression = Expression.Parameter(typeof(CloudEvent));
            foreach (var pi in typeof(CloudEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty))
            {
                var attrib = pi.GetCustomAttribute<DataMemberAttribute>();
                if (attrib == null)
                {
                    continue;
                }

                Debug.Assert(!accessors.ContainsKey(attrib.Name));

                var exp = Expression.Lambda<Func<CloudEvent, object>>(Expression.Property(typeExpression, pi), typeExpression).Compile();
                accessors.Add(attrib.Name, exp);
            }

            _propertyAccessors = accessors;
        }

        [DataMember(Name = CloudEventConstants.Id)]
        [Required(ErrorMessage = "The Id property must be specified in each CloudEvent.")]
        public string Id { get; set; }

        [DataMember(Name = CloudEventConstants.SpecVersion)]
        [RegularExpression("^1.0$", ErrorMessage = "This type only supports specversion '1.0'.")]
        public string SpecVersion { get; set; }

        [DataMember(Name = CloudEventConstants.Source)]
        [Required(ErrorMessage = "The source property must be specified in each CloudEvent.")]
        public string Source { get; set; }

        [DataMember(Name = CloudEventConstants.Type)]
        [Required(ErrorMessage = "The type property must be specified in each CloudEvent.")]
        public string Type { get; set; }

        [DataMember(Name = CloudEventConstants.DataContentType)]
        public string DataContentType { get; set; }

        [DataMember(Name = CloudEventConstants.DataSchema)]
        public string DataSchema { get; set; }

        [DataMember(Name = CloudEventConstants.Subject)]
        public string Subject { get; set; }

        [DataMember(Name = CloudEventConstants.Time)]
        public string Time { get; set; }

        [DataMember(Name = CloudEventConstants.Data)]
        public object Data { get; set; }

        [DataMember(Name = CloudEventConstants.DataBase64)]
        public string DataBase64 { get; set; }

        public IDictionary<string, object> ExtensionAttributes { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        string IEvent.EventType
        {
            get => Type;
            set => Type = value;
        }

        public bool TryGetValue(string key, out object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = null;
                return false;
            }

            if (key.Contains('.'))
            {
                var split = key.Split('.');
                if (string.Equals(split[0], CloudEventConstants.Data, StringComparison.OrdinalIgnoreCase) && Data != null && split.Length > 1)
                {
                    var tmpValue = Data;
                    for (var i = 0; i < split.Length; i++)
                    {
                        // look for the property on the grid event data object
                        if (tmpValue != null && JObject.FromObject(tmpValue).TryGetValue(split[i], out var dataValue))
                        {
                            tmpValue = dataValue.ToObject<object>();
                            if (i == split.Length - 1)
                            {
                                value = tmpValue;
                                return true;
                            }
                        }
                    }
                }
            }

            if (_propertyAccessors.TryGetValue(key, out var expr))
            {
                value = expr(this);
                return true;
            }

            return ExtensionAttributes.TryGetValue(key.ToLower(), out value);
        }

        internal void Validate()
        {
            foreach (var property in GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty))
            {
                foreach (var attrib in property.GetCustomAttributes<ValidationAttribute>())
                {
                    if (!attrib.IsValid(property.GetValue(this)))
                    {
                        throw new JsonException(attrib.ErrorMessage);
                    }
                }
            }

            foreach (var key in ExtensionAttributes.Keys)
            {
                var match = Regex.Match(key, "[^a-z0-9]");
                if (match.Success)
                {
                    throw new JsonException($"Invalid character in extension attribute name: '{match.Value}'. CloudEvent attribute names must consist of lower-case letters ('a' to 'z') or digits ('0' to '9') from the ASCII character set.");
                }
            }
        }
    }

    internal static class CloudEventConstants
    {
        // Reserved property names
        public const string SpecVersion = "specversion";
        public const string Id = "id";
        public const string Source = "source";
        public const string Type = "type";
        public const string DataContentType = "datacontenttype";
        public const string DataSchema = "dataschema";
        public const string Subject = "subject";
        public const string Time = "time";
        public const string Data = "data";
        public const string DataBase64 = "data_base64";
    }
}
