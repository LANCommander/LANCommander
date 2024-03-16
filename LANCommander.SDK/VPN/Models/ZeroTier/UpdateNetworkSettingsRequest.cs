using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.VPN.Models.ZeroTier
{
    public class UpdateNetworkSettingsRequest
    {
        public bool AllowDNS { get; set; }
        public bool AllowDefault { get; set; }
        public bool AllowManaged { get; set; }
        public bool AllowGlobal { get; set; }
    }
}
