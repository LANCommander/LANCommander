using Semver;
using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Exceptions
{
    public class ApiVersionMismatchException : Exception
    {
        private SemVersion ClientVersion;
        private SemVersion ServerVersion;

        public ApiVersionMismatchException(SemVersion client, SemVersion server, string message) : base(message) { }
    }
}
