using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.VPN.Exceptions.ZeroTier
{
    public class ServiceNotInstalledException : Exception
    {
        public ServiceNotInstalledException(string message) : base(message) { }
    }
}
