using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Helpers
{
    public static class IPXRelayHelper
    {
        /// <summary>
        /// Resolves the address games and scripts should use to reach the server's IPX relay.
        /// The relay is hosted by the LANCommander server, so a blank host in settings means
        /// "wherever the server is". Hostnames are resolved to an address because the emulators
        /// consuming these variables (DOSBox and friends) often can't do their own name resolution.
        /// </summary>
        /// <param name="configuredHost">The host from the IPX relay settings. May be blank.</param>
        /// <param name="serverAddress">Address of the connected LANCommander server, used as the fallback.</param>
        /// <param name="logger">Optional logger for resolution failures.</param>
        /// <returns>An address for the relay, or null if no host could be determined.</returns>
        public static async Task<string> ResolveHostAsync(string configuredHost, Uri serverAddress, ILogger logger = null)
        {
            var host = configuredHost;

            if (String.IsNullOrWhiteSpace(host))
                host = serverAddress?.DnsSafeHost;

            if (String.IsNullOrWhiteSpace(host))
                return null;

            if (IPAddress.TryParse(host, out _))
                return host;

            try
            {
                var entry = await Dns.GetHostEntryAsync(host);

                var address = entry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                              ?? entry.AddressList.FirstOrDefault();

                if (address != null)
                    return address.ToString();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not resolve IPX relay host {Host} to an address, passing it through as-is", host);
            }

            return host;
        }
    }
}
