using LANCommander.Models;
using LANCommander.SDK.VPN.Configurations;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class VPNController : ControllerBase
    {
        public VPNController() { }

        [HttpGet]
        public async Task<VPNConfiguration> GetConfiguration()
        {
            var settings = SettingService.GetSettings();
            var configuration = new VPNConfiguration()
            {
                Type = SDK.Enums.VPNType.ZeroTier
            };

            switch (settings.VPN.Type)
            {
                case SDK.Enums.VPNType.ZeroTier:
                    configuration.Data = new ZeroTierConfiguration
                    {
                        NetworkId = (settings.VPN.Configuration as LANCommanderZeroTierSettings).NetworkId
                    };
                    break;
            }

            return configuration;
        }
    }
}
