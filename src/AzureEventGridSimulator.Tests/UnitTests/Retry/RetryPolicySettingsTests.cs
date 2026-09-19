using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Retry;

[Trait("Category", "unit")]
public class RetryPolicySettingsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public void GivenMaxDeliveryAttemptsInRange_WhenValidated_ThenNoExceptionThrown(
        int maxDeliveryAttempts
    )
    {
        var settings = new RetryPolicySettings { MaxDeliveryAttempts = maxDeliveryAttempts };

        Should.NotThrow(() => settings.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void GivenMaxDeliveryAttemptsOutOfRange_WhenValidated_ThenExceptionThrown(
        int maxDeliveryAttempts
    )
    {
        var settings = new RetryPolicySettings { MaxDeliveryAttempts = maxDeliveryAttempts };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());

        exception.ParamName.ShouldBe(nameof(RetryPolicySettings.MaxDeliveryAttempts));
        exception.Message.ShouldBe(
            "MaxDeliveryAttempts must be between 1 and 30. (Parameter 'MaxDeliveryAttempts')"
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1440)]
    public void GivenEventTimeToLiveInRange_WhenValidated_ThenNoExceptionThrown(
        int eventTimeToLiveInMinutes
    )
    {
        var settings = new RetryPolicySettings
        {
            EventTimeToLiveInMinutes = eventTimeToLiveInMinutes,
        };

        Should.NotThrow(() => settings.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void GivenEventTimeToLiveOutOfRange_WhenValidated_ThenExceptionThrown(
        int eventTimeToLiveInMinutes
    )
    {
        var settings = new RetryPolicySettings
        {
            EventTimeToLiveInMinutes = eventTimeToLiveInMinutes,
        };

        var exception = Should.Throw<ArgumentException>(() => settings.Validate());

        exception.ParamName.ShouldBe(nameof(RetryPolicySettings.EventTimeToLiveInMinutes));
        exception.Message.ShouldBe(
            "EventTimeToLiveInMinutes must be between 1 and 1440. (Parameter 'EventTimeToLiveInMinutes')"
        );
    }
}
