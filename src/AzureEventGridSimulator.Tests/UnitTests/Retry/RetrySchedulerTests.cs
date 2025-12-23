using AzureEventGridSimulator.Domain.Services.Retry;
using AzureEventGridSimulator.Tests.Helpers;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Retry;

[Trait("Category", "unit")]
public class RetrySchedulerTests
{
    private static readonly DateTimeOffset FixedTime = new(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly RetryScheduler _scheduler;
    private readonly FakeTimeProvider _timeProvider = new(FixedTime);

    public RetrySchedulerTests()
    {
        _scheduler = new RetryScheduler(_timeProvider);
    }

    [Theory]
    [InlineData(1, 10)] // 10 seconds
    [InlineData(2, 30)] // 30 seconds
    [InlineData(3, 60)] // 1 minute
    [InlineData(4, 300)] // 5 minutes
    [InlineData(5, 600)] // 10 minutes
    [InlineData(6, 1800)] // 30 minutes
    [InlineData(7, 3600)] // 1 hour
    [InlineData(8, 10800)] // 3 hours
    [InlineData(9, 21600)] // 6 hours
    [InlineData(10, 43200)] // 12 hours
    public void GivenAttemptNumber_WhenGettingNextRetryTime_ThenFollowsAzureSchedule(
        int attemptNumber,
        int expectedDelaySeconds
    )
    {
        var nextRetryTime = _scheduler.GetNextRetryTime(attemptNumber);

        var expectedTime = FixedTime.AddSeconds(expectedDelaySeconds);
        nextRetryTime.ShouldBe(expectedTime);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(100)]
    public void GivenAttemptBeyondSchedule_WhenGettingNextRetryTime_ThenReturns12Hours(
        int attemptNumber
    )
    {
        var expectedDelaySeconds = 12 * 60 * 60; // 12 hours

        var nextRetryTime = _scheduler.GetNextRetryTime(attemptNumber);

        var expectedTime = FixedTime.AddSeconds(expectedDelaySeconds);
        nextRetryTime.ShouldBe(expectedTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GivenInvalidAttemptNumber_WhenGettingNextRetryTime_ThenReturnsImmediately(
        int attemptNumber
    )
    {
        var nextRetryTime = _scheduler.GetNextRetryTime(attemptNumber);

        nextRetryTime.ShouldBe(FixedTime);
    }

    [Fact]
    public void GivenHttp404_WhenGettingNextRetryTime_ThenMinimum5MinuteDelay()
    {
        var expectedMinDelaySeconds = 5 * 60; // 5 minutes

        // First attempt would normally be 10s, but 404 requires minimum 5 minutes
        var nextRetryTime = _scheduler.GetNextRetryTime(1, 404);

        var expectedTime = FixedTime.AddSeconds(expectedMinDelaySeconds);
        nextRetryTime.ShouldBe(expectedTime);
    }

    [Fact]
    public void GivenHttp408_WhenGettingNextRetryTime_ThenMinimum2MinuteDelay()
    {
        var expectedMinDelaySeconds = 2 * 60; // 2 minutes

        // First attempt would normally be 10s, but 408 requires minimum 2 minutes
        var nextRetryTime = _scheduler.GetNextRetryTime(1, 408);

        var expectedTime = FixedTime.AddSeconds(expectedMinDelaySeconds);
        nextRetryTime.ShouldBe(expectedTime);
    }

    [Fact]
    public void GivenHttp503_WhenGettingNextRetryTime_ThenMinimum30SecondDelay()
    {
        var expectedMinDelaySeconds = 30; // 30 seconds

        // First attempt would normally be 10s, but 503 requires minimum 30 seconds
        var nextRetryTime = _scheduler.GetNextRetryTime(1, 503);

        var expectedTime = FixedTime.AddSeconds(expectedMinDelaySeconds);
        nextRetryTime.ShouldBe(expectedTime);
    }

    [Fact]
    public void GivenHttp503OnLaterAttempt_WhenStandardDelayIsLarger_ThenUsesStandardDelay()
    {
        var expectedMinDelaySeconds = 5 * 60; // 5 minutes (attempt 4 standard delay)

        // Attempt 4 standard delay is 5 minutes, which is larger than 503's 30 seconds
        var nextRetryTime = _scheduler.GetNextRetryTime(4, 503);

        var expectedTime = FixedTime.AddSeconds(expectedMinDelaySeconds);
        nextRetryTime.ShouldBe(expectedTime);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(203)]
    [InlineData(204)]
    public void GivenSuccessStatusCode_WhenChecking_ThenReturnsTrue(int statusCode)
    {
        _scheduler.IsSuccessStatusCode(statusCode).ShouldBeTrue();
    }

    [Theory]
    [InlineData(199)]
    [InlineData(205)]
    [InlineData(301)]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(503)]
    public void GivenNonSuccessStatusCode_WhenChecking_ThenReturnsFalse(int statusCode)
    {
        _scheduler.IsSuccessStatusCode(statusCode).ShouldBeFalse();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(413)]
    public void GivenImmediateDeadLetterStatusCode_WhenChecking_ThenReturnsTrue(int statusCode)
    {
        _scheduler.ShouldImmediatelyDeadLetter(statusCode).ShouldBeTrue();
    }

    [Theory]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(408)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void GivenRetryableStatusCode_WhenChecking_ThenReturnsFalse(int statusCode)
    {
        _scheduler.ShouldImmediatelyDeadLetter(statusCode).ShouldBeFalse();
    }

    [Theory]
    [InlineData(400, "BadRequest")]
    [InlineData(401, "Unauthorized")]
    [InlineData(403, "Forbidden")]
    [InlineData(413, "PayloadTooLarge")]
    public void GivenKnownStatusCode_WhenGettingDeadLetterReason_ThenReturnsSpecificReason(
        int statusCode,
        string expectedReason
    )
    {
        _scheduler.GetDeadLetterReasonForStatusCode(statusCode).ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(404, "HttpStatus404")]
    [InlineData(500, "HttpStatus500")]
    [InlineData(503, "HttpStatus503")]
    public void GivenUnknownStatusCode_WhenGettingDeadLetterReason_ThenReturnsGenericReason(
        int statusCode,
        string expectedReason
    )
    {
        _scheduler.GetDeadLetterReasonForStatusCode(statusCode).ShouldBe(expectedReason);
    }
}
