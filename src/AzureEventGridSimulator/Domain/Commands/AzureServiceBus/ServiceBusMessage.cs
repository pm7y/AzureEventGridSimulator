namespace AzureEventGridSimulator.Domain.Commands;

using System.Collections.Generic;
using AzureEventGridSimulator.Domain.Entities;

public sealed class ServiceBusMessage<TEvent>
    where TEvent : IEvent
{
    public TEvent Body { get; set; }
    public IDictionary<string, string> BrokerProperties { get; set; } = new Dictionary<string, string>();
    public IDictionary<string, string> UserProperties { get; set; } = new Dictionary<string, string>();
}
