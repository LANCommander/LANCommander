using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;

namespace LANCommander.SDK.Providers;

public class NetworkInformationProvider : INetworkInformationProvider
{
    public string GetMacAddress()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic => nic.GetPhysicalAddress().ToString())
            .FirstOrDefault();
    }

    public string GetComputerName()
    {
        return Dns.GetHostName();
    }

    public string GetIpAddress()
    {
        return Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
    }

    public async Task<string> GetMacAddressAsync(Uri destination, CancellationToken cancellationToken = default)
    {
        var route = await GetRouteAsync(destination, cancellationToken);

        return route?.MacAddress ?? GetMacAddress();
    }

    public async Task<string> GetIpAddressAsync(Uri destination, CancellationToken cancellationToken = default)
    {
        var route = await GetRouteAsync(destination, cancellationToken);

        return route?.LocalAddress.ToString() ?? GetIpAddress();
    }

    /// <summary>
    /// The local address and interface MAC the OS would use to send traffic to <paramref name="destination"/>.
    /// Connecting a UDP socket only asks the OS to pick a route and source address; no packets are sent.
    /// Returns null when the destination cannot be resolved or routed, or when the route goes through an
    /// interface without a hardware address (loopback, some tunnels).
    /// </summary>
    private async Task<NetworkRoute> GetRouteAsync(Uri destination, CancellationToken cancellationToken)
    {
        if (destination == null || String.IsNullOrWhiteSpace(destination.Host))
            return null;

        IPAddress[] addresses;

        try
        {
            addresses = await Dns.GetHostAddressesAsync(destination.DnsSafeHost, cancellationToken);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return null;
        }

        var port = destination.IsDefaultPort || destination.Port <= 0 ? 80 : destination.Port;

        foreach (var address in addresses.OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1))
        {
            var localAddress = GetLocalAddressFor(address, port);

            if (localAddress == null)
                continue;

            var macAddress = FindMacAddress(GetNetworkInterfaces(), localAddress);

            if (!String.IsNullOrEmpty(macAddress))
                return new NetworkRoute(localAddress, macAddress);
        }

        return null;
    }

    private static IPAddress GetLocalAddressFor(IPAddress remote, int port)
    {
        try
        {
            using var socket = new Socket(remote.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            socket.Connect(new IPEndPoint(remote, port));

            return (socket.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    /// <summary>
    /// The MAC address of the interface that owns <paramref name="localAddress"/>, or null when no interface
    /// with a hardware address owns it.
    /// </summary>
    internal static string FindMacAddress(IEnumerable<NetworkInterface> interfaces, IPAddress localAddress)
        => FindMacAddress(
            interfaces.Select(nic => (
                nic.GetIPProperties().UnicastAddresses.Select(ua => ua.Address),
                nic.GetPhysicalAddress().ToString())),
            localAddress);

    internal static string FindMacAddress(
        IEnumerable<(IEnumerable<IPAddress> Addresses, string MacAddress)> interfaces,
        IPAddress localAddress)
    {
        foreach (var (addresses, macAddress) in interfaces)
        {
            if (!String.IsNullOrEmpty(macAddress) && addresses.Any(address => AddressesMatch(address, localAddress)))
                return macAddress;
        }

        return null;
    }

    // IPAddress.Equals also compares IPv6 scope IDs, which the socket's local endpoint may not carry
    private static bool AddressesMatch(IPAddress a, IPAddress b)
    {
        if (a.IsIPv4MappedToIPv6)
            a = a.MapToIPv4();

        if (b.IsIPv4MappedToIPv6)
            b = b.MapToIPv4();

        return a.AddressFamily == b.AddressFamily && a.GetAddressBytes().AsSpan().SequenceEqual(b.GetAddressBytes());
    }

    private sealed record NetworkRoute(IPAddress LocalAddress, string MacAddress);

    /// <summary>
    /// Get active network interfaces on the system
    /// </summary>
    /// <returns></returns>
    public IEnumerable<NetworkInterface> GetNetworkInterfaces()
    {
        var networkInterfaces = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(i => i.OperationalStatus == OperationalStatus.Up &&
                        i.NetworkInterfaceType != NetworkInterfaceType.Loopback);

        return networkInterfaces;
    }
    
    public IEnumerable<IPAddress> GetBroadcastAddresses()
    {
        var networkInterfaces = GetNetworkInterfaces();
        
        foreach (var nic in networkInterfaces)
        {
            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    var ip = ua.Address;
                    var mask = ua.IPv4Mask;

                    if (mask == null)
                        continue;
                    
                    var ipBytes = ip.GetAddressBytes();
                    var maskBytes = mask.GetAddressBytes();
                    var broadcastBytes = new byte[4];
                    
                    for (var i = 0; i < 4; i++)
                        broadcastBytes[i] = (byte)(ipBytes[i] | (maskBytes[i] ^ 255));
                    
                    yield return new IPAddress(broadcastBytes);
                }
            }
        }
    }
}