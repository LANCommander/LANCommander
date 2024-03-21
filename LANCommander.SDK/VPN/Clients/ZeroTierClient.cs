using LANCommander.SDK.VPN.Configurations;
using LANCommander.SDK.VPN.Exceptions;
using LANCommander.SDK.VPN.Models.ZeroTier;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.VPN.Clients
{
    public class ZeroTierClient : IVPNClient
    {
        private ZeroTierConfiguration Configuration;
        private RestClient ZeroTierServiceClient;
        private Client Client;
        private string Token;
        private const string BaseUrl = "http://localhost:9993";

        public ZeroTierClient(ZeroTierConfiguration configuration, Client client) {
            Configuration = configuration;
            Client = client;

            ZeroTierServiceClient = new RestClient(BaseUrl);

            var tokenPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ZeroTier", "One", "authtoken.secret");

            if (File.Exists(tokenPath))
                Token = File.ReadAllText(tokenPath);
            else
                throw new ClientInitializationException("Could not read the token");
        }

        public bool Connect()
        {
            try
            {
                var task = PostRequest<UpdateNetworkSettingsResponse>($"/network/{Configuration.NetworkId}", new UpdateNetworkSettingsRequest
                {
                    AllowDefault = false,
                    AllowDNS = true,
                    AllowManaged = true,
                    AllowGlobal = false
                });

                task.Wait();

                var response = task.Result;

                Client.PostRequest<ApproveNodeRequest>("/api/ZeroTier/ApproveNode", new ApproveNodeRequest
                {
                    NodeId = response.Id
                });
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        public bool Disconnect()
        {
            try
            {
                var status = GetNodeStatus();

                var task = DeleteRequest<LeaveNetworkResponse>($"/network/{Configuration.NetworkId}");

                task.Wait();

                var response = task.Result;

                if (response.Result)
                {
                    Client.PostRequest<ApproveNodeRequest>("/api/ZeroTier/RemoveNode", new ApproveNodeRequest
                    {
                        NodeId = status.Address
                    });
                }

                return response.Result;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public VPNConnectionStatus GetStatus()
        {
            try
            {
                var response = GetNodeStatus();

                return new VPNConnectionStatus
                {
                    Connected = response.Online,
                    Status = response.Online ? "Connected" : "Not Connected"
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private NodeStatusResponse GetNodeStatus()
        {
            var task = GetRequest<NodeStatusResponse>("/status");

            task.Wait();

            var response = task.Result;

            return response;
        }

        private async Task<T> PostRequest<T>(string route, object body)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddJsonBody(body)
                .AddHeader("X-ZT1-AUTH", Token);

            var response = await ZeroTierServiceClient.PostAsync<T>(request);

            return response;
        }

        private async Task<T> PostRequest<T>(string route)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddHeader("X-ZT1-AUTH", Token);

            var response = await ZeroTierServiceClient.PostAsync<T>(request);

            return response;
        }

        private async Task<T> GetRequest<T>(string route)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddHeader("X-ZT1-AUTH", Token);

            var response = await ZeroTierServiceClient.GetAsync<T>(request);

            return response;
        }

        private async Task<T> DeleteRequest<T>(string route)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddHeader("X-ZT1-AUTH", Token);

            var response = await ZeroTierServiceClient.DeleteAsync<T>(request);

            return response;
        }
    }
}
