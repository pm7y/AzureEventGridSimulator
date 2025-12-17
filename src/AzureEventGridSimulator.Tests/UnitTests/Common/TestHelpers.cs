using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Microsoft.AspNetCore.Http;

namespace AzureEventGridSimulator.Tests.UnitTests.Common;

/// <summary>
/// Shared test helper methods for creating test objects.
/// </summary>
public static class TestHelpers
{
    #region HttpContext Helpers

    /// <summary>
    /// Creates a basic HttpContext with the specified content type.
    /// </summary>
    public static HttpContext CreateHttpContext(string contentType = "application/json")
    {
        return new DefaultHttpContext { Request = { ContentType = contentType } };
    }

    /// <summary>
    /// Creates an HttpContext configured for CloudEvents binary mode.
    /// </summary>
    public static HttpContext CreateCloudEventsBinaryModeContext(
        string specVersion = "1.0",
        string type = "com.example.test",
        string source = "/test/source",
        string id = "test-id-123",
        string time = null,
        string subject = null,
        string dataContentType = null,
        string dataSchema = null
    )
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                ContentType = "application/json",
                Headers =
                {
                    [Constants.CeSpecVersionHeader] = specVersion,
                    [Constants.CeTypeHeader] = type,
                    [Constants.CeSourceHeader] = source,
                    [Constants.CeIdHeader] = id,
                },
            },
        };

        if (time != null)
        {
            context.Request.Headers[Constants.CeTimeHeader] = time;
        }

        if (subject != null)
        {
            context.Request.Headers[Constants.CeSubjectHeader] = subject;
        }

        if (dataContentType != null)
        {
            context.Request.Headers[Constants.CeDataContentTypeHeader] = dataContentType;
        }

        if (dataSchema != null)
        {
            context.Request.Headers[Constants.CeDataSchemaHeader] = dataSchema;
        }

        return context;
    }

    /// <summary>
    /// Creates an HttpContext configured for CloudEvents structured mode.
    /// </summary>
    public static HttpContext CreateCloudEventsStructuredModeContext()
    {
        return new DefaultHttpContext
        {
            Request = { ContentType = "application/cloudevents+json" },
        };
    }

    /// <summary>
    /// Creates an HttpContext configured for CloudEvents batch mode.
    /// </summary>
    public static HttpContext CreateCloudEventsBatchModeContext()
    {
        return new DefaultHttpContext
        {
            Request = { ContentType = "application/cloudevents-batch+json" },
        };
    }

    #endregion

    #region Event Helpers

    /// <summary>
    /// Creates a valid EventGridEvent for testing.
    /// </summary>
    public static EventGridEvent CreateValidEventGridEvent(
        string id = "test-id-123",
        string subject = "/test/subject",
        string eventType = "Test.EventType",
        string eventTime = "2025-01-15T10:30:00Z",
        string dataVersion = "1.0",
        object data = null
    )
    {
        return new EventGridEvent
        {
            Id = id,
            Subject = subject,
            EventType = eventType,
            EventTime = eventTime,
            DataVersion = dataVersion,
            Data = data ?? new { Property = "Value" },
        };
    }

    /// <summary>
    /// Creates a valid CloudEvent for testing.
    /// </summary>
    public static CloudEvent CreateValidCloudEvent(
        string specVersion = "1.0",
        string type = "com.example.test",
        string source = "/test/source",
        string id = "test-id-123",
        string time = null,
        string subject = null,
        object data = null
    )
    {
        return new CloudEvent
        {
            SpecVersion = specVersion,
            Type = type,
            Source = source,
            Id = id,
            Time = time,
            Subject = subject,
            Data = data,
        };
    }

    /// <summary>
    /// Creates a SimulatorEvent from an EventGridEvent.
    /// </summary>
    public static SimulatorEvent CreateSimulatorEventFromEventGrid(
        string id = "test-id-123",
        string subject = "/test/subject",
        string eventType = "Test.EventType",
        string eventTime = "2025-01-15T10:30:00Z",
        string dataVersion = "1.0",
        object data = null
    )
    {
        return SimulatorEvent.FromEventGridEvent(
            CreateValidEventGridEvent(id, subject, eventType, eventTime, dataVersion, data)
        );
    }

    /// <summary>
    /// Creates a SimulatorEvent from a CloudEvent.
    /// </summary>
    public static SimulatorEvent CreateSimulatorEventFromCloudEvent(
        string specVersion = "1.0",
        string type = "com.example.test",
        string source = "/test/source",
        string id = "test-id-123",
        string time = null,
        string subject = null,
        object data = null
    )
    {
        return SimulatorEvent.FromCloudEvent(
            CreateValidCloudEvent(specVersion, type, source, id, time, subject, data)
        );
    }

    #endregion

    #region Subscriber Settings Helpers

    /// <summary>
    /// Creates valid ServiceBusSubscriberSettings for testing.
    /// </summary>
    public static ServiceBusSubscriberSettings CreateValidServiceBusSettings(
        string name = "TestSubscriber",
        string queue = "my-queue",
        string topic = null
    )
    {
        return new ServiceBusSubscriberSettings
        {
            Name = name,
            ConnectionString =
                "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
            Queue = topic == null ? queue : null,
            Topic = topic,
        };
    }

    /// <summary>
    /// Creates valid StorageQueueSubscriberSettings for testing.
    /// </summary>
    public static StorageQueueSubscriberSettings CreateValidStorageQueueSettings(
        string name = "TestSubscriber",
        string queueName = "my-queue"
    )
    {
        return new StorageQueueSubscriberSettings
        {
            Name = name,
            ConnectionString =
                "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net",
            QueueName = queueName,
        };
    }

    /// <summary>
    /// Creates valid TopicSettings for testing.
    /// </summary>
    public static TopicSettings CreateValidTopicSettings(
        string name = "TestTopic",
        int port = 60101,
        string key = "TheLocal+DevelopmentKey="
    )
    {
        return new TopicSettings
        {
            Name = name,
            Port = port,
            Key = key,
        };
    }

    #endregion

    #region Test Constants

    /// <summary>
    /// Test connection string for Service Bus.
    /// </summary>
    public const string TestServiceBusConnectionString =
        "Endpoint=sb://test-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123";

    /// <summary>
    /// Test connection string for Storage Queue.
    /// </summary>
    public const string TestStorageQueueConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=teststorage;AccountKey=abc123;EndpointSuffix=core.windows.net";

    #endregion
}
