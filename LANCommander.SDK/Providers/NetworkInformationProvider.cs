using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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