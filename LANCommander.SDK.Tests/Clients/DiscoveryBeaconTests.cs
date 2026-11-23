using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests.Clients;

/// <summary>
/// Launchers find servers by broadcasting a probe to each interface's subnet broadcast address. Linux and
/// macOS only deliver a broadcast to sockets bound to the wildcard or broadcast address, so a beacon bound
/// to the interface's unicast address (which is what works on Windows) never sees probes on a Linux or
/// Docker host.
/// </summary>
public class DiscoveryBeaconTests
{
    [Theory]
    [InlineData("192.168.178.20", "255.255.255.0", "192.168.178.255")]
    [InlineData("10.1.2.3", "255.0.0.0", "10.255.255.255")]
    [InlineData("172.17.0.2", "255.255.0.0", "172.17.255.255")]
    public void GetBroadcastAddress_ComputesSubnetBroadcast(string address, string mask, string expected)
    {
        Assert.Equal(
            IPAddress.Parse(expected),
            DiscoveryBeacon.GetBroadcastAddress(IPAddress.Parse(address), IPAddress.Parse(mask)));
    }

    [Fact]
    public void GetBindEndPoint_OnLinux_BindsToTheBroadcastAddress()
    {
        var endPoint = DiscoveryBeacon.GetBindEndPoint(
            IPAddress.Parse("192.168.178.20"), IPAddress.Parse("255.255.255.0"), 35891, isWindows: false);

        Assert.Equal(new IPEndPoint(IPAddress.Parse("192.168.178.255"), 35891), endPoint);
    }

    [Fact]
    public void GetBindEndPoint_OnWindows_BindsToTheUnicastAddress()
    {
        var endPoint = DiscoveryBeacon.GetBindEndPoint(
            IPAddress.Parse("192.168.178.20"), IPAddress.Parse("255.255.255.0"), 35891, isWindows: true);

        Assert.Equal(new IPEndPoint(IPAddress.Parse("192.168.178.20"), 35891), endPoint);
    }

    [Fact]
    public void GetBindEndPoint_WithoutMask_FallsBackToWildcard()
    {
        var endPoint = DiscoveryBeacon.GetBindEndPoint(IPAddress.Parse("192.168.178.20"), null, 35891, isWindows: false);

        Assert.Equal(new IPEndPoint(IPAddress.Any, 35891), endPoint);
    }

    /// <summary>
    /// Real sockets: a probe sent to the interface's broadcast address must reach the beacon on whatever
    /// OS the tests run on.
    /// </summary>
    [Fact]
    public async Task Beacon_ReceivesProbeSentToSubnetBroadcast()
    {
        var candidate = NetworkInterface.GetAllNetworkInterfaces()
            .Where(i => i.OperationalStatus == OperationalStatus.Up && i.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(i => (Interface: i, Address: i.GetIPProperties().UnicastAddresses
                .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork)))
            .FirstOrDefault(c => c.Address?.IPv4Mask != null && !c.Address.IPv4Mask.Equals(IPAddress.Any));

        if (candidate.Interface == null)
            return; // no IPv4 LAN interface to broadcast on

        var port = GetFreeUdpPort();
        var broadcastAddress = DiscoveryBeacon.GetBroadcastAddress(candidate.Address.Address, candidate.Address.IPv4Mask)!;
        var probed = new TaskCompletionSource<IPEndPoint>(TaskCreationOptions.RunContinuationsAsynchronously);

        var beacon = new DiscoveryBeacon(candidate.Interface);

        try
        {
            beacon.OnProbe += (_, probeEndPoint) => probed.TrySetResult(probeEndPoint);

            await beacon.StartAsync(port);

            using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.EnableBroadcast = true;
            sender.Bind(new IPEndPoint(candidate.Address.Address, 0));

            for (var attempt = 0; attempt < 5 && !probed.Task.IsCompleted; attempt++)
            {
                sender.SendTo("probe"u8.ToArray(), new IPEndPoint(broadcastAddress, port));
                await Task.WhenAny(probed.Task, Task.Delay(200));
            }

            Assert.True(probed.Task.IsCompleted, $"Beacon on {candidate.Interface.Name} did not receive a probe sent to {broadcastAddress}:{port}");
            Assert.Equal(candidate.Address.Address, beacon.InterfaceIPEndPoint.Address);
        }
        finally
        {
            beacon.Dispose();
        }
    }

    private static int GetFreeUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));

        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
