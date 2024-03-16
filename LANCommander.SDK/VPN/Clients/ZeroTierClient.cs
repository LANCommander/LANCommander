using LANCommander.SDK.VPN.Configurations;
using LANCommander.SDK.VPN.Exceptions;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LANCommander.SDK.VPN.Clients
{
    public class ZeroTierClient : IVPNClient
    {
        private ZeroTierConfiguration Configuration;
        private RestClient ApiClient;
        private string Token;
        private const string BaseUrl = "http://localhost:9993";

        public ZeroTierClient(ZeroTierConfiguration configuration) {
            Configuration = configuration;

            ApiClient = new RestClient(BaseUrl);

            var tokenPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ZeroTier", "One", "authtoken.secret");

            if (File.Exists(tokenPath))
                Token = File.ReadAllText(tokenPath);
            else
                throw new ClientInitializationException("Could not read the token");
        }

        public bool Connect()
        {
            throw new NotImplementedException();
        }

        public bool Disconnect()
        {
            throw new NotImplementedException();
        }

        public VPNConnectionStatus GetStatus()
        {
            throw new NotImplementedException();
        }
    }
}
