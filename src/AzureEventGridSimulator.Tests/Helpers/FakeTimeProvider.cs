namespace AzureEventGridSimulator.Tests.Helpers;

/// <summary>
/// A fake TimeProvider for testing time-dependent code.
/// Allows tests to control the current time for deterministic behavior.
/// </summary>
public class FakeTimeProvider(DateTimeOffset startTime) : TimeProvider
{
    private DateTimeOffset _utcNow = startTime;

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    /// <summary>
    /// Sets the current time to a specific value.
    /// </summary>
    public void SetUtcNow(DateTimeOffset value)
    {
        _utcNow = value;
    }

    /// <summary>
    /// Advances the current time by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }
}
