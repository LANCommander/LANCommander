using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LANCommander.SDK.Providers;

namespace LANCommander.SDK.Tests.Providers;

/// <summary>
/// Key allocation by MAC address identifies a machine by the MAC it reports. It must be the MAC of the
/// interface the machine reaches the server through, not whichever adapter the OS happens to list
/// first (VPN and virtual adapters such as ZeroTier or Hyper-V switches are often listed first).
/// </summary>
public class NetworkInformationProviderTests
{
    private static readonly IPAddress LanAddress = IPAddress.Parse("192.168.1.50");

    [Fact]
    public void FindMacAddress_ReturnsTheInterfaceOwningTheLocalAddress_NotTheFirstListed()
    {
        var interfaces = new (IEnumerable<IPAddress>, string)[]
        {
            ([IPAddress.Parse("10.147.17.5")], "22CD1AEEA72E"), // overlay adapter listed first
            ([IPAddress.Parse("172.20.0.1")], "00155D32BBC6"),
            ([LanAddress], "1A4B24C82AF3"),
        };

        Assert.Equal("1A4B24C82AF3", NetworkInformationProvider.FindMacAddress(interfaces, LanAddress));
    }

    [Fact]
    public void FindMacAddress_SkipsInterfacesWithoutAHardwareAddress()
    {
        var interfaces = new (IEnumerable<IPAddress>, string)[]
        {
            ([LanAddress], ""),
            ([LanAddress], "1A4B24C82AF3"),
        };

        Assert.Equal("1A4B24C82AF3", NetworkInformationProvider.FindMacAddress(interfaces, LanAddress));
    }

    [Fact]
    public void FindMacAddress_ReturnsNullWhenNoInterfaceOwnsTheAddress()
    {
        var interfaces = new (IEnumerable<IPAddress>, string)[]
        {
            ([IPAddress.Parse("10.147.17.5")], "22CD1AEEA72E"),
        };

        Assert.Null(NetworkInformationProvider.FindMacAddress(interfaces, LanAddress));
    }

    [Fact]
    public void FindMacAddress_MatchesIPv4MappedAddresses()
    {
        var interfaces = new (IEnumerable<IPAddress>, string)[]
        {
            ([LanAddress], "1A4B24C82AF3"),
        };

        Assert.Equal("1A4B24C82AF3", NetworkInformationProvider.FindMacAddress(interfaces, LanAddress.MapToIPv6()));
    }

    [Fact]
    public void FindMacAddress_IgnoresIPv6ScopeIds()
    {
        var owned = IPAddress.Parse("fe80::1c2d:3e4f:5a6b:7c8d%12");
        var local = IPAddress.Parse("fe80::1c2d:3e4f:5a6b:7c8d");

        var interfaces = new (IEnumerable<IPAddress>, string)[]
        {
            ([owned], "1A4B24C82AF3"),
        };

        Assert.Equal("1A4B24C82AF3", NetworkInformationProvider.FindMacAddress(interfaces, local));
    }

    [Fact]
    public async Task GetMacAddressAsync_FallsBackWhenTheRouteIsLoopback()
    {
        var provider = new NetworkInformationProvider();

        Assert.Equal(provider.GetMacAddress(), await provider.GetMacAddressAsync(new Uri("http://127.0.0.1:1337")));
    }

    [Fact]
    public async Task GetMacAddressAsync_FallsBackWithoutADestination()
    {
        var provider = new NetworkInformationProvider();

        Assert.Equal(provider.GetMacAddress(), await provider.GetMacAddressAsync(null!));
    }

    [Fact]
    public async Task GetMacAddressAsync_ReturnsTheInterfaceTheOsRoutesThrough()
    {
        // TEST-NET-1: never answered, but routable through the default route if the machine has one
        var destination = IPAddress.Parse("192.0.2.1");

        IPAddress localAddress;

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(destination, 80));
            localAddress = ((IPEndPoint)socket.LocalEndPoint!).Address;
        }
        catch (SocketException)
        {
            return; // no IPv4 route on this machine; nothing to compare against
        }

        var owner = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(nic => nic.GetIPProperties().UnicastAddresses.Any(ua => ua.Address.Equals(localAddress)));

        var expected = owner?.GetPhysicalAddress().ToString();

        if (String.IsNullOrEmpty(expected))
            return; // routed through an interface without a hardware address

        var provider = new NetworkInformationProvider();

        Assert.Equal(expected, await provider.GetMacAddressAsync(new Uri($"http://{destination}:1337")));
        Assert.Equal(localAddress.ToString(), await provider.GetIpAddressAsync(new Uri($"http://{destination}:1337")));
    }
}
