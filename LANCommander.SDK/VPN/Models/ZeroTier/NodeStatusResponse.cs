using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.VPN.Models.ZeroTier
{
    public class NodeStatusResponse
    {
        public string Address { get; set; }
        public bool Online { get; set; }
        public bool TcpFallbackActive { get; set; }
    }
}
