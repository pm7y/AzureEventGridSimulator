using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities.Management;
using AzureEventGridSimulator.Domain.Services.Management;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Management;

[Trait("Category", "unit")]
public class EventSubscriptionMapperTests
{
    private static readonly EventSubscriptionScope Scope = new("sub-id", "rg", "MyTopic");

    [Fact]
    public void Should_MapWebHookDestination_When_ConvertingFromArm()
    {
        var resource = new ArmEventSubscriptionResource
        {
            Properties = new ArmEventSubscriptionProperties
            {
                Destination = new ArmEventSubscriptionDestination
                {
                    EndpointType = "WebHook",
                    Properties = new ArmEventSubscriptionDestinationProperties
                    {
                        EndpointUrl = "https://example.test/hook",
                    },
                },
            },
        };

        var mapped = EventSubscriptionMapper.TryMapToHttpSubscriber(
            "my-sub",
            resource,
            out var subscriber
        );

        mapped.ShouldBeTrue();
        subscriber!.Name.ShouldBe("my-sub");
        subscriber.Endpoint.ShouldBe("https://example.test/hook");
    }

    [Fact]
    public void Should_MapIncludedEventTypesAndAdvancedFilter_When_ConvertingFromArm()
    {
        var resource = new ArmEventSubscriptionResource
        {
            Properties = new ArmEventSubscriptionProperties
            {
                Destination = new ArmEventSubscriptionDestination
                {
                    EndpointType = "WebHook",
                    Properties = new ArmEventSubscriptionDestinationProperties
                    {
                        EndpointUrl = "https://example.test/hook",
                    },
                },
                Filter = new ArmEventSubscriptionFilter
                {
                    IncludedEventTypes = ["candidate.created"],
                    AdvancedFilters =
                    [
                        new ArmAdvancedFilter
                        {
                            OperatorType = "StringIn",
                            Key = "data.officeId",
                            Values =
                            [
                                JsonSerializer.SerializeToElement("office-1"),
                                JsonSerializer.SerializeToElement("office-2"),
                            ],
                        },
                    ],
                },
            },
        };

        EventSubscriptionMapper.TryMapToHttpSubscriber("my-sub", resource, out var subscriber);

        subscriber!.Filter!.IncludedEventTypes.ShouldBe(["candidate.created"]);
        var advanced = subscriber.Filter.AdvancedFilters!.ShouldHaveSingleItem();
        advanced.OperatorType.ShouldBe(AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn);
        advanced.Key.ShouldBe("data.officeId");
        advanced.Values!.ShouldBe(["office-1", "office-2"]);
    }

    [Fact]
    public void Should_ReturnFalse_When_DestinationIsNotWebHook()
    {
        var resource = new ArmEventSubscriptionResource
        {
            Properties = new ArmEventSubscriptionProperties
            {
                Destination = new ArmEventSubscriptionDestination { EndpointType = "EventHub" },
            },
        };

        EventSubscriptionMapper.TryMapToHttpSubscriber("my-sub", resource, out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_ProduceSucceededWebHookResource_When_ConvertingToArm()
    {
        var subscriber = new HttpSubscriberSettings
        {
            Name = "my-sub",
            Endpoint = "https://example.test/hook",
            Filter = new FilterSetting { IncludedEventTypes = ["candidate.created"] },
        };

        var resource = EventSubscriptionMapper.MapToArm(Scope, subscriber);

        resource.Name.ShouldBe("my-sub");
        resource.Id.ShouldBe(
            "/subscriptions/sub-id/resourceGroups/rg"
                + "/providers/Microsoft.EventGrid/topics/MyTopic/eventSubscriptions/my-sub"
        );
        resource.Properties!.ProvisioningState.ShouldBe("Succeeded");
        resource.Properties.Destination!.EndpointType.ShouldBe("WebHook");
        resource.Properties.Destination.Properties!.EndpointUrl.ShouldBe(
            "https://example.test/hook"
        );
        resource.Properties.Filter!.IncludedEventTypes.ShouldBe(["candidate.created"]);
    }

    [Fact]
    public void Should_MapStorageQueueDestination_When_ConvertingFromArm()
    {
        var resource = new ArmEventSubscriptionResource
        {
            Properties = new ArmEventSubscriptionProperties
            {
                Destination = new ArmEventSubscriptionDestination
                {
                    EndpointType = "StorageQueue",
                    Properties = new ArmEventSubscriptionDestinationProperties
                    {
                        ResourceId =
                            "/subscriptions/sub-id/resourceGroups/rg/providers"
                            + "/Microsoft.Storage/storageAccounts/myaccount",
                        QueueName = "my-queue",
                    },
                },
                Filter = new ArmEventSubscriptionFilter
                {
                    IncludedEventTypes = ["candidate.created"],
                },
            },
        };

        var mapped = EventSubscriptionMapper.TryMapToStorageQueueSubscriber(
            "my-sub",
            resource,
            out var subscriber
        );

        mapped.ShouldBeTrue();
        subscriber!.Name.ShouldBe("my-sub");
        subscriber.QueueName.ShouldBe("my-queue");
        subscriber.SourceResourceId.ShouldBe(
            "/subscriptions/sub-id/resourceGroups/rg/providers"
                + "/Microsoft.Storage/storageAccounts/myaccount"
        );
        subscriber.Filter!.IncludedEventTypes.ShouldBe(["candidate.created"]);
    }

    [Fact]
    public void Should_ReturnFalse_When_DestinationIsNotStorageQueue()
    {
        var resource = new ArmEventSubscriptionResource
        {
            Properties = new ArmEventSubscriptionProperties
            {
                Destination = new ArmEventSubscriptionDestination
                {
                    EndpointType = "WebHook",
                    Properties = new ArmEventSubscriptionDestinationProperties
                    {
                        EndpointUrl = "https://example.test/hook",
                    },
                },
            },
        };

        EventSubscriptionMapper
            .TryMapToStorageQueueSubscriber("my-sub", resource, out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void Should_ReturnFalse_When_StorageQueueDestinationHasNoQueueName()
    {
        var resource = new ArmEventSubscriptionResource
        {
            Properties = new ArmEventSubscriptionProperties
            {
                Destination = new ArmEventSubscriptionDestination
                {
                    EndpointType = "StorageQueue",
                    Properties = new ArmEventSubscriptionDestinationProperties
                    {
                        ResourceId =
                            "/subscriptions/sub-id/resourceGroups/rg/providers"
                            + "/Microsoft.Storage/storageAccounts/myaccount",
                    },
                },
            },
        };

        EventSubscriptionMapper
            .TryMapToStorageQueueSubscriber("my-sub", resource, out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void Should_ProduceSucceededStorageQueueResource_When_ConvertingToArm()
    {
        var subscriber = new StorageQueueSubscriberSettings
        {
            Name = "my-sub",
            QueueName = "my-queue",
            SourceResourceId =
                "/subscriptions/sub-id/resourceGroups/rg/providers"
                + "/Microsoft.Storage/storageAccounts/myaccount",
            Filter = new FilterSetting { IncludedEventTypes = ["candidate.created"] },
        };

        var resource = EventSubscriptionMapper.MapToArm(Scope, subscriber);

        resource.Name.ShouldBe("my-sub");
        resource.Id.ShouldBe(
            "/subscriptions/sub-id/resourceGroups/rg"
                + "/providers/Microsoft.EventGrid/topics/MyTopic/eventSubscriptions/my-sub"
        );
        resource.Properties!.ProvisioningState.ShouldBe("Succeeded");
        resource.Properties.Destination!.EndpointType.ShouldBe("StorageQueue");
        resource.Properties.Destination.Properties!.QueueName.ShouldBe("my-queue");
        resource.Properties.Destination.Properties.ResourceId.ShouldBe(
            "/subscriptions/sub-id/resourceGroups/rg/providers"
                + "/Microsoft.Storage/storageAccounts/myaccount"
        );
        resource.Properties.Filter!.IncludedEventTypes.ShouldBe(["candidate.created"]);
    }
}
