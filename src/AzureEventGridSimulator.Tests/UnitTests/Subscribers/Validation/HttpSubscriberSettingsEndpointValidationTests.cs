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

    [Fact]
    public void GivenInvalidRetryPolicy_WhenValidated_ThenThrowsArgumentException()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
            RetryPolicy = new RetryPolicySettings { MaxDeliveryAttempts = 0 },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "MaxDeliveryAttempts must be between 1 and 30. (Parameter 'MaxDeliveryAttempts')"
        );
    }

    // Validation order for HTTP: name, endpoint present, endpoint is an HTTP(S) URL, then
    // filter, retry policy and dead-letter. Each test below breaks two adjacent rules and
    // pins which message wins, word for word.

    [Fact]
    public void GivenBlankEndpointAndInvalidFilter_WhenValidated_ThenEndpointRequiredErrorIsReported()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "   ",
            Filter = new FilterSetting { AdvancedFilters = [new AdvancedFilterSetting()] },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Endpoint is required for HTTP subscribers. (Parameter 'Endpoint')"
        );
    }

    [Fact]
    public void GivenNonHttpEndpointAndInvalidFilter_WhenValidated_ThenEndpointUrlErrorIsReported()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "ftp://example.com/file",
            Filter = new FilterSetting { AdvancedFilters = [new AdvancedFilterSetting()] },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe(
            "Endpoint must be a valid HTTP or HTTPS URL. (Parameter 'Endpoint')"
        );
    }

    [Fact]
    public void GivenInvalidFilterAndInvalidRetryPolicy_WhenValidated_ThenFilterErrorIsReported()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
            Filter = new FilterSetting { AdvancedFilters = [new AdvancedFilterSetting()] },
            RetryPolicy = new RetryPolicySettings { MaxDeliveryAttempts = 0 },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldBe("A filter key must be provided (Parameter 'Key')");
    }

    [Fact]
    public void GivenDeadLetterWithBlankFolderPath_WhenValidated_ThenDefaultFolderPathIsApplied()
    {
        var deadLetter = new DeadLetterSettings { FolderPath = "" };
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
            DeadLetter = deadLetter,
        };

        settings.Validate();

        deadLetter.FolderPath.ShouldBe("./dead-letters");
    }
}
