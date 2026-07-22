using Semver;
using System;

namespace LANCommander.SDK.Exceptions
{
    public class ApiVersionMismatchException : Exception
    {
        public SemVersion ClientVersion { get; }
        public SemVersion ServerVersion { get; }

        public ApiVersionMismatchException(SemVersion client, SemVersion server, string message) : base(message)
        {
            ClientVersion = client;
            ServerVersion = server;
        }
    }
}
