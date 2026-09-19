using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
///     Launches the compiled simulator as a child process and calls the dashboard on its own
///     dashboardPort. WebApplicationFactory bypasses Kestrel's Listen calls, so only a real
///     process shows which ports the simulator actually listens on. CI runners have no
///     development certificate, so each run generates a throwaway self-signed one.
/// </summary>
[Trait("Category", "integration")]
public sealed class SimulatorDashboardPortTests : IDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ListenTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly ConcurrentQueue<string> _output = new();

    private readonly string _workingDirectory = Directory
        .CreateTempSubdirectory("aegs-dashboard-port-")
        .FullName;

    private string Output => string.Join(Environment.NewLine, _output);

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
    public async Task GivenADashboardPort_WhenStarted_ThenTheDashboardIsServedOnThatPort()
    {
        var (topicPort, dashboardPort) = GetTwoFreePorts();
        var (certificateFile, certificatePassword, certificateHash) = CreateCertificate();
        var configFile = Path.Combine(_workingDirectory, "config.json");
        await File.WriteAllTextAsync(
            configFile,
            $$"""
            {
                "dashboardEnabled": true,
                "dashboardPort": {{dashboardPort}},
                "topics": [{
                    "name": "DashboardTopic",
                    "port": {{topicPort}},
                    "key": "TheLocal+DevelopmentKey="
                }]
            }
            """
        );

        using var handler = new HttpClientHandler
        {
            // Only trust the certificate generated for this run
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate?.GetCertHashString(HashAlgorithmName.SHA256) == certificateHash,
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

        using var process = StartSimulator(configFile, certificateFile, certificatePassword);
        try
        {
            // Once the topic port answers the simulator has started, so the dashboard port
            // only gets a short grace period
            var topicStatus = await GetStats(client, process, topicPort, StartupTimeout);
            var dashboardStatus = await GetStats(client, process, dashboardPort, ListenTimeout);

            dashboardStatus.ShouldBe(HttpStatusCode.OK, Output);
            topicStatus.ShouldBe(HttpStatusCode.OK, Output);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }

            using var timeout = new CancellationTokenSource(ExitTimeout);
            await process.WaitForExitAsync(timeout.Token);
        }
    }

    private static (int First, int Second) GetTwoFreePorts()
    {
        // Hold both listeners open together so that the OS hands out two different ports
        using var first = new TcpListener(IPAddress.Loopback, 0);
        using var second = new TcpListener(IPAddress.Loopback, 0);
        first.Start();
        second.Start();

        return (((IPEndPoint)first.LocalEndpoint).Port, ((IPEndPoint)second.LocalEndpoint).Port);
    }

    private (string File, string Password, string Hash) CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        var file = Path.Combine(_workingDirectory, "simulator.pfx");
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        File.WriteAllBytes(file, certificate.Export(X509ContentType.Pfx, password));

        return (file, password, certificate.GetCertHashString(HashAlgorithmName.SHA256));
    }

    private Process StartSimulator(
        string configFile,
        string certificateFile,
        string certificatePassword
    )
    {
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
        startInfo.ArgumentList.Add($"--Kestrel:Certificates:Default:Path={certificateFile}");
        startInfo.ArgumentList.Add(
            $"--Kestrel:Certificates:Default:Password={certificatePassword}"
        );
        SimulatorProcessEnvironment.RemoveInheritedSimulatorConfiguration(startInfo);

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += CaptureOutput;
        process.ErrorDataReceived += CaptureOutput;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private void CaptureOutput(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            _output.Enqueue(e.Data);
        }
    }

    /// <summary>
    ///     Polls GET /dashboard/api/stats on the port until something answers, and returns the
    ///     status code.
    /// </summary>
    private async Task<HttpStatusCode> GetStats(
        HttpClient client,
        Process process,
        int port,
        TimeSpan timeout
    )
    {
        var uri = new Uri($"https://127.0.0.1:{port}/dashboard/api/stats");
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The simulator exited with code {process.ExitCode} before {uri} answered.{Environment.NewLine}{Output}"
                );
            }

            try
            {
                using var response = await client.GetAsync(uri);
                return response.StatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (stopwatch.Elapsed >= timeout)
                {
                    throw new TimeoutException(
                        $"Nothing answered on {uri} within {timeout.TotalSeconds}s: {ex.Message}{Environment.NewLine}{Output}",
                        ex
                    );
                }

                await Task.Delay(PollInterval);
            }
        }
    }
}
