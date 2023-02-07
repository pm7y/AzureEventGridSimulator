namespace AzureEventGridSimulator.Infrastructure.Filters;

using System;
using AzureEventGridSimulator.Infrastructure.Settings;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class EventTypeAttribute : Attribute
{
    public EventTypeAttribute(EventType eventType)
    {
        EventType = eventType;
    }

    public EventType EventType { get; }
}
