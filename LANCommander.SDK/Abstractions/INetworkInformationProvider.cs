using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

namespace LANCommander.SDK.Abstractions;

public interface INetworkInformationProvider
{
    public string GetMacAddress();
    public string GetComputerName();
    public string GetIpAddress();
    public IEnumerable<NetworkInterface> GetNetworkInterfaces();
    public IEnumerable<IPAddress> GetBroadcastAddresses();
}