using LANCommander.SDK.VPN;
using LANCommander.SDK.VPN.Clients;
using LANCommander.SDK.VPN.Configurations;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK
{
    public class VPNService
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }

        public delegate void OnConnectedHandler(object sender);
        public event OnConnectedHandler OnConnected;

        public delegate void OnDisconnectedHandler(object sender);
        public event OnDisconnectedHandler OnDisconnected;

        private IVPNClient VPNClient;
        
        public VPNService(Client client)
        {
            Client = client;
        }

        public VPNService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public void Init()
        {
            var configuration = Client.GetRequest<VPNConfiguration>("/api/VPN/Configuration");

            switch (configuration.Type)
            {
                case Enums.VPNType.ZeroTier:
                    VPNClient = new ZeroTierClient(configuration.Data as ZeroTierConfiguration);
                    break;
            }
        }

        public bool Connect()
        {
            return VPNClient.Connect();
        }

        public bool Disconnect()
        {
            return VPNClient.Disconnect();
        }

        public VPNConnectionStatus GetStatus()
        {
            return VPNClient.GetStatus();
        }
    }
}
