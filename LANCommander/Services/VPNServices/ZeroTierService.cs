using LANCommander.Models;
using LANCommander.Models.ZeroTier;
using RestSharp;

namespace LANCommander.Services.VPNServices
{
    public class ZeroTierService
    {
        private const string BaseUrl = "https://api.zerotier.com/api/v1";
        private RestClient Client { get; set; }
        private LANCommanderZeroTierSettings Configuration { get; set; }

        public ZeroTierService() {
            Client = new RestClient(BaseUrl);

            var settings = SettingService.GetSettings();

            Configuration = settings.VPN.Configuration as LANCommanderZeroTierSettings;
        }

        public async Task ApproveNode(string nodeId, string username)
        {
            await PostRequestAsync<object>($"/network/{Configuration.NetworkId}/member/{nodeId}", new ModifyMemberRequest
            {
                Hidden = false,
                Name = username,
                Config = new MemberConfig
                {
                    Authorized = true
                }
            });
        }

        public async Task RemoveNode(string nodeId)
        {
            await DeleteRequest<object>($"/network/{Configuration.NetworkId}/member/{nodeId}");
        }

        private async Task<T> PostRequestAsync<T>(string route, object body)
        {
            if (String.IsNullOrWhiteSpace(Configuration.ApiKey))
                return default;

            var request = new RestRequest(route)
                .AddJsonBody(body)
                .AddHeader("Authorization", $"token {Configuration.ApiKey}");

            var response = await Client.PostAsync<T>(request);

            return response;
        }

        private async Task<T> PostRequestAsync<T>(string route)
        {
            if (String.IsNullOrWhiteSpace(Configuration.ApiKey))
                return default;

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"token {Configuration.ApiKey}");

            var response = await Client.PostAsync<T>(request);

            return response;
        }

        private async Task<T> GetRequest<T>(string route)
        {
            if (String.IsNullOrWhiteSpace(Configuration.ApiKey))
                return default;

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"token {Configuration.ApiKey}");

            var response = await Client.GetAsync<T>(request);

            return response;
        }

        private async Task<T> DeleteRequest<T>(string route)
        {
            if (String.IsNullOrWhiteSpace(Configuration.ApiKey))
                return default;

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"token {Configuration.ApiKey}");

            var response = await Client.DeleteAsync<T>(request);

            return response;
        }
    }
}
