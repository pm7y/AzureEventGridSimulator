using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using AzureEventGridSimulator.Infrastructure;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Infrastructure;

[Trait("Category", "unit")]
public class ValidationIpAddressProviderTests
{
    [Fact]
    public void GivenThisMachine_WhenCreate_ThenReturnsAnIpv4AddressAssignedToThisMachineOrLoopback()
    {
        var result = IPAddress.Parse(new ValidationIpAddressProvider().Create());

        var assignedAddresses = NetworkInterface
            .GetAllNetworkInterfaces()
            .SelectMany(o => o.GetIPProperties().UnicastAddresses)
            .Select(o => o.Address)
            .Append(IPAddress.Loopback)
            .ToList();

        result.AddressFamily.ShouldBe(AddressFamily.InterNetwork);
        assignedAddresses.ShouldContain(result);
    }

    [Fact]
    public void GivenInterfaceWithRouterAsGatewayAndDnsServer_WhenCreate_ThenReturnsTheMachinesOwnAddress()
    {
        // On a typical LAN the router (192.168.1.1) is both the gateway and the DNS server
        var provider = CreateProvider(
            Interface(unicast: ["192.168.1.50"], gateways: ["192.168.1.1"])
        );

        provider.Create().ShouldBe("192.168.1.50");
    }

    [Fact]
    public void GivenInterfaceWithNoAddressOfItsOwn_WhenCreate_ThenRouterAddressIsNeverChosen()
    {
        var provider = CreateProvider(Interface(unicast: [], gateways: ["192.168.1.1"]));

        provider.Create().ShouldBe("127.0.0.1");
    }

    [Theory]
    [InlineData("172.16.0.1")]
    [InlineData("172.20.0.5")]
    [InlineData("172.31.255.254")]
    [InlineData("169.254.10.20")]
    [InlineData("127.0.0.2")]
    [InlineData("2001:db8::5")]
    [InlineData("fe80::1")]
    public void GivenOnlyAnIneligibleAddress_WhenCreate_ThenFallsBackToLoopback(string address)
    {
        var provider = CreateProvider(Interface(unicast: [address]));

        provider.Create().ShouldBe("127.0.0.1");
    }

    [Theory]
    [InlineData("172.15.255.254")]
    [InlineData("172.32.0.1")]
    [InlineData("172.217.1.1")]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.1.50")]
    public void GivenAnEligibleAddress_WhenCreate_ThenReturnsIt(string address)
    {
        var provider = CreateProvider(Interface(unicast: [address]));

        provider.Create().ShouldBe(address);
    }

    [Fact]
    public void GivenIneligibleAddressBeforeEligibleOne_WhenCreate_ThenReturnsTheEligibleOne()
    {
        var provider = CreateProvider(
            Interface(unicast: ["172.17.0.1"]),
            Interface(unicast: ["fe80::1", "169.254.1.1", "10.0.0.5"])
        );

        provider.Create().ShouldBe("10.0.0.5");
    }

    [Fact]
    public void GivenInterfaceIsDown_WhenCreate_ThenItsAddressesAreIgnored()
    {
        var provider = CreateProvider(
            Interface(
                unicast: ["192.168.1.50"],
                gateways: ["192.168.1.1"],
                status: OperationalStatus.Down
            ),
            Interface(unicast: ["10.0.0.5"])
        );

        provider.Create().ShouldBe("10.0.0.5");
    }

    [Fact]
    public void GivenOnlyInterfaceIsDown_WhenCreate_ThenFallsBackToLoopback()
    {
        var provider = CreateProvider(
            Interface(unicast: ["192.168.1.50"], status: OperationalStatus.Down)
        );

        provider.Create().ShouldBe("127.0.0.1");
    }

    [Fact]
    public void GivenLoopbackInterface_WhenCreate_ThenItsAddressesAreIgnored()
    {
        var provider = CreateProvider(
            Interface(unicast: ["10.0.0.5"], type: NetworkInterfaceType.Loopback),
            Interface(unicast: ["192.168.1.50"])
        );

        provider.Create().ShouldBe("192.168.1.50");
    }

    [Fact]
    public void GivenInterfaceWithIpv4GatewayAfterOneWithout_WhenCreate_ThenGatewayInterfaceIsPreferred()
    {
        var provider = CreateProvider(
            Interface(unicast: ["10.0.0.5"]),
            Interface(unicast: ["192.168.1.50"], gateways: ["192.168.1.1"])
        );

        provider.Create().ShouldBe("192.168.1.50");
    }

    [Fact]
    public void GivenInterfaceWithOnlyAnIpv6Gateway_WhenCreate_ThenItIsNotPreferredOverAnIpv4GatewayInterface()
    {
        // e.g. macOS utun VPN interfaces report an IPv6 link-local gateway
        var provider = CreateProvider(
            Interface(unicast: ["10.0.0.5"], gateways: ["fe80::1"]),
            Interface(unicast: ["192.168.1.50"], gateways: ["192.168.1.1"])
        );

        provider.Create().ShouldBe("192.168.1.50");
    }

    [Fact]
    public void GivenNoInterfaceHasAGateway_WhenCreate_ThenFirstEligibleAddressIsReturned()
    {
        var provider = CreateProvider(
            Interface(unicast: ["10.0.0.5"]),
            Interface(unicast: ["192.168.1.50"])
        );

        provider.Create().ShouldBe("10.0.0.5");
    }

    [Fact]
    public void GivenNoInterfaces_WhenCreate_ThenFallsBackToLoopback()
    {
        var provider = CreateProvider();

        provider.Create().ShouldBe("127.0.0.1");
    }

    [Fact]
    public void GivenProvider_WhenConvertedToStringRepeatedly_ThenInterfacesAreEnumeratedOnce()
    {
        var enumerations = 0;
        var provider = new ValidationIpAddressProvider(() =>
        {
            enumerations++;
            return [Interface(unicast: ["192.168.1.50"])];
        });

        provider.ToString().ShouldBe("192.168.1.50");
        ((string)provider).ShouldBe("192.168.1.50");
        $"{provider}".ShouldBe("192.168.1.50");

        enumerations.ShouldBe(1);
    }

    [Fact]
    public void GivenTwoProviders_WhenConvertedToString_ThenEachUsesItsOwnInterfaces()
    {
        var first = CreateProvider(Interface(unicast: ["10.0.0.5"]));
        var second = CreateProvider(Interface(unicast: ["192.168.1.50"]));

        first.ToString().ShouldBe("10.0.0.5");
        second.ToString().ShouldBe("192.168.1.50");
    }

    private static ValidationIpAddressProvider CreateProvider(
        params ValidationIpAddressProvider.NetworkInterfaceInfo[] interfaces
    )
    {
        return new ValidationIpAddressProvider(() => interfaces);
    }

    private static ValidationIpAddressProvider.NetworkInterfaceInfo Interface(
        string[] unicast,
        string[]? gateways = null,
        OperationalStatus status = OperationalStatus.Up,
        NetworkInterfaceType type = NetworkInterfaceType.Ethernet
    )
    {
        return new ValidationIpAddressProvider.NetworkInterfaceInfo(
            status,
            type,
            [.. unicast.Select(IPAddress.Parse)],
            [.. (gateways ?? []).Select(IPAddress.Parse)]
        );
    }
}
