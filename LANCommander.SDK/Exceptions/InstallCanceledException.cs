using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Exceptions
{
    public class InstallCanceledException : Exception
    {
        public InstallCanceledException(string message) : base(message) { }
    }
}
