using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public class DiscoveredServer
    {
        public string Name { get; private set; }
        public Uri Address { get; private set; }
        public SemVersion Version { get; private set; }

        public DiscoveredServer(string beaconData, IPEndPoint endPoint)
        {
            var dataParts = beaconData.Split('|');

            if (Uri.TryCreate(dataParts.ElementAtOrDefault(0), UriKind.Absolute, out var address))
                Address = address;
            else
                Address = new Uri($"http://{endPoint.Address}:{endPoint.Port}");

            Name = dataParts.ElementAtOrDefault(1) ?? "LANCommander";

            if (SemVersion.TryParse(dataParts.ElementAtOrDefault(2), SemVersionStyles.Any, out var version))
                Version = version;
        }
    }
}
