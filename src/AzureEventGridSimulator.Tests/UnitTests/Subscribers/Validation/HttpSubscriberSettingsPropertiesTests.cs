using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Subscribers.Validation;

[Trait("Category", "unit")]
public class HttpSubscriberSettingsPropertiesTests
{
    [Fact]
    public void GivenHttpSubscriber_ThenSubscriberTypeIsHttp()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
        };

        settings.SubscriberType.ShouldBe("http");
    }

    [Fact]
    public void GivenSameEndpoint_ThenValidationCodeIsSame()
    {
        var settings1 = new HttpSubscriberSettings
        {
            Name = "Subscriber1",
            Endpoint = "https://example.com/webhook",
        };

        var settings2 = new HttpSubscriberSettings
        {
            Name = "Subscriber2",
            Endpoint = "https://example.com/webhook",
        };

        settings1.ValidationCode.ShouldBe(settings2.ValidationCode);
    }

    [Fact]
    public void GivenDifferentEndpoints_ThenValidationCodesAreDifferent()
    {
        var settings1 = new HttpSubscriberSettings
        {
            Name = "Subscriber1",
            Endpoint = "https://example.com/webhook1",
        };

        var settings2 = new HttpSubscriberSettings
        {
            Name = "Subscriber2",
            Endpoint = "https://example.com/webhook2",
        };

        settings1.ValidationCode.ShouldNotBe(settings2.ValidationCode);
    }

    [Fact]
    public void GivenEndpoint_ThenValidationCodeIsNotEmpty()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com/webhook",
        };

        settings.ValidationCode.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void GivenEndpoint_ThenGetValidationCodeReturnsConsistentResult()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com/webhook",
        };

        var code1 = settings.GetValidationCode();
        var code2 = settings.GetValidationCode();

        code1.ShouldBe(code2);
        code1.ShouldBe(settings.ValidationCode);
    }

    [Fact]
    public void GivenNewSubscriber_ThenValidationStatusIsDefault()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
        };

        settings.ValidationStatus.ShouldBe(default);
    }

    [Fact]
    public void GivenSubscriber_WhenValidationStatusSet_ThenStatusIsStored()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
            ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful,
        };

        settings.ValidationStatus.ShouldBe(SubscriptionValidationStatus.ValidationSuccessful);
    }

    [Fact]
    public void GivenNewSubscriber_ThenDisableValidationIsFalse()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
        };

        settings.DisableValidation.ShouldBeFalse();
    }

    [Fact]
    public void GivenSubscriber_WhenDisableValidationSet_ThenValueIsStored()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
            DisableValidation = true,
        };

        settings.DisableValidation.ShouldBeTrue();
    }

    [Fact]
    public void GivenNewSubscriber_ThenDeliverySchemaIsNull()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
        };

        settings.DeliverySchema.ShouldBeNull();
    }

    [Fact]
    public void GivenSubscriber_WhenDeliverySchemaSet_ThenValueIsStored()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
            DeliverySchema = EventSchema.CloudEventV1_0,
        };

        settings.DeliverySchema.ShouldBe(EventSchema.CloudEventV1_0);
    }

    [Fact]
    public void GivenNewSubscriber_ThenDisabledIsFalse()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
        };

        settings.Disabled.ShouldBeFalse();
    }

    [Fact]
    public void GivenSubscriber_WhenDisabledSet_ThenValueIsStored()
    {
        var settings = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com",
            Disabled = true,
        };

        settings.Disabled.ShouldBeTrue();
    }
}
