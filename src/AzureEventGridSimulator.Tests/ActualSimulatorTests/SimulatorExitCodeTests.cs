using System.Diagnostics;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
///     Launches the compiled simulator as a child process and checks its exit code, so that
///     scripts, CI and container restart policies can tell a failed start from a clean exit.
///     Each run gets its own empty working directory, so the simulator's appsettings.json in the
///     test output directory isn't loaded, and Kestrel's fallback address is a dynamic loopback
///     port, so no fixed ports or certificates are needed.
/// </summary>
[Trait("Category", "integration")]
public sealed class SimulatorExitCodeTests : IDisposable
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);

    private readonly string _workingDirectory = Directory
        .CreateTempSubdirectory("aegs-exit-code-")
        .FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_workingDirectory, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort clean up
        }
    }

    [Fact]
    public async Task GivenAnInvalidConfiguration_WhenStarted_ThenExitsWithFailureCode()
    {
        var (exitCode, output) = await RunSimulator(
            """
            {
                "topics": [{
                    "name": "Not A Valid Topic Name!",
                    "port": 61202,
                    "key": "TheLocal+DevelopmentKey="
                }]
            }
            """
        );

        output.ShouldContain("A topic name can only contain letters, numbers, and dashes.");
        exitCode.ShouldBe(1, output);
    }

    [Fact]
    public async Task GivenAllTopicsAreDisabled_WhenStarted_ThenExitsWithFailureCode()
    {
        var (exitCode, output) = await RunSimulator(
            """
            {
                "dashboardEnabled": false,
                "topics": [{
                    "name": "DisabledTopic",
                    "port": 61201,
                    "key": "TheLocal+DevelopmentKey=",
                    "disabled": true
                }]
            }
            """
        );

        output.ShouldContain("All of the configured topics are disabled");
        exitCode.ShouldBe(1, output);
    }

    [Fact]
    public async Task GivenNoTopics_WhenStarted_ThenShowsHelpAndExitsWithSuccessCode()
    {
        var (exitCode, output) = await RunSimulator("""{ "dashboardEnabled": false }""");

        output.ShouldContain("No topics configured");
        exitCode.ShouldBe(0, output);
    }

    private async Task<(int ExitCode, string Output)> RunSimulator(string configJson)
    {
        var configFile = Path.Combine(_workingDirectory, "config.json");
        await File.WriteAllTextAsync(configFile, configJson);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        startInfo.ArgumentList.Add($"--ConfigFile={configFile}");
        SimulatorProcessEnvironment.RemoveInheritedSimulatorConfiguration(startInfo);
        // With no enabled topic Kestrel falls back to its default address (localhost:5000),
        // so bind a dynamic loopback port instead.
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the simulator process.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(ExitTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(true);
            await process.WaitForExitAsync();
            throw new TimeoutException(
                $"The simulator did not exit within {ExitTimeout.TotalSeconds}s.{Environment.NewLine}{await standardOutput}{await standardError}"
            );
        }

        return (process.ExitCode, await standardOutput + await standardError);
    }
}
