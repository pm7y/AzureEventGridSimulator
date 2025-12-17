using System;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers;

[Trait("Category", "unit")]
public class SubscribersSettingsDuplicateNameValidationTests : SubscribersSettingsTestBase
{
    [Fact]
    public void GivenDuplicateHttpSubscriberNames_WhenValidated_ThenThrowsException()
    {
        var settings = new SubscribersSettings
        {
            Http = new[]
            {
                CreateValidHttpSubscriber("Subscriber1"),
                CreateValidHttpSubscriber("Subscriber1"),
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Duplicate");
        exception.Message.ShouldContain("Subscriber1");
    }

    [Fact]
    public void GivenDuplicateNamesAcrossDifferentTypes_WhenValidated_ThenThrowsException()
    {
        var settings = new SubscribersSettings
        {
            Http = new[] { CreateValidHttpSubscriber("SharedName") },
            ServiceBus = new[] { CreateValidServiceBusSubscriber("SharedName") },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Duplicate");
        exception.Message.ShouldContain("SharedName");
    }

    [Fact]
    public void GivenDuplicateNamesCaseInsensitive_WhenValidated_ThenThrowsException()
    {
        var settings = new SubscribersSettings
        {
            Http = new[]
            {
                CreateValidHttpSubscriber("Subscriber"),
                CreateValidHttpSubscriber("SUBSCRIBER"),
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Duplicate");
    }

    [Fact]
    public void GivenMultipleDuplicateNames_WhenValidated_ThenAllDuplicatesListed()
    {
        var settings = new SubscribersSettings
        {
            Http = new[]
            {
                CreateValidHttpSubscriber("Dup1"),
                CreateValidHttpSubscriber("Dup1"),
                CreateValidHttpSubscriber("Dup2"),
                CreateValidHttpSubscriber("Dup2"),
            },
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());
        exception.Message.ShouldContain("Dup1");
        exception.Message.ShouldContain("Dup2");
    }
}
