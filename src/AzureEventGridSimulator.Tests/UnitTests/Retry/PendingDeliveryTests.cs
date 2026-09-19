using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Retry;

[Trait("Category", "unit")]
public class PendingDeliveryTests
{
    private static readonly DateTimeOffset FixedTime = new(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);

    // With no retry policy the subscriber has none, so PendingDelivery falls back to the
    // RetryPolicySettings defaults
    private static PendingDelivery CreatePendingDelivery(
        RetryPolicySettings? retryPolicy = null,
        DateTimeOffset? enqueuedTime = null
    )
    {
        var subscriber = new HttpSubscriberSettings
        {
            Name = "TestSubscriber",
            Endpoint = "https://example.com/webhook",
            DisableValidation = true,
            ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful,
            RetryPolicy = retryPolicy,
        };

        var topic = new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TestKey",
        };

        var evt = SimulatorEvent.FromEventGridEvent(
            new EventGridEvent
            {
                Id = Guid.NewGuid().ToString(),
                Subject = "test/subject",
                EventType = "Test.EventType",
                EventTime = FixedTime.ToString("o"),
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
            EnqueuedTime = enqueuedTime ?? FixedTime,
            NextAttemptTime = enqueuedTime ?? FixedTime,
        };
    }

    [Fact]
    public void GivenNewDelivery_WhenChecking_ThenIsNotExpired()
    {
        var delivery = CreatePendingDelivery(
            new RetryPolicySettings { EventTimeToLiveInMinutes = 60 },
            FixedTime
        );

        delivery.IsExpired(FixedTime).ShouldBeFalse();
    }

    [Fact]
    public void GivenDeliveryOlderThanTtl_WhenChecking_ThenIsExpired()
    {
        var delivery = CreatePendingDelivery(
            new RetryPolicySettings { EventTimeToLiveInMinutes = 60 },
            FixedTime.AddMinutes(-61)
        );

        delivery.IsExpired(FixedTime).ShouldBeTrue();
    }

    [Fact]
    public void GivenDeliveryEnqueuedExactlyOneTtlAgo_WhenChecking_ThenIsNotExpired()
    {
        var delivery = CreatePendingDelivery(
            new RetryPolicySettings { EventTimeToLiveInMinutes = 60 },
            FixedTime.AddMinutes(-60)
        );

        // Expiry is strictly after the TTL, so the exact boundary is still live
        delivery.IsExpired(FixedTime).ShouldBeFalse();
    }

    [Fact]
    public void GivenDeliveryEnqueuedOneTickMoreThanOneTtlAgo_WhenChecking_ThenIsExpired()
    {
        var delivery = CreatePendingDelivery(
            new RetryPolicySettings { EventTimeToLiveInMinutes = 60 },
            FixedTime.AddMinutes(-60).AddTicks(-1)
        );

        delivery.IsExpired(FixedTime).ShouldBeTrue();
    }

    [Fact]
    public void GivenDeliveryWithShortTtl_WhenChecking_ThenExpiredQuickly()
    {
        var delivery = CreatePendingDelivery(
            new RetryPolicySettings { EventTimeToLiveInMinutes = 1 },
            FixedTime.AddMinutes(-2)
        );

        delivery.IsExpired(FixedTime).ShouldBeTrue();
    }

    [Fact]
    public void GivenNoAttempts_WhenChecking_ThenHasNotReachedMax()
    {
        var delivery = CreatePendingDelivery(new RetryPolicySettings { MaxDeliveryAttempts = 30 });
        delivery.AttemptCount = 0;

        delivery.HasReachedMaxAttempts.ShouldBeFalse();
    }

    [Fact]
    public void GivenAttemptCountBelowMax_WhenChecking_ThenHasNotReachedMax()
    {
        var delivery = CreatePendingDelivery(new RetryPolicySettings { MaxDeliveryAttempts = 30 });
        delivery.AttemptCount = 15;

        delivery.HasReachedMaxAttempts.ShouldBeFalse();
    }

    [Fact]
    public void GivenAttemptCountAtMax_WhenChecking_ThenHasReachedMax()
    {
        var delivery = CreatePendingDelivery(new RetryPolicySettings { MaxDeliveryAttempts = 30 });
        delivery.AttemptCount = 30;

        delivery.HasReachedMaxAttempts.ShouldBeTrue();
    }

    [Fact]
    public void GivenAttemptCountAboveMax_WhenChecking_ThenHasReachedMax()
    {
        var delivery = CreatePendingDelivery(new RetryPolicySettings { MaxDeliveryAttempts = 30 });
        delivery.AttemptCount = 50;

        delivery.HasReachedMaxAttempts.ShouldBeTrue();
    }

    [Fact]
    public void GivenCustomMaxAttempts_WhenChecking_ThenUsesCustomValue()
    {
        var delivery = CreatePendingDelivery(new RetryPolicySettings { MaxDeliveryAttempts = 5 });
        delivery.AttemptCount = 5;

        delivery.HasReachedMaxAttempts.ShouldBeTrue();
    }

    [Fact]
    public void GivenRetryPolicyEnabled_WhenChecking_ThenRetryEnabled()
    {
        var delivery = CreatePendingDelivery(new RetryPolicySettings { Enabled = true });

        delivery.RetryEnabled.ShouldBeTrue();
    }

    [Fact]
    public void GivenRetryPolicyDisabled_WhenChecking_ThenRetryDisabled()
    {
        var delivery = CreatePendingDelivery(new RetryPolicySettings { Enabled = false });

        delivery.RetryEnabled.ShouldBeFalse();
    }

    [Fact]
    public void GivenNoRetryPolicy_WhenChecking_ThenAzureDefaultsApply()
    {
        // Azure's defaults: retry enabled, 30 attempts, a 1440-minute (24-hour) TTL
        var delivery = CreatePendingDelivery(enqueuedTime: FixedTime);
        delivery.Subscriber.RetryPolicy.ShouldBeNull();

        delivery.RetryEnabled.ShouldBeTrue();

        delivery.AttemptCount = 29;
        delivery.HasReachedMaxAttempts.ShouldBeFalse();
        delivery.AttemptCount = 30;
        delivery.HasReachedMaxAttempts.ShouldBeTrue();

        delivery.IsExpired(FixedTime.AddMinutes(1440)).ShouldBeFalse();
        delivery.IsExpired(FixedTime.AddMinutes(1440).AddTicks(1)).ShouldBeTrue();
    }

    [Fact]
    public void GivenNoAttempts_WhenGettingLastAttempt_ThenReturnsNull()
    {
        var delivery = CreatePendingDelivery();

        delivery.LastAttempt.ShouldBeNull();
    }

    [Fact]
    public void GivenOneAttempt_WhenGettingLastAttempt_ThenReturnsThatAttempt()
    {
        var delivery = CreatePendingDelivery();
        var attempt = new DeliveryAttempt(1, DeliveryOutcome.HttpError, FixedTime, 500);
        delivery.Attempts.Add(attempt);

        delivery.LastAttempt.ShouldBe(attempt);
    }

    [Fact]
    public void GivenMultipleAttempts_WhenGettingLastAttempt_ThenReturnsLast()
    {
        var delivery = CreatePendingDelivery();
        var attempt1 = new DeliveryAttempt(
            1,
            DeliveryOutcome.HttpError,
            FixedTime.AddMinutes(-10),
            500
        );
        var attempt2 = new DeliveryAttempt(2, DeliveryOutcome.Timeout, FixedTime.AddMinutes(-5));
        var attempt3 = new DeliveryAttempt(3, DeliveryOutcome.HttpError, FixedTime, 503);
        delivery.Attempts.Add(attempt1);
        delivery.Attempts.Add(attempt2);
        delivery.Attempts.Add(attempt3);

        delivery.LastAttempt.ShouldBe(attempt3);
    }

    [Fact]
    public void GivenNewDelivery_WhenCreated_ThenHasUniqueId()
    {
        var delivery1 = CreatePendingDelivery();
        var delivery2 = CreatePendingDelivery();

        delivery1.Id.ShouldNotBeNullOrEmpty();
        delivery2.Id.ShouldNotBeNullOrEmpty();
        delivery1.Id.ShouldNotBe(delivery2.Id);
    }

    [Fact]
    public void GivenNewDelivery_WhenCreated_ThenHasCorrectDefaults()
    {
        var delivery = CreatePendingDelivery();

        delivery.AttemptCount.ShouldBe(0);
        delivery.Attempts.ShouldBeEmpty();
    }
}
