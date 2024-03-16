using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.VPN.Exceptions
{
    public class ClientConnectionException : Exception
    {
        public ClientConnectionException(string message) : base(message) { }
    }
}
