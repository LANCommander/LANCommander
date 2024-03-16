using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.VPN.Configurations
{
    public class VPNConfiguration
    {
        public VPNType Type { get; set; }
        public object Data { get; set; }
    }
}
