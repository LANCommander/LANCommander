using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Models
{
    public class DiscoveredServer
    {
        public string Name { get; private set; }
        public Uri Address { get; private set; }
        public SemVersion Version { get; private set; }

        public DiscoveredServer(BeaconMessage message, IPEndPoint endPoint)
        {
            if (Uri.TryCreate(message.Address, UriKind.Absolute, out var address))
                Address = address;
            else
                Address = new Uri($"http://{endPoint.Address}:{endPoint.Port}");

            Name = message.Name ?? "LANCommander";

            if (SemVersion.TryParse(message.Version, SemVersionStyles.Any, out var version))
                Version = version;
        }
    }
}
