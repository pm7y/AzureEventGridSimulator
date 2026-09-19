using NSubstitute;

namespace AzureEventGridSimulator.Tests.UnitTests.Common;

/// <summary>
///     Assertions for <see cref="ILogger" /> substitutes created with NSubstitute.
/// </summary>
public static class LoggerAssertions
{
    /// <summary>
    ///     Asserts that the logger received at least one call at <paramref name="level" /> whose
    ///     formatted message contains <paramref name="contains" />.
    /// </summary>
    public static void ShouldHaveLogged(this ILogger logger, LogLevel level, string contains)
    {
        logger
            .Received()
            .Log(
                level,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => MessageContains(o, contains)),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    /// <summary>
    ///     Asserts that the logger received no call at <paramref name="level" /> whose formatted
    ///     message contains <paramref name="contains" />.
    /// </summary>
    public static void ShouldNotHaveLogged(this ILogger logger, LogLevel level, string contains)
    {
        logger
            .DidNotReceive()
            .Log(
                level,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => MessageContains(o, contains)),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()
            );
    }

    private static bool MessageContains(object? state, string contains)
    {
        return state != null && string.Concat(state).Contains(contains, StringComparison.Ordinal);
    }
}
