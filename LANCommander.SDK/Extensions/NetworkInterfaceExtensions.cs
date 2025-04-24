using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LANCommander.SDK.Extensions;

public static class NetworkInterfaceExtensions
{
    public static IEnumerable<IPAddress> GetBroadcastAddresses(this NetworkInterface networkInterface)
    {
        foreach (var ua in networkInterface.GetIPProperties().UnicastAddresses)
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