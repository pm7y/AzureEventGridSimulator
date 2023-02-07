namespace AzureEventGridSimulator.Tests.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using Shouldly;
using Xunit;
using static AzureEventGridSimulator.Infrastructure.Settings.AzureServiceBusSubscriptionSettings;
using BrokerPropertyKeys = Domain.Commands.SendNotificationEventsToAzureServiceBusSubscriberCommandHandler<Domain.Entities.IEvent>.BrokerPropertyKeys;

[Trait("Category", "unit")]
public static class SendNotificationEventGridEventsToAzureServiceBusSubscriberCommandHandlerTests
{
    public class CreateMessageTests
    {
        public static TheoryData<string> AllBrokerPropertyKeys
        {
            get
            {
                var data = new TheoryData<string>();
                foreach (var key in BrokerPropertyKeys.AllKeys)
                {
                    data.Add(key);
                }

                return data;
            }
        }

        public static TheoryData<string> AllBrokerPropertyKeysExceptMessageId
        {
            get
            {
                var data = new TheoryData<string>();
                foreach (var key in BrokerPropertyKeys.AllKeys.Where(x => x != BrokerPropertyKeys.MessageId))
                {
                    data.Add(key);
                }

                return data;
            }
        }

        [Theory]
        [MemberData(nameof(AllBrokerPropertyKeys))]
        public void GivenBrokerProperty_WithDynamicValue_ThenDynamicValueShouldBeAddedToBrokerProperties(string propertyName)
        {
            var payload = new
            {
                DynamicValue = Guid.NewGuid().ToString()
            };

            var evt = EventGridEventStub(payload);
            var subscription = SubscriptionStub(
                new Dictionary<string, Property>
                {
                    { propertyName, new Property(PropertyType.Dynamic, "data.DynamicValue") }
                });

            var target = new SendNotificationEventGridEventsToAzureServiceBusSubscriberCommandHandler(null, null);
            var actual = target.CreateMessage(subscription, evt);

            Assert.True(actual.BrokerProperties.ContainsKey(propertyName));
            actual.BrokerProperties[propertyName].ShouldBe(payload.DynamicValue);
        }

        [Theory]
        [MemberData(nameof(AllBrokerPropertyKeysExceptMessageId))]
        public void GivenBrokerProperty_WithStaticValue_ThenDynamicValueShouldBeAddedToBrokerProperties(string propertyName)
        {
            const string staticValue = "SampleStaticValue";

            var payload = new
            {
                DynamicValue = "Anything but static value"
            };

            var evt = EventGridEventStub(payload);
            var subscription = SubscriptionStub(
                new Dictionary<string, Property>
                {
                    { propertyName, new Property(PropertyType.Static, staticValue) }
                });

            var target = new SendNotificationEventGridEventsToAzureServiceBusSubscriberCommandHandler(null, null);
            var actual = target.CreateMessage(subscription, evt);

            Assert.True(actual.BrokerProperties.ContainsKey(propertyName));
            actual.BrokerProperties[propertyName].ShouldBe(staticValue);
        }

        [Fact]
        public void GivenUserProperty_WithDynamicValue_ThenDynamicValueShouldBeAddedToUserProperties()
        {
            const string propertyName = "SampleUserProperty";

            var payload = new
            {
                DynamicValue = Guid.NewGuid().ToString()
            };

            var evt = EventGridEventStub(payload);
            var subscription = SubscriptionStub(
                new Dictionary<string, Property>
                {
                    { propertyName, new Property(PropertyType.Dynamic, "data.DynamicValue") }
                });

            var target = new SendNotificationEventGridEventsToAzureServiceBusSubscriberCommandHandler(null, null);
            var actual = target.CreateMessage(subscription, evt);

            Assert.True(actual.UserProperties.ContainsKey(propertyName));
            actual.UserProperties[propertyName].ShouldBe(payload.DynamicValue);
        }

        [Fact]
        public void GivenUserProperty_WithStaticValue_ThenStaticValueShouldBeAddedToUserProperties()
        {
            const string propertyName = "SampleUserProperty";
            const string staticValue = "SampleStaticValue";

            var payload = new
            {
                DynamicValue = Guid.NewGuid().ToString()
            };

            var evt = EventGridEventStub(payload);
            var subscription = SubscriptionStub(
                new Dictionary<string, Property>
                {
                    { propertyName, new Property(PropertyType.Static, staticValue) }
                });

            var target = new SendNotificationEventGridEventsToAzureServiceBusSubscriberCommandHandler(null, null);
            var actual = target.CreateMessage(subscription, evt);

            Assert.True(actual.UserProperties.ContainsKey(propertyName));
            actual.UserProperties[propertyName].ShouldBe(staticValue);
        }

        [Fact]
        public void GivenStaticMessageId_ThrowException()
        {
            var evt = EventGridEventStub(new { });
            var subscription = SubscriptionStub(
                new Dictionary<string, Property>
                {
                    { BrokerPropertyKeys.MessageId, new Property(PropertyType.Static, "Value") }
                });

            var target = new SendNotificationEventGridEventsToAzureServiceBusSubscriberCommandHandler(null, null);

            Assert.Throws<InvalidOperationException>(() => target.CreateMessage(subscription, evt));
        }

        private static AzureServiceBusSubscriptionSettings SubscriptionStub(
            Dictionary<string, AzureServiceBusSubscriptionSettings.Property> properties,
            string @namespace = "test-namespace",
            string name = "test-subscription",
            string sharedAccessKey = "sak",
            string sharedAccessKeyName = "sakn",
            bool disabled = false,
            string topicName = "topic")
        {
            return new AzureServiceBusSubscriptionSettings
            {
                Namespace = @namespace,
                Name = name,
                SharedAccessKey = sharedAccessKey,
                SharedAccessKeyName = sharedAccessKeyName,
                Disabled = disabled,
                Topic = topicName,
                Properties = properties
            };
        }

        private static EventGridEvent EventGridEventStub<T>(
            T data,
            string id = "id",
            string subject = "subject",
            string eventType = "eventType",
            string dataVersion = "1.0.0",
            string metaDataVersion = "1",
            string topic = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/azureeventgridsimulator/providers/Microsoft.EventGrid/topics/azureeventgridsimulatortopic")
        {
            return new EventGridEvent
            {
                Id = id,
                Subject = subject,
                EventType = eventType,
                DataVersion = dataVersion,
                MetadataVersion = metaDataVersion,
                Topic = topic,
                Data = data
            };
        }
    }
}
