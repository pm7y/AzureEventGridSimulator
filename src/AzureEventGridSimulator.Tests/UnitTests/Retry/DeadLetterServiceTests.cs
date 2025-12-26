using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services.Retry;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Retry;

[Trait("Category", "unit")]
public class DeadLetterServiceTests : IDisposable
{
    private readonly ILogger<DeadLetterService> _logger;
    private readonly DeadLetterService _service;
    private readonly string _tempFolder;

    public DeadLetterServiceTests()
    {
        _logger = Substitute.For<ILogger<DeadLetterService>>();
        _service = new DeadLetterService(_logger);
        _tempFolder = Path.Combine(Path.GetTempPath(), $"dead-letter-tests-{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        // Clean up temp folder
        if (Directory.Exists(_tempFolder))
            Directory.Delete(_tempFolder, true);
    }

    private static PendingDelivery CreatePendingDelivery(
        bool? deadLetterEnabled = true,
        string folderPath = "./dead-letters",
        string? eventId = null,
        string topicName = "TestTopic",
        string subscriberName = "TestSubscriber"
    )
    {
        var subscriber = new HttpSubscriberSettings
        {
            Name = subscriberName,
            Endpoint = "https://example.com/webhook",
            DisableValidation = true,
            ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful,
            DeadLetter = deadLetterEnabled.HasValue
                ? new DeadLetterSettings
                {
                    Enabled = deadLetterEnabled.Value,
                    FolderPath = folderPath,
                }
                : null,
        };

        var topic = new TopicSettings
        {
            Name = topicName,
            Port = 60101,
            Key = "TestKey",
        };

        var evt = SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = eventId ?? Guid.NewGuid().ToString(),
                Subject = "test/subject",
                EventType = "Test.EventType",
                EventTime = DateTimeOffset.UtcNow.ToString("o"),
                DataVersion = "1.0",
                Data = new { test = "data" },
            }
        );

        return new PendingDelivery
        {
            Event = evt,
            Subscriber = subscriber,
            Topic = topic,
            InputSchema = EventSchema.EventGridSchema,
        };
    }

    [Fact]
    public async Task GivenEnabledDeadLetter_WhenWriting_ThenCreatesFile()
    {
        var delivery = CreatePendingDelivery(true, _tempFolder);

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        var expectedFolder = Path.Combine(
            _tempFolder,
            delivery.Topic.Name!,
            delivery.Subscriber.Name!
        );
        Directory.Exists(expectedFolder).ShouldBeTrue();

        var files = Directory.GetFiles(expectedFolder, "*.json");
        files.Length.ShouldBe(1);
    }

    [Fact]
    public async Task GivenDisabledDeadLetter_WhenWriting_ThenDoesNotCreateFile()
    {
        var delivery = CreatePendingDelivery(false, _tempFolder);

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        Directory.Exists(_tempFolder).ShouldBeFalse();
    }

    [Fact]
    public async Task GivenNullDeadLetterSettings_WhenWriting_ThenDoesNotCreateFile()
    {
        var delivery = CreatePendingDelivery(null, _tempFolder);

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        Directory.Exists(_tempFolder).ShouldBeFalse();
    }

    [Fact]
    public async Task GivenDelivery_WhenWriting_ThenFileContainsCorrectJson()
    {
        var delivery = CreatePendingDelivery(true, _tempFolder);
        delivery.AttemptCount = 5;
        delivery.Attempts.Add(
            new DeliveryAttempt(
                5,
                DeliveryOutcome.HttpError,
                DateTimeOffset.UtcNow,
                503,
                "Service Unavailable"
            )
        );

        await _service.WriteDeadLetterAsync(delivery, "MaxDeliveryAttemptsExceeded");

        var expectedFolder = Path.Combine(
            _tempFolder,
            delivery.Topic.Name!,
            delivery.Subscriber.Name!
        );
        var files = Directory.GetFiles(expectedFolder, "*.json");
        var content = await File.ReadAllTextAsync(files[0]);
        var json = JsonDocument.Parse(content);

        json.RootElement.GetProperty("deadLetterReason")
            .GetString()
            .ShouldBe("MaxDeliveryAttemptsExceeded");
        json.RootElement.GetProperty("deliveryAttempts").GetInt32().ShouldBe(5);
        json.RootElement.GetProperty("lastDeliveryOutcome").GetString().ShouldBe("HttpError");
        json.RootElement.GetProperty("lastHttpStatusCode").GetInt32().ShouldBe(503);
        json.RootElement.GetProperty("lastErrorMessage")
            .GetString()
            .ShouldBe("Service Unavailable");
        json.RootElement.GetProperty("topicName").GetString().ShouldBe(delivery.Topic.Name);
        json.RootElement.GetProperty("subscriberName")
            .GetString()
            .ShouldBe(delivery.Subscriber.Name);
        json.RootElement.GetProperty("subscriberType").GetString().ShouldBe("http");
    }

    [Fact]
    public async Task GivenEventGridEvent_WhenWriting_ThenEventPayloadIncluded()
    {
        var delivery = CreatePendingDelivery(true, _tempFolder);

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        var expectedFolder = Path.Combine(
            _tempFolder,
            delivery.Topic.Name!,
            delivery.Subscriber.Name!
        );
        var files = Directory.GetFiles(expectedFolder, "*.json");
        var content = await File.ReadAllTextAsync(files[0]);
        var json = JsonDocument.Parse(content);

        var eventElement = json.RootElement.GetProperty("event");
        eventElement.GetProperty("id").GetString().ShouldNotBeNullOrEmpty();
        eventElement.GetProperty("eventType").GetString().ShouldBe("Test.EventType");
        eventElement.GetProperty("subject").GetString().ShouldBe("test/subject");
    }

    [Fact]
    public async Task GivenDelivery_WhenWriting_ThenFileNameContainsTimestampAndEventId()
    {
        var eventId = "test-event-123";
        var delivery = CreatePendingDelivery(true, _tempFolder, eventId);

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        var expectedFolder = Path.Combine(
            _tempFolder,
            delivery.Topic.Name!,
            delivery.Subscriber.Name!
        );
        var files = Directory.GetFiles(expectedFolder, "*.json");
        var fileName = Path.GetFileName(files[0]);

        fileName.ShouldContain(eventId);
        fileName.ShouldEndWith(".json");
        // Should have timestamp format like 20250115_103000
        fileName.ShouldMatch(@"^\d{8}_\d{6}_.*\.json$");
    }

    [Fact]
    public async Task GivenEventIdWithInvalidChars_WhenWriting_ThenCreatesValidFile()
    {
        // Use characters that are invalid on the current platform
        var invalidChars = Path.GetInvalidFileNameChars();
        var eventId = $"test{invalidChars[0]}event{invalidChars[0]}id";
        var delivery = CreatePendingDelivery(true, _tempFolder, eventId);

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        var expectedFolder = Path.Combine(
            _tempFolder,
            delivery.Topic.Name!,
            delivery.Subscriber.Name!
        );
        var files = Directory.GetFiles(expectedFolder, "*.json");
        files.Length.ShouldBe(1); // File was created successfully

        // Verify the file name doesn't contain the invalid character
        var fileName = Path.GetFileName(files[0]);
        fileName.ShouldNotContain(invalidChars[0].ToString());
    }

    [Fact]
    public async Task GivenVeryLongEventId_WhenWriting_ThenTruncatesFileName()
    {
        var eventId = new string('x', 200);
        var delivery = CreatePendingDelivery(true, _tempFolder, eventId);

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        var expectedFolder = Path.Combine(
            _tempFolder,
            delivery.Topic.Name!,
            delivery.Subscriber.Name!
        );
        var files = Directory.GetFiles(expectedFolder, "*.json");
        files.Length.ShouldBe(1);

        var fileName = Path.GetFileName(files[0]);
        fileName.Length.ShouldBeLessThan(100); // Reasonable file name length
    }

    [Fact]
    public async Task GivenDelivery_WhenWriting_ThenCreatesFolderStructure()
    {
        var topicName = "MyTopic";
        var subscriberName = "MySubscriber";
        var delivery = CreatePendingDelivery(
            true,
            _tempFolder,
            topicName: topicName,
            subscriberName: subscriberName
        );

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        var expectedFolder = Path.Combine(_tempFolder, topicName, subscriberName);
        Directory.Exists(expectedFolder).ShouldBeTrue();
    }

    [Fact]
    public async Task GivenMultipleDeliveries_WhenWriting_ThenCreatesMultipleFiles()
    {
        var delivery1 = CreatePendingDelivery(true, _tempFolder, "event-1");
        var delivery2 = CreatePendingDelivery(true, _tempFolder, "event-2");

        await _service.WriteDeadLetterAsync(delivery1, "Reason1");
        await _service.WriteDeadLetterAsync(delivery2, "Reason2");

        var expectedFolder = Path.Combine(
            _tempFolder,
            delivery1.Topic.Name!,
            delivery1.Subscriber.Name!
        );
        var files = Directory.GetFiles(expectedFolder, "*.json");
        files.Length.ShouldBe(2);
    }

    [Fact]
    public async Task GivenSuccessfulWrite_WhenWriting_ThenLogsWarning()
    {
        var delivery = CreatePendingDelivery(true, _tempFolder);

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        _logger
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => (o.ToString() ?? "").Contains("dead-lettered")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    [Fact]
    public async Task GivenDisabledDeadLetter_WhenWriting_ThenLogsDebug()
    {
        var delivery = CreatePendingDelivery(false, _tempFolder);

        await _service.WriteDeadLetterAsync(delivery, "TestReason");

        _logger
            .Received()
            .Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => (o.ToString() ?? "").Contains("disabled")),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }
}
