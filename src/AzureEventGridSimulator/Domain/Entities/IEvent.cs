namespace AzureEventGridSimulator.Domain.Entities;

public interface IEvent
{
    string Id { get; set; }
    string Subject { get; set; }
    string EventType { get; set; }

    bool TryGetValue(string key, out object value);
}
