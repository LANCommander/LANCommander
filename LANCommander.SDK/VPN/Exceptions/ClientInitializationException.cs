using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.VPN.Exceptions
{
    public class ClientInitializationException : Exception
    {
        public ClientInitializationException(string message) : base(message) { }
    }
}
