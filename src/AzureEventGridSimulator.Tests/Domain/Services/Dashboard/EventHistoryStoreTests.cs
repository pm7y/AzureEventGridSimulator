using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.Domain.Services.Dashboard;

[Trait("Category", "unit")]
public class EventHistoryStoreTests
{
    private static int _recordCounter;
    private readonly EventHistoryStore _store;

    public EventHistoryStoreTests()
    {
        _store = new EventHistoryStore();
    }

    [Fact]
    public void Add_SingleEvent_IncrementsCountAndTotalReceived()
    {
        var record = CreateTestRecord("event-1");

        _store.Add(record);

        _store.Count.ShouldBe(1);
        _store.TotalEventsReceived.ShouldBe(1);
    }

    [Fact]
    public void Add_MultipleEvents_TracksAllEvents()
    {
        _store.Add(CreateTestRecord("event-1"));
        _store.Add(CreateTestRecord("event-2"));
        _store.Add(CreateTestRecord("event-3"));

        _store.Count.ShouldBe(3);
        _store.TotalEventsReceived.ShouldBe(3);
    }

    [Fact]
    public void Add_ExceedsMaxCapacity_EvictsOldestEvents()
    {
        // Add more than max capacity
        for (var i = 0; i < EventHistoryStore.MaxCapacity + 10; i++)
        {
            _store.Add(CreateTestRecord($"event-{i}"));
        }

        _store.Count.ShouldBe(EventHistoryStore.MaxCapacity);
        _store.TotalEventsReceived.ShouldBe(EventHistoryStore.MaxCapacity + 10);
    }

    [Fact]
    public void Add_ExceedsMaxCapacity_OldestEventsAreRemoved()
    {
        for (var i = 0; i < EventHistoryStore.MaxCapacity + 5; i++)
        {
            _store.Add(CreateTestRecord($"event-{i}"));
        }

        // First 5 events should be evicted
        _store.Get("event-0").ShouldBeNull();
        _store.Get("event-4").ShouldBeNull();

        // Event starting from index 5 should still exist
        _store.Get("event-5").ShouldNotBeNull();
        _store.Get($"event-{EventHistoryStore.MaxCapacity + 4}").ShouldNotBeNull();
    }

    [Fact]
    public void Get_ExistingEvent_ReturnsEvent()
    {
        var record = CreateTestRecord("event-1");
        _store.Add(record);

        var retrieved = _store.Get("event-1");

        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe("event-1");
    }

    [Fact]
    public void Get_NonExistingEvent_ReturnsNull()
    {
        var retrieved = _store.Get("non-existing");

        retrieved.ShouldBeNull();
    }

    [Fact]
    public void GetAll_ReturnsEventsOrderedByReceivedTimeDescending()
    {
        _store.Add(CreateTestRecord("event-1"));
        _store.Add(CreateTestRecord("event-2"));
        _store.Add(CreateTestRecord("event-3"));

        var all = _store.GetAll();

        all.Count.ShouldBe(3);
        // Most recent should be first (event-3)
        all[0].Id.ShouldBe("event-3");
        all[2].Id.ShouldBe("event-1");
    }

    [Fact]
    public void GetByTopic_FiltersEventsByTopicName()
    {
        _store.Add(CreateTestRecord("event-1", "topic-a"));
        _store.Add(CreateTestRecord("event-2", "topic-b"));
        _store.Add(CreateTestRecord("event-3", "topic-a"));

        var topicAEvents = _store.GetByTopic("topic-a");

        topicAEvents.Count.ShouldBe(2);
        topicAEvents.ShouldAllBe(e => e.TopicName == "topic-a");
    }

    [Fact]
    public void GetByTopic_IsCaseInsensitive()
    {
        _store.Add(CreateTestRecord("event-1", "MyTopic"));

        var result = _store.GetByTopic("mytopic");

        result.Count.ShouldBe(1);
    }

    [Fact]
    public void UpdateDelivery_ExistingEvent_AddsDeliveryRecord()
    {
        var record = CreateTestRecord("event-1");
        _store.Add(record);

        var delivery = new DeliveryRecord
        {
            SubscriberName = "test-subscriber",
            SubscriberType = "http",
            Endpoint = "https://example.com",
            Status = DeliveryStatus.Delivered,
        };

        _store.UpdateDelivery("event-1", delivery);

        var updated = _store.Get("event-1");
        updated.ShouldNotBeNull();
        updated.GetDeliveries().Count.ShouldBe(1);
        updated.GetDeliveries()[0].SubscriberName.ShouldBe("test-subscriber");
    }

    [Fact]
    public void UpdateDelivery_NonExistingEvent_DoesNotThrow()
    {
        var delivery = new DeliveryRecord
        {
            SubscriberName = "test-subscriber",
            SubscriberType = "http",
            Endpoint = "https://example.com",
        };

        Should.NotThrow(() => _store.UpdateDelivery("non-existing", delivery));
    }

    [Fact]
    public void GetStats_ReturnsCorrectStatistics()
    {
        _store.Add(CreateTestRecord("event-1"));
        _store.Add(CreateTestRecord("event-2"));

        var stats = _store.GetStats(3);

        stats.TotalEventsReceived.ShouldBe(2);
        stats.EventsInHistory.ShouldBe(2);
        stats.TopicsActive.ShouldBe(3);
    }

    [Fact]
    public void GetStats_CountsDeliveryStatuses()
    {
        var record1 = CreateTestRecord("event-1");
        record1.AddOrUpdateDelivery(
            new DeliveryRecord
            {
                SubscriberName = "sub-1",
                SubscriberType = "http",
                Endpoint = "https://example.com",
                Status = DeliveryStatus.Delivered,
            }
        );

        var record2 = CreateTestRecord("event-2");
        record2.AddOrUpdateDelivery(
            new DeliveryRecord
            {
                SubscriberName = "sub-2",
                SubscriberType = "http",
                Endpoint = "https://example.com",
                Status = DeliveryStatus.Failed,
            }
        );

        var record3 = CreateTestRecord("event-3");
        record3.AddOrUpdateDelivery(
            new DeliveryRecord
            {
                SubscriberName = "sub-3",
                SubscriberType = "http",
                Endpoint = "https://example.com",
                Status = DeliveryStatus.Pending,
            }
        );

        _store.Add(record1);
        _store.Add(record2);
        _store.Add(record3);

        var stats = _store.GetStats(1);

        stats.TotalDelivered.ShouldBe(1);
        stats.TotalFailed.ShouldBe(1);
        stats.TotalPending.ShouldBe(1);
    }

    [Fact]
    public void Clear_RemovesAllEvents()
    {
        _store.Add(CreateTestRecord("event-1"));
        _store.Add(CreateTestRecord("event-2"));

        _store.Clear();

        _store.Count.ShouldBe(0);
        _store.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Clear_ResetsTotalEventsReceived()
    {
        _store.Add(CreateTestRecord("event-1"));
        _store.Add(CreateTestRecord("event-2"));

        _store.Clear();

        // Total received should be reset to zero
        _store.TotalEventsReceived.ShouldBe(0);
    }

    private static EventHistoryRecord CreateTestRecord(string id, string topicName = "test-topic")
    {
        _recordCounter++;
        return new EventHistoryRecord
        {
            Id = id,
            ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(_recordCounter),
            TopicName = topicName,
            TopicPort = 60101,
            EventType = "Test.EventType",
            Subject = "/test/subject",
            Source = "/test/source",
            EventTime = DateTime.UtcNow.ToString("o"),
            InputSchema = EventSchema.EventGridSchema,
            PayloadJson = "{}",
        };
    }
}
