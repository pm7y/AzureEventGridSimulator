using AzureEventGridSimulator.Domain.Services.Retry;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Retry;

[Trait("Category", "unit")]
public class RetrySchedulerTests
{
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
        var before = DateTime.UtcNow;

        var nextRetryTime = RetryScheduler.GetNextRetryTime(attemptNumber);

        var after = DateTime.UtcNow;
        var expectedMin = before.AddSeconds(expectedDelaySeconds);
        var expectedMax = after.AddSeconds(expectedDelaySeconds + 1); // Allow 1 second tolerance

        nextRetryTime.ShouldBeGreaterThanOrEqualTo(expectedMin);
        nextRetryTime.ShouldBeLessThanOrEqualTo(expectedMax);
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
        var before = DateTime.UtcNow;
        var expectedDelaySeconds = 12 * 60 * 60; // 12 hours

        var nextRetryTime = RetryScheduler.GetNextRetryTime(attemptNumber);

        var after = DateTime.UtcNow;
        var expectedMin = before.AddSeconds(expectedDelaySeconds);
        var expectedMax = after.AddSeconds(expectedDelaySeconds + 1);

        nextRetryTime.ShouldBeGreaterThanOrEqualTo(expectedMin);
        nextRetryTime.ShouldBeLessThanOrEqualTo(expectedMax);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GivenInvalidAttemptNumber_WhenGettingNextRetryTime_ThenReturnsImmediately(
        int attemptNumber
    )
    {
        var before = DateTime.UtcNow;

        var nextRetryTime = RetryScheduler.GetNextRetryTime(attemptNumber);

        nextRetryTime.ShouldBeLessThanOrEqualTo(before.AddSeconds(1));
    }

    [Fact]
    public void GivenHttp404_WhenGettingNextRetryTime_ThenMinimum5MinuteDelay()
    {
        var before = DateTime.UtcNow;
        var expectedMinDelaySeconds = 5 * 60; // 5 minutes

        // First attempt would normally be 10s, but 404 requires minimum 5 minutes
        var nextRetryTime = RetryScheduler.GetNextRetryTime(1, 404);

        nextRetryTime.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(expectedMinDelaySeconds));
    }

    [Fact]
    public void GivenHttp408_WhenGettingNextRetryTime_ThenMinimum2MinuteDelay()
    {
        var before = DateTime.UtcNow;
        var expectedMinDelaySeconds = 2 * 60; // 2 minutes

        // First attempt would normally be 10s, but 408 requires minimum 2 minutes
        var nextRetryTime = RetryScheduler.GetNextRetryTime(1, 408);

        nextRetryTime.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(expectedMinDelaySeconds));
    }

    [Fact]
    public void GivenHttp503_WhenGettingNextRetryTime_ThenMinimum30SecondDelay()
    {
        var before = DateTime.UtcNow;
        var expectedMinDelaySeconds = 30; // 30 seconds

        // First attempt would normally be 10s, but 503 requires minimum 30 seconds
        var nextRetryTime = RetryScheduler.GetNextRetryTime(1, 503);

        nextRetryTime.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(expectedMinDelaySeconds));
    }

    [Fact]
    public void GivenHttp503OnLaterAttempt_WhenStandardDelayIsLarger_ThenUsesStandardDelay()
    {
        var before = DateTime.UtcNow;
        var expectedMinDelaySeconds = 5 * 60; // 5 minutes (attempt 4 standard delay)

        // Attempt 4 standard delay is 5 minutes, which is larger than 503's 30 seconds
        var nextRetryTime = RetryScheduler.GetNextRetryTime(4, 503);

        nextRetryTime.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(expectedMinDelaySeconds));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(203)]
    [InlineData(204)]
    public void GivenSuccessStatusCode_WhenChecking_ThenReturnsTrue(int statusCode)
    {
        RetryScheduler.IsSuccessStatusCode(statusCode).ShouldBeTrue();
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
        RetryScheduler.IsSuccessStatusCode(statusCode).ShouldBeFalse();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(413)]
    public void GivenImmediateDeadLetterStatusCode_WhenChecking_ThenReturnsTrue(int statusCode)
    {
        RetryScheduler.ShouldImmediatelyDeadLetter(statusCode).ShouldBeTrue();
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
        RetryScheduler.ShouldImmediatelyDeadLetter(statusCode).ShouldBeFalse();
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
        RetryScheduler.GetDeadLetterReasonForStatusCode(statusCode).ShouldBe(expectedReason);
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
        RetryScheduler.GetDeadLetterReasonForStatusCode(statusCode).ShouldBe(expectedReason);
    }
}
