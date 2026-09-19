using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AzureEventGridSimulator.Infrastructure;

public class ValidationIpAddressProvider
{
    private readonly Func<IEnumerable<NetworkInterfaceInfo>> _getNetworkInterfaces;
    private readonly Lock _lock = new();
    private string? _ipAddress;

    public ValidationIpAddressProvider()
        : this(GetNetworkInterfaces) { }

    internal ValidationIpAddressProvider(
        Func<IEnumerable<NetworkInterfaceInfo>> getNetworkInterfaces
    )
    {
        _getNetworkInterfaces = getNetworkInterfaces;
    }

    public string Create()
    {
        // Use this machine's own (unicast) addresses on interfaces that are up and not loopback
        var interfaces = _getNetworkInterfaces()
            .Where(o => o.Status == OperationalStatus.Up && o.Type != NetworkInterfaceType.Loopback)
            .ToList();

        // Prefer an interface with an IPv4 gateway (the one that reaches the rest of the network)
        var address =
            FirstCandidateAddress(
                interfaces.Where(o =>
                    o.GatewayAddresses.Any(g => g.AddressFamily == AddressFamily.InterNetwork)
                )
            )
            ?? FirstCandidateAddress(interfaces)
            ?? IPAddress.Loopback;

        return address.ToString();
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return _ipAddress ??= Create();
        }
    }

    private static IPAddress? FirstCandidateAddress(IEnumerable<NetworkInterfaceInfo> interfaces)
    {
        return interfaces.SelectMany(o => o.UnicastAddresses).FirstOrDefault(IsCandidateAddress);
    }

    private static bool IsCandidateAddress(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip))
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();

        // 172.16.0.0/12 (private range used by Docker bridge networks)
        var isPrivate172 = bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;

        // 169.254.0.0/16 (link-local, self-assigned when DHCP fails)
        var isLinkLocal = bytes[0] == 169 && bytes[1] == 254;

        return !isPrivate172 && !isLinkLocal;
    }

    private static IEnumerable<NetworkInterfaceInfo> GetNetworkInterfaces()
    {
        return NetworkInterface
            .GetAllNetworkInterfaces()
            .Select(o =>
            {
                var properties = o.GetIPProperties();

                return new NetworkInterfaceInfo(
                    o.OperationalStatus,
                    o.NetworkInterfaceType,
                    [.. properties.UnicastAddresses.Select(a => a.Address)],
                    [.. properties.GatewayAddresses.Select(a => a.Address)]
                );
            });
    }

    public static implicit operator string(ValidationIpAddressProvider d)
    {
        return d.ToString();
    }

    internal sealed record NetworkInterfaceInfo(
        OperationalStatus Status,
        NetworkInterfaceType Type,
        IReadOnlyList<IPAddress> UnicastAddresses,
        IReadOnlyList<IPAddress> GatewayAddresses
    );
}
