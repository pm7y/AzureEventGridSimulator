using System.Diagnostics;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

public class ActualSimulatorFixture : IDisposable, IAsyncLifetime
{
    private const string SimulatorFileName = "AzureEventGridSimulator";
    private const int MaxStartupWaitTimeMs = 30000;
    private const int PollingIntervalMs = 100;
    private bool _disposed;
    private string _simulatorExePath;

    private Process _simulatorProcess;

    public async Task InitializeAsync()
    {
        var simulatorDirectory = Directory.GetCurrentDirectory();
        var executable = Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location);
        _simulatorExePath = Path.Combine(simulatorDirectory, executable);

        KillExistingSimulators();

        _simulatorProcess = Process.Start(
            new ProcessStartInfo(_simulatorExePath)
            {
                WorkingDirectory = simulatorDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                Environment =
                {
                    new KeyValuePair<string, string>("ASPNETCORE_ENVIRONMENT", "Test"),
                },
            }
        );

        await WaitForSimulatorToBeReady();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_simulatorProcess?.HasExited == false)
            {
                _simulatorProcess.Kill(true);
                _simulatorProcess.WaitForExit();
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private static async Task WaitForSimulatorToBeReady()
    {
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        using var httpClient = new HttpClient(handler);
        httpClient.Timeout = TimeSpan.FromSeconds(2);

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < MaxStartupWaitTimeMs)
        {
            try
            {
                // Try to connect to the simulator's endpoint
                _ = await httpClient.GetAsync(
                    "https://localhost:60101/api/events?api-version=2018-01-01"
                );
                // Any response (even 4xx) means the server is up
                return;
            }
            catch (HttpRequestException)
            {
                // Server not ready yet, wait and retry
                await Task.Delay(PollingIntervalMs);
            }
            catch (TaskCanceledException)
            {
                // Timeout, wait and retry
                await Task.Delay(PollingIntervalMs);
            }
        }

        throw new InvalidOperationException(
            $"Simulator did not start within {MaxStartupWaitTimeMs}ms"
        );
    }

    private void KillExistingSimulators()
    {
        try
        {
            // Kill any existing instances of the test simulator that may still be hanging around.
            // Note: there shouldn't be any unless something went wrong and the test runner didn't exit cleanly.
            var simulatorProcesses = Process
                .GetProcesses()
                .Where(o => o.ProcessName == SimulatorFileName)
                .Where(o =>
                    string.Equals(
                        o.MainModule?.FileName,
                        _simulatorExePath,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .ToArray();

            foreach (var process in simulatorProcesses)
            {
                process.Kill();
            }
        }
        catch
        {
            //
        }
    }

    ~ActualSimulatorFixture()
    {
        Dispose();
    }
}
