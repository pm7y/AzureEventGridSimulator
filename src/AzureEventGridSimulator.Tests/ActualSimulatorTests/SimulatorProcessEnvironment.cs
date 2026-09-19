using System.Diagnostics;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
///     Keeps a child simulator process hermetic. The simulator reads AEGS_ and ASPNETCORE_
///     environment variables after its --ConfigFile, so any the test host inherited from a
///     developer's shell (e.g. AEGS_dashboardEnabled or AEGS_topics__0__port) would silently
///     override the test's own configuration.
/// </summary>
internal static class SimulatorProcessEnvironment
{
    public static void RemoveInheritedSimulatorConfiguration(ProcessStartInfo startInfo)
    {
        var inheritedKeys = startInfo
            .Environment.Keys.Where(key =>
                key.StartsWith("AEGS_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("ASPNETCORE_", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        foreach (var key in inheritedKeys)
        {
            startInfo.Environment.Remove(key);
        }
    }
}
