using LANCommander.SDK.VPN.Configurations;
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

        public async Task<VPNConfiguration> GetConfiguration()
        {
            return new VPNConfiguration()
            {
                Type = SDK.Enums.VPNType.ZeroTier,
                Data = new ZeroTierConfiguration
                {
                    NetworkId = "<NetworkId>"
                }
            };
        }
    }
}
