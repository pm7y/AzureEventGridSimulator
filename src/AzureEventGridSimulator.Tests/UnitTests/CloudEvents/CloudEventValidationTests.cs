using System;
using AzureEventGridSimulator.Domain.Entities;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.CloudEvents;

[Trait("Category", "unit")]
public class CloudEventValidationTests
{
    [Fact]
    public void GivenValidCloudEvent_WhenValidated_ThenNoExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123"
        };

        Should.NotThrow(() => cloudEvent.Validate());
    }

    [Fact]
    public void GivenValidCloudEventWithOptionalFields_WhenValidated_ThenNoExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
            Time = "2025-01-15T10:30:00Z",
            Subject = "/test/subject",
            DataContentType = "application/json",
            DataSchema = "https://example.com/schema",
            Data = new { Property = "Value" }
        };

        Should.NotThrow(() => cloudEvent.Validate());
    }

    [Fact]
    public void GivenCloudEventWithMissingSpecVersion_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123"
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("SpecVersion");
    }

    [Fact]
    public void GivenCloudEventWithWrongSpecVersion_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "0.3",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123"
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("1.0");
    }

    [Fact]
    public void GivenCloudEventWithMissingType_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Source = "/test/source",
            Id = "test-id-123"
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("Type");
    }

    [Fact]
    public void GivenCloudEventWithMissingSource_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Id = "test-id-123"
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("Source");
    }

    [Fact]
    public void GivenCloudEventWithMissingId_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source"
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("Id");
    }

    [Fact]
    public void GivenCloudEventWithInvalidTime_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
            Time = "not-a-valid-timestamp"
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("Time");
    }

    [Fact]
    public void GivenCloudEventWithBothDataAndDataBase64_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
            Data = new { Property = "Value" },
            DataBase64 = "SGVsbG8gV29ybGQ="
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("mutually exclusive");
    }
}
