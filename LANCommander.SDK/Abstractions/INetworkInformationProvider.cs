using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Abstractions;

public interface INetworkInformationProvider
{
    public string GetMacAddress();
    public string GetComputerName();
    public string GetIpAddress();
    public IEnumerable<NetworkInterface> GetNetworkInterfaces();
    public IEnumerable<IPAddress> GetBroadcastAddresses();

    /// <summary>
    /// The MAC address of the network interface this machine uses to reach <paramref name="destination"/>,
    /// falling back to <see cref="GetMacAddress()"/> when that interface cannot be determined.
    /// </summary>
    public Task<string> GetMacAddressAsync(Uri destination, CancellationToken cancellationToken = default)
        => Task.FromResult(GetMacAddress());

    /// <summary>
    /// The local IP address this machine uses to reach <paramref name="destination"/>,
    /// falling back to <see cref="GetIpAddress()"/> when it cannot be determined.
    /// </summary>
    public Task<string> GetIpAddressAsync(Uri destination, CancellationToken cancellationToken = default)
        => Task.FromResult(GetIpAddress());
}
