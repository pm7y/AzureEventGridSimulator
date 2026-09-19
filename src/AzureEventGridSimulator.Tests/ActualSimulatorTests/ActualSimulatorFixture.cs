using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
///     Starts the compiled simulator (the apphost in the test output directory) as a child process
///     for the integration-actual tests. It listens on the topic ports in appsettings.test.json and
///     serves HTTPS with the ASP.NET Core development certificate, so a machine without one needs
///     <c>dotnet dev-certs https</c> first.
/// </summary>
public class ActualSimulatorFixture : IDisposable, IAsyncLifetime
{
    private const string SimulatorFileName = "AzureEventGridSimulator";
    private const int MaxStartupWaitTimeMs = 30000;
    private const int PollingIntervalMs = 100;
    private readonly ConcurrentQueue<string> _output = new();
    private bool _disposed;
    private string? _simulatorExePath;
    private Process? _simulatorProcess;

    public async Task InitializeAsync()
    {
        var simulatorDirectory = Directory.GetCurrentDirectory();
        var executable = Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location);
        _simulatorExePath = Path.Combine(simulatorDirectory, executable);

        KillExistingSimulators();

        var startInfo = new ProcessStartInfo(_simulatorExePath)
        {
            WorkingDirectory = simulatorDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        SimulatorProcessEnvironment.RemoveInheritedSimulatorConfiguration(startInfo);
        // The simulator loads appsettings.{environment}.json, and file names are case sensitive
        // on Linux, so this has to match appsettings.test.json exactly. Otherwise only the copied
        // appsettings.json is loaded, and its subscribers deliver to requestcatcher.com.
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "test";

        var process = new Process { StartInfo = startInfo };
        // Keep reading both pipes: once a pipe that nobody reads fills up, the simulator's
        // console logging blocks
        process.OutputDataReceived += CaptureOutput;
        process.ErrorDataReceived += CaptureOutput;
        try
        {
            process.Start();
        }
        catch
        {
            // e.g. the apphost is missing. Dispose reads HasExited, which throws for a process
            // that never started, so only a started process goes in the field.
            process.Dispose();
            throw;
        }

        _simulatorProcess = process;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await WaitForSimulatorToBeReady(process);
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

    private string Output => string.Join(Environment.NewLine, _output);

    private void CaptureOutput(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            _output.Enqueue(e.Data);
        }
    }

    private async Task WaitForSimulatorToBeReady(Process simulatorProcess)
    {
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        using var httpClient = new HttpClient(handler);
        httpClient.Timeout = TimeSpan.FromSeconds(2);

        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < MaxStartupWaitTimeMs)
        {
            // e.g. Kestrel couldn't bind HTTPS because there's no development certificate
            if (simulatorProcess.HasExited)
            {
                // Also waits for the rest of the redirected output
                await simulatorProcess.WaitForExitAsync();
                throw new InvalidOperationException(
                    $"The simulator exited with code {simulatorProcess.ExitCode} before it was ready.{Environment.NewLine}{Output}"
                );
            }

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
            $"Simulator did not start within {MaxStartupWaitTimeMs}ms.{Environment.NewLine}{Output}"
        );
    }

    private void KillExistingSimulators()
    {
        if (_simulatorExePath == null)
        {
            return;
        }

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
