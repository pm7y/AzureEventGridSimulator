using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AzureEventGridSimulator.Infrastructure;

public class ValidationIpAddressProvider
{
    private static string? _ipAddress;

    // MA0158: Lock type requires .NET 9+, but this project multi-targets net8.0+
    private static readonly object _lock = new();

    public string Create()
    {
        return (
            NetworkInterface
                .GetAllNetworkInterfaces()
                .SelectMany(o => o.GetIPProperties().DnsAddresses)
                .FirstOrDefault(ip =>
                    ip.AddressFamily == AddressFamily.InterNetwork
                    && !ip.ToString().StartsWith("172", StringComparison.Ordinal)
                    && !IPAddress.IsLoopback(ip)
                )
            ?? IPAddress.Loopback
        ).ToString();
    }

    public override string ToString()
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(_ipAddress))
            {
                _ipAddress = Create();
            }
        }

        return _ipAddress;
    }

    public static implicit operator string(ValidationIpAddressProvider d)
    {
        return d.ToString();
    }
}
