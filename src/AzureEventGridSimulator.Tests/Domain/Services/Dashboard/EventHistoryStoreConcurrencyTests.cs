using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.Domain.Services.Dashboard;

/// <summary>
///     Exercises the thread-safety guarantees of the store: per-topic locks keep
///     the eviction accounting consistent under concurrent writers, and delivery
///     updates are safe to run alongside dashboard reads.
/// </summary>
[Trait("Category", "unit")]
public class EventHistoryStoreConcurrencyTests
{
    private static EventHistoryRecord CreateRecord(string id, string topicName = "test-topic")
    {
        return new EventHistoryRecord
        {
            Id = id,
            ReceivedAt = DateTimeOffset.UtcNow,
            TopicName = topicName,
            TopicPort = 60101,
            EventType = "Test.EventType",
            Subject = "/test/subject",
            Source = "/test/source",
            EventTime = DateTimeOffset.UtcNow.ToString("o"),
            InputSchema = EventSchema.EventGridSchema,
            PayloadJson = "{}",
        };
    }

    [Fact]
    public async Task Add_ConcurrentWritersOnSameTopic_CountStaysAtCapacityAndTotalsAreAccurate()
    {
        const int writers = 8;
        const int eventsPerWriter = 100;
        var store = new EventHistoryStore();

        await Task.WhenAll(
            Enumerable
                .Range(0, writers)
                .Select(w =>
                    Task.Run(() =>
                    {
                        for (var i = 0; i < eventsPerWriter; i++)
                        {
                            store.Add(CreateRecord($"writer{w}-event{i}"));
                        }
                    })
                )
        );

        store.Count.ShouldBe(EventHistoryStore.MaxCapacityPerTopic);
        store.TotalEventsReceived.ShouldBe(writers * eventsPerWriter);
        store.GetByTopic("test-topic").Count.ShouldBe(EventHistoryStore.MaxCapacityPerTopic);
    }

    [Fact]
    public async Task Add_ConcurrentWritersOnDistinctTopics_PerTopicCapacityIsIndependent()
    {
        const int eventsPerTopic = EventHistoryStore.MaxCapacityPerTopic + 50;
        var store = new EventHistoryStore();

        await Task.WhenAll(
            Task.Run(() =>
            {
                for (var i = 0; i < eventsPerTopic; i++)
                {
                    store.Add(CreateRecord($"a-{i}", "topic-a"));
                }
            }),
            Task.Run(() =>
            {
                for (var i = 0; i < eventsPerTopic; i++)
                {
                    store.Add(CreateRecord($"b-{i}", "topic-b"));
                }
            })
        );

        store.GetByTopic("topic-a").Count.ShouldBe(EventHistoryStore.MaxCapacityPerTopic);
        store.GetByTopic("topic-b").Count.ShouldBe(EventHistoryStore.MaxCapacityPerTopic);
        store.Count.ShouldBe(EventHistoryStore.MaxCapacityPerTopic * 2);
    }

    [Fact]
    public void Add_SameEventIdTwice_ReplacesRecordWithoutCorruptingEvictionAccounting()
    {
        var store = new EventHistoryStore();

        store.Add(CreateRecord("duplicate-id"));
        store.Add(CreateRecord("duplicate-id"));

        store.Count.ShouldBe(1);
        store.TotalEventsReceived.ShouldBe(2);

        // Fill to capacity; if the replay had taken a second slot in the order
        // queue the count would drift away from the actual record count.
        for (var i = 0; i < EventHistoryStore.MaxCapacityPerTopic; i++)
        {
            store.Add(CreateRecord($"event-{i}"));
        }

        store.Count.ShouldBe(EventHistoryStore.MaxCapacityPerTopic);
        store.GetByTopic("test-topic").Count.ShouldBe(EventHistoryStore.MaxCapacityPerTopic);
    }

    [Fact]
    public async Task UpdateDelivery_WhileReadersEnumerate_DoesNotThrow()
    {
        const int iterations = 1000;
        var store = new EventHistoryStore();
        store.Add(CreateRecord("event-under-delivery"));

        var writer = Task.Run(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                store.UpdateDelivery(
                    "event-under-delivery",
                    new DeliveryRecord
                    {
                        SubscriberName = $"subscriber-{i % 10}",
                        SubscriberType = "http",
                        Endpoint = "https://localhost/webhook",
                        Status = DeliveryStatus.Delivered,
                    }
                );
            }
        });

        var reader = Task.Run(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                var record = store.Get("event-under-delivery");
                foreach (
                    var delivery in record.ShouldNotBeNullAnd("record was evicted").GetDeliveries()
                )
                {
                    _ = delivery.Attempts.Count;
                }

                _ = store.GetStats(topicsActive: 1);
            }
        });

        await Should.NotThrowAsync(async () => await Task.WhenAll(writer, reader));
    }

    [Fact]
    public async Task AddRejection_ConcurrentWriters_CapacityAndTotalsAreAccurate()
    {
        const int writers = 4;
        const int rejectionsPerWriter = 50;
        var store = new EventHistoryStore();

        await Task.WhenAll(
            Enumerable
                .Range(0, writers)
                .Select(w =>
                    Task.Run(() =>
                    {
                        for (var i = 0; i < rejectionsPerWriter; i++)
                        {
                            store.AddRejection(
                                new RejectedEventRecord
                                {
                                    Id = $"rejection-{w}-{i}",
                                    RejectedAt = DateTimeOffset.UtcNow,
                                    TopicName = "test-topic",
                                    TopicPort = 60101,
                                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                                    ErrorMessage = "Invalid JSON",
                                }
                            );
                        }
                    })
                )
        );

        store.RejectionCount.ShouldBe(EventHistoryStore.MaxRejectedCapacity);
        store.TotalRejections.ShouldBe(writers * rejectionsPerWriter);
        store.GetAllRejections().Count.ShouldBe(EventHistoryStore.MaxRejectedCapacity);
    }
}
