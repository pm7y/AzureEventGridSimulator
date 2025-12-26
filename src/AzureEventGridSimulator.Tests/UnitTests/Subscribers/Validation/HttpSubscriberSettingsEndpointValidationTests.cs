using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Validation;

[Trait("Category", "unit")]
public class HttpSubscriberSettingsEndpointValidationTests
{
    [Fact]
    public void GivenMissingEndpoint_WhenDeserialized_ThenThrowsException()
    {
        var json = """{ "name": "TestSubscriber" }""";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<HttpSubscriberSettings>(json));
    }

    [Fact]
    public void GivenEmptyEndpoint_WhenValidated_ThenThrowsException()
    {
        var settings = new HttpSubscriberSettings { Name = "TestSubscriber", Endpoint = "" };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Endpoint");
    }

    [Fact]
    public void GivenWhitespaceEndpoint_WhenValidated_ThenThrowsException()
    {
        var settings = new HttpSubscriberSettings { Name = "TestSubscriber", Endpoint = "   " };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Endpoint");
    }

    [Fact]
    public void GivenInvalidEndpointUrl_WhenValidated_ThenThrowsException()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "not-a-valid-url",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("HTTP or HTTPS");
    }

    [Fact]
    public void GivenFtpEndpoint_WhenValidated_ThenThrowsException()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "ftp://example.com/file",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("HTTP or HTTPS");
    }

    [Fact]
    public void GivenHttpEndpoint_WhenValidated_ThenNoException()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "http://localhost:8080/webhook",
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void GivenHttpsEndpoint_WhenValidated_ThenNoException()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com/webhook",
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void GivenNullFilter_WhenValidated_ThenNoException()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
            Filter = null,
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Fact]
    public void GivenValidFilter_WhenValidated_ThenNoException()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
            Filter = new FilterSetting { IncludedEventTypes = new[] { "Event.Type1" } },
        };

        Should.NotThrow(() => settings.Validate());
    }
}
