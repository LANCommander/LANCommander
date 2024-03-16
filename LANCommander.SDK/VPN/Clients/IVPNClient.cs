using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.VPN.Clients
{
    public interface IVPNClient
    {
        bool Connect();
        bool Disconnect();
        VPNConnectionStatus GetStatus();
    }
}
