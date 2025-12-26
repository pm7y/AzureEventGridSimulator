using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Validation;

[Trait("Category", "unit")]
public class HttpSubscriberSettingsNameValidationTests
{
    [Fact]
    public void GivenMissingName_WhenDeserialized_ThenThrowsException()
    {
        var json = """{ "endpoint": "https://example.com" }""";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<HttpSubscriberSettings>(json));
    }

    [Fact]
    public void GivenEmptyName_WhenValidated_ThenThrowsException()
    {
        var settings = new HttpSubscriberSettings { Name = "", Endpoint = "https://example.com" };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("name");
    }

    [Fact]
    public void GivenWhitespaceName_WhenValidated_ThenThrowsException()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "   ",
            Endpoint = "https://example.com",
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("name");
    }
}
