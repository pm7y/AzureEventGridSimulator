using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Management;

[Trait("Category", "unit")]
public class SubscribersSettingsRegistryTests
{
    private static HttpSubscriberSettings Subscriber(string name) =>
        new() { Name = name, Endpoint = $"https://{name}.test/hook" };

    [Fact]
    public void Should_AddSubscriber_When_Upserting()
    {
        var subscribers = new SubscribersSettings();

        subscribers.UpsertHttpSubscriber(Subscriber("a"));

        subscribers.HttpSubscribers.Select(s => s.Name).ShouldBe(["a"]);
    }

    [Fact]
    public void Should_ReplaceByName_When_UpsertingExistingName()
    {
        var subscribers = new SubscribersSettings();
        subscribers.UpsertHttpSubscriber(Subscriber("a"));

        subscribers.UpsertHttpSubscriber(
            new HttpSubscriberSettings { Name = "A", Endpoint = "https://updated.test/hook" }
        );

        var only = subscribers.HttpSubscribers.ShouldHaveSingleItem();
        only.Endpoint.ShouldBe("https://updated.test/hook");
    }

    [Fact]
    public void Should_RemoveByNameCaseInsensitive_When_Removing()
    {
        var subscribers = new SubscribersSettings();
        subscribers.UpsertHttpSubscriber(Subscriber("a"));

        subscribers.RemoveHttpSubscriber("A").ShouldBeTrue();
        subscribers.HttpSubscribers.ShouldBeEmpty();
    }

    [Fact]
    public void Should_ReturnFalse_When_RemovingUnknownSubscriber()
    {
        var subscribers = new SubscribersSettings();

        subscribers.RemoveHttpSubscriber("missing").ShouldBeFalse();
    }

    [Fact]
    public async Task Should_RemainConsistent_When_MutatedConcurrentlyWhileEnumerated()
    {
        var subscribers = new SubscribersSettings();

        // Continuously enumerate the (lock-free) read path while other threads mutate it. Copy-on-write
        // means enumeration must never throw, even under concurrent upserts and removes.
        using var stop = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                _ = subscribers.All.ToList();
                _ = subscribers.HttpSubscribers.Count();
            }
        });

        var writers = Enumerable
            .Range(0, 8)
            .Select(i =>
                Task.Run(() =>
                {
                    for (var j = 0; j < 200; j++)
                    {
                        var name = $"sub-{i}-{j % 10}";
                        subscribers.UpsertHttpSubscriber(Subscriber(name));
                        subscribers.RemoveHttpSubscriber(name);
                    }
                })
            )
            .ToArray();

        await Task.WhenAll(writers);
        await stop.CancelAsync();
        await reader;

        subscribers.HttpSubscribers.ShouldBeEmpty();
    }
}
